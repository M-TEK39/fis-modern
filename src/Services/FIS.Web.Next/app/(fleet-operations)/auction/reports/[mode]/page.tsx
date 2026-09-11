import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  AuctionApiError,
  getAuctionAllVehiclesReport,
  getAuctionByLotReport,
  getAuctionByNumberReport,
  getAuctionOneVehicleReport,
  getAuctionSaleToNameReport,
  getAuctions,
  type AuctionRecord,
} from "@/lib/api/fleet-operations/api-auction";
import { getSession } from "@/lib/auth/session";

const REPORTS_ROLE = "Reports";
const REPORT_MODES = [
  "one-vehicle",
  "all-vehicles",
  "sale-to-name",
  "auction-gg",
  "auction-lot",
] as const;
type ReportMode = (typeof REPORT_MODES)[number];

export type AuctionReportPageProps = {
  params: Promise<{ mode: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getPositiveInt(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasReportsRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(REPORTS_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function modeFromValue(value: string): ReportMode | null {
  return REPORT_MODES.includes(value as ReportMode) ? (value as ReportMode) : null;
}

function reportTitle(mode: ReportMode) {
  return {
    "one-vehicle": "Auction Report on ONE Vehicle",
    "all-vehicles": "Auction Report on ALL Vehicles",
    "sale-to-name": "Auction Report on Sale to Name",
    "auction-gg": "Auction Report of ONE Auction - Sort by GG Number",
    "auction-lot": "Auction Report of ONE Auction - Sort by LOT Number",
  }[mode];
}

function ReportForm({
  mode,
  search,
  auctions,
}: Readonly<{ mode: ReportMode; search: Record<string, string>; auctions: AuctionRecord[] }>) {
  const matchingVehicles =
    mode === "one-vehicle" && search.searchQuery
      ? auctions.filter((auction) => {
          const value =
            search.searchType === "GP" ? auction.registrationNumber : auction.fleetNumber;
          return (
            value?.toLocaleLowerCase().includes(search.searchQuery.toLocaleLowerCase()) === true
          );
        })
      : auctions;

  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <input name="run" type="hidden" value="1" />
      {mode === "one-vehicle" ? (
        <>
          <fieldset className="vehicle-search-options">
            <legend>Find vehicle by</legend>
            <label className="vehicle-checkbox-label">
              <input
                type="radio"
                name="searchType"
                value="GG"
                defaultChecked={search.searchType !== "GP"}
              />{" "}
              GG
            </label>
            <label className="vehicle-checkbox-label">
              <input
                type="radio"
                name="searchType"
                value="GP"
                defaultChecked={search.searchType === "GP"}
              />{" "}
              GP
            </label>
          </fieldset>
          <div className="vehicle-search-row">
            <label className="sr-only" htmlFor="auction-report-search">
              Vehicle number
            </label>
            <input
              className="vehicle-search"
              id="auction-report-search"
              maxLength={8}
              name="searchQuery"
              placeholder={search.searchType === "GP" ? "Enter GP number" : "Enter GG number"}
              defaultValue={search.searchQuery}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="auction-report-vehicle">
              Vehicle
            </label>
            <select
              className="form-select"
              id="auction-report-vehicle"
              name="vmfCode"
              defaultValue={search.vmfCode}
            >
              <option value="">Select vehicle...</option>
              {matchingVehicles.map((auction) => (
                <option key={`${auction.auctionCode}-${auction.vmfCode}`} value={auction.vmfCode}>
                  {valueOrDash(auction.fleetNumber)} / {valueOrDash(auction.registrationNumber)} (
                  {auction.vmfCode})
                </option>
              ))}
            </select>
          </div>
        </>
      ) : mode === "sale-to-name" ? (
        <div className="form-field">
          <label className="form-label" htmlFor="auction-buyer">
            Buyer Name
          </label>
          <input
            className="form-input"
            id="auction-buyer"
            maxLength={30}
            name="buyerName"
            defaultValue={search.buyerName}
          />
        </div>
      ) : (
        <>
          <div className="form-field">
            <label className="form-label" htmlFor="auction-report-garage">
              Garage
            </label>
            <select
              className="form-select"
              id="auction-report-garage"
              name="garage"
              defaultValue={search.garage}
            >
              <option value="JHB">JHB</option>
              <option value="PTA">PTA</option>
              <option value="ALL">ALL</option>
            </select>
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="auction-report-number">
              Auction Number
            </label>
            <input
              className="form-input"
              id="auction-report-number"
              maxLength={7}
              name="auctionNumber"
              placeholder="e.g. 2003/03"
              defaultValue={search.auctionNumber}
            />
          </div>
        </>
      )}
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Submit
        </button>
        <Link className="button button-secondary" href="/auction/reports">
          Menu
        </Link>
      </div>
    </form>
  );
}

function ReportTable({ rows }: Readonly<{ rows: AuctionRecord[] }>) {
  if (rows.length === 0) {
    return (
      <div className="vehicle-empty-state">
        <p className="eyebrow">No records found</p>
        <h2>No auction records matched the report.</h2>
        <p className="muted-copy">Adjust the parameters and try again.</p>
      </div>
    );
  }

  return (
    <div className="vehicle-table-wrapper" aria-live="polite">
      <table className="vehicle-table">
        <caption className="sr-only">Auction report results</caption>
        <thead>
          <tr>
            <th scope="col">GG</th>
            <th scope="col">GP</th>
            <th scope="col">Auction</th>
            <th scope="col">Lot</th>
            <th scope="col">Auth Date</th>
            <th scope="col">Sold To</th>
            <th scope="col">Sold Amount</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.auctionCode}>
              <td>{valueOrDash(row.fleetNumber)}</td>
              <td>{valueOrDash(row.registrationNumber)}</td>
              <td>{valueOrDash(row.auctionNumber)}</td>
              <td>{valueOrDash(row.lot)}</td>
              <td>{formatDate(row.authDate)}</td>
              <td>{valueOrDash(row.soldTo)}</td>
              <td>{valueOrDash(row.soldAmount)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>Auction report could not be generated.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href="/auction/reports">
        Back to reports
      </Link>
    </section>
  );
}

async function AuctionReportPageContent({
  params,
  searchParams,
  routePath = "/auction/reports",
}: AuctionReportPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") {
    redirect("/login");
  }
  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  }
  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }
  if (!hasReportsRole(session.roles)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to run Auction reports.</h2>
        </section>
      </main>
    );
  }

  const mode = modeFromValue((await params).mode);
  if (!mode) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Report not found</p>
          <h2>This Auction report does not exist.</h2>
          <Link className="button button-secondary" href="/auction/reports">
            Back to reports
          </Link>
        </section>
      </main>
    );
  }

  const query = await searchParams;
  const search: Record<string, string> = {
    searchType:
      getQueryValue(query.searchType) ?? (getQueryValue(query.Radio1) === "Radiogp" ? "GP" : "GG"),
    searchQuery: (getQueryValue(query.searchQuery) ?? getQueryValue(query.xnumber) ?? "")
      .trim()
      .slice(0, 8),
    vmfCode: getQueryValue(query.vmfCode) ?? "",
    garage: (getQueryValue(query.garage) ?? "JHB").toUpperCase(),
    auctionNumber: (getQueryValue(query.auctionNumber) ?? getQueryValue(query.xaucnumber) ?? "")
      .trim()
      .slice(0, 7),
    buyerName: (getQueryValue(query.buyerName) ?? getQueryValue(query.xbname) ?? "")
      .trim()
      .slice(0, 30),
  };

  try {
    const auctions = await getAuctions();
    let reportRows: AuctionRecord[] | null = null;
    if (getQueryValue(query.run) === "1") {
      if (mode === "one-vehicle") {
        const vmfCode = getPositiveInt(search.vmfCode);
        if (vmfCode) {
          reportRows = (await getAuctionOneVehicleReport(vmfCode)).data;
        }
      } else if (mode === "all-vehicles") {
        reportRows = (await getAuctionAllVehiclesReport(search.auctionNumber, search.garage)).data;
      } else if (mode === "sale-to-name") {
        reportRows = (await getAuctionSaleToNameReport(search.buyerName)).data;
      } else if (mode === "auction-gg") {
        reportRows = (await getAuctionByNumberReport(search.auctionNumber, search.garage)).data;
      } else {
        reportRows = (await getAuctionByLotReport(search.auctionNumber, search.garage)).data;
      }
    }

    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="auction-report-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Auction reports</p>
              <h1 id="auction-report-title">{reportTitle(mode)}</h1>
              <p>Run the legacy report with the current compatible Auction data.</p>
            </div>
            <Link className="button button-secondary" href="/auction">
              Auction Menu
            </Link>
          </header>
          <ReportForm mode={mode} search={search} auctions={auctions} />
          {reportRows !== null ? (
            <section
              className="vehicle-status-maintenance-panel"
              aria-labelledby="auction-report-results-title"
            >
              <div className="vehicle-form-section-header">
                <div>
                  <p className="eyebrow">Report results</p>
                  <h2 id="auction-report-results-title">{reportRows.length} record(s) returned</h2>
                </div>
              </div>
              <ReportTable rows={reportRows} />
            </section>
          ) : null}
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof AuctionApiError && error.reason === "unauthorized") {
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    }
    console.error(
      "FIS auction report request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }
}

export default function AuctionReportPage(props: AuctionReportPageProps) {
  return (
    <StreamedRoute>
      <AuctionReportPageContent {...props} />
    </StreamedRoute>
  );
}
