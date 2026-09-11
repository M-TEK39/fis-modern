import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import {
  AuctionApiError,
  getAuctionPage,
  type AuctionRecord,
  type AuctionSearchType,
} from "@/lib/api/fleet-operations/api-auction";
import { getSession } from "@/lib/auth/session";

const REPORTS_ROLE = "Reports";

export type AuctionMaintenancePageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getSearchType(value: string | undefined): AuctionSearchType {
  return value === "GP" || value === "Radiogp" ? "GP" : "GG";
}

function getPositiveQueryInt(value: string | undefined) {
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

function buildDetailHref(auctionCode: number, searchType: AuctionSearchType, searchQuery: string) {
  const params = new URLSearchParams({ auctionId: String(auctionCode) });
  if (searchQuery) {
    params.set("searchType", searchType);
    params.set("searchQuery", searchQuery);
  }
  return `/auction/maintenance/detail?${params.toString()}`;
}

function pageHref(
  routePath: string,
  searchType: AuctionSearchType,
  searchQuery: string,
  page: number,
) {
  return `${routePath}?${new URLSearchParams({ searchType, searchQuery, page: String(page) }).toString()}`;
}

function AuctionPagination({
  routePath,
  searchType,
  searchQuery,
  page,
  totalPages,
}: Readonly<{
  routePath: string;
  searchType: AuctionSearchType;
  searchQuery: string;
  page: number;
  totalPages: number;
}>) {
  if (totalPages <= 1) return null;

  return (
    <nav className="vehicle-pagination" aria-label="Auction maintenance pages">
      {page > 1 ? (
        <Link
          className="vehicle-pagination-button"
          href={pageHref(routePath, searchType, searchQuery, page - 1)}
        >
          Previous
        </Link>
      ) : (
        <span className="vehicle-pagination-button vehicle-pagination-disabled">Previous</span>
      )}
      <span className="vehicle-pagination-meta" aria-live="polite">
        Page {page} of {totalPages}
      </span>
      {page < totalPages ? (
        <Link
          className="vehicle-pagination-button"
          href={pageHref(routePath, searchType, searchQuery, page + 1)}
        >
          Next
        </Link>
      ) : (
        <span className="vehicle-pagination-button vehicle-pagination-disabled">Next</span>
      )}
    </nav>
  );
}

function SearchForm({
  searchType,
  searchQuery,
}: Readonly<{ searchType: AuctionSearchType; searchQuery: string }>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <fieldset className="vehicle-search-options">
        <legend>Search by</legend>
        <label className="vehicle-checkbox-label">
          <input type="radio" name="searchType" value="GG" defaultChecked={searchType === "GG"} />{" "}
          GG
        </label>
        <label className="vehicle-checkbox-label">
          <input type="radio" name="searchType" value="GP" defaultChecked={searchType === "GP"} />{" "}
          GP
        </label>
      </fieldset>
      <div className="vehicle-search-row">
        <label className="sr-only" htmlFor="auction-vehicle-search">
          {searchType === "GG" ? "GG number" : "GP number"}
        </label>
        <input
          className="vehicle-search"
          id="auction-vehicle-search"
          maxLength={8}
          name="searchQuery"
          placeholder={searchType === "GG" ? "Enter GG number" : "Enter GP number"}
          defaultValue={searchQuery}
        />
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Submit
        </button>
        <Link className="button button-secondary" href="/auction">
          Menu
        </Link>
      </div>
    </form>
  );
}

function AuctionRows({
  auctions,
  searchType,
  searchQuery,
}: Readonly<{ auctions: AuctionRecord[]; searchType: AuctionSearchType; searchQuery: string }>) {
  if (auctions.length === 0) {
    return (
      <div className="vehicle-empty-state">
        <p className="eyebrow">No records found</p>
        <h2>
          {searchQuery
            ? `No auction records matched “${searchQuery}”.`
            : "No auction records are available."}
        </h2>
        <p className="muted-copy">Try another GG or GP number.</p>
      </div>
    );
  }

  return (
    <div className="vehicle-table-wrapper" aria-live="polite">
      <table className="vehicle-table">
        <caption className="sr-only">Auction records available for maintenance</caption>
        <thead>
          <tr>
            <th scope="col">Number</th>
            <th scope="col">Auction Number</th>
            <th scope="col">Auction Garage</th>
            <th scope="col">Action</th>
          </tr>
        </thead>
        <tbody>
          {auctions.map((auction) => (
            <tr key={auction.auctionCode}>
              <td>
                {valueOrDash(
                  searchType === "GG" ? auction.fleetNumber : auction.registrationNumber,
                )}
              </td>
              <td>{valueOrDash(auction.auctionNumber)}</td>
              <td>{valueOrDash(auction.camp ?? auction.auctionGarage)}</td>
              <td>
                <Link
                  className="button button-secondary button-small"
                  href={buildDetailHref(auction.auctionCode, searchType, searchQuery)}
                >
                  Mod
                </Link>
              </td>
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
      <h2>Auction maintenance could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/auction/maintenance">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

async function AuctionMaintenancePageContent({
  searchParams,
  routePath = "/auction/maintenance",
}: AuctionMaintenancePageProps) {
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
          <h2>You do not have permission to maintain Auction records.</h2>
        </section>
      </main>
    );
  }

  const query = await searchParams;
  const searchType = getSearchType(getQueryValue(query.searchType) ?? getQueryValue(query.Radio1));
  const searchQuery = (getQueryValue(query.searchQuery) ?? getQueryValue(query.txtGGNum) ?? "")
    .trim()
    .slice(0, 8);
  const requestedPage = getPositiveQueryInt(getQueryValue(query.page)) ?? 1;
  const notice =
    getQueryValue(query.updated) === "1"
      ? "Auction record updated successfully."
      : getQueryValue(query.error);

  try {
    const result = await getAuctionPage(searchType, searchQuery, requestedPage);
    const auctions = result.items;

    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="auction-maintenance-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Auction maintenance</p>
              <h1 id="auction-maintenance-title">Auction Maintenance</h1>
              <p>Search a vehicle by GG or GP number, then update its legacy auction record.</p>
            </div>
            <Link className="button button-secondary" href="/auction">
              Auction Menu
            </Link>
          </header>
          {notice ? (
            <div
              className={
                getQueryValue(query.error) ? "notice notice-error" : "notice notice-success"
              }
              role={getQueryValue(query.error) ? "alert" : "status"}
            >
              {notice}
            </div>
          ) : null}
          <SearchForm searchType={searchType} searchQuery={searchQuery} />
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="auction-maintenance-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Johannesburg Garage</p>
                <h2 id="auction-maintenance-results-title">Auction records found</h2>
              </div>
            </div>
            <AuctionRows auctions={auctions} searchType={searchType} searchQuery={searchQuery} />
            <AuctionPagination
              routePath={routePath}
              searchType={searchType}
              searchQuery={searchQuery}
              page={result.page}
              totalPages={result.totalPages}
            />
          </section>
          <div className="vehicle-footer-actions">
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
          </div>
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
      "FIS auction maintenance lookup failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }
}

export default function AuctionMaintenancePage(props: AuctionMaintenancePageProps) {
  return (
    <StreamedRoute>
      <AuctionMaintenancePageContent {...props} />
    </StreamedRoute>
  );
}
