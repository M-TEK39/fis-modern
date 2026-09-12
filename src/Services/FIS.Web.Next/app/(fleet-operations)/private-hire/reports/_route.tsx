import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import ReportPrintButton from "@/components/ui/report-print-button";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  ApiUnavailable,
  PrivateHireNotice,
  PrivateHireReportTableHeader,
} from "@/app/(fleet-operations)/private-hire/_components";
import { dateValue, queryValue, valueOrDash } from "@/app/(fleet-operations)/private-hire/_utils";
import {
  getPrivateHireContractors,
  getPrivateHireVehicles,
  PrivateHireApiError,
  type PrivateHireContractorRecord,
  type PrivateHireVehicleRecord,
} from "@/lib/api/fleet-operations/api-private-hire";
import { getSites, type SiteRecord } from "@/lib/api/reference-data/api-sites";
import { getSession } from "@/lib/auth/session";

const REPORT_DATE_FORMATTER = new Intl.DateTimeFormat("en-ZA", {
  timeZone: "Africa/Johannesburg",
});

const ROLE = "Private Hire Vehicles";
export type PrivateHireReportKind =
  "all" | "one" | "site" | "department" | "department-in-service" | "company";

function hasRole(roles: readonly string[]) {
  return roles.some((role) => role.localeCompare(ROLE, undefined, { sensitivity: "accent" }) === 0);
}

function getNumber(value: string) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function reportTitle(kind: PrivateHireReportKind) {
  return {
    all: "Report on All Private Hire Vehicles",
    one: "Report on One Private Hire Vehicle",
    site: "Private Hire Vehicles per Site",
    department: "Private Hire Vehicles per Department",
    "department-in-service": "Private Hire Vehicles per Department in Service",
    company: "Private Hire Vehicles per Hire Company",
  }[kind];
}

function siteOptionLabel(site: SiteRecord) {
  return `${site.description?.trim() || `Site ${site.siteCode}`} (${site.siteCode})`;
}

function ReportTable({
  vehicles,
  contractors,
}: Readonly<{ vehicles: PrivateHireVehicleRecord[]; contractors: PrivateHireContractorRecord[] }>) {
  const contractorNames = new Map(
    contractors.map((contractor) => [contractor.contractorId, contractor.companyName]),
  );
  if (vehicles.length === 0)
    return <p className="muted-copy">No records found for the selected report.</p>;
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Private Hire vehicle report</caption>
        <PrivateHireReportTableHeader
          columns={[
            "Registration",
            "Model description",
            "Engine no.",
            "Chassis no.",
            "Site",
            "Contracted to",
            "Year",
            "Colour",
            "Take-on",
            "Take-on odo",
            "Return",
            "Return odo",
          ]}
        />
        <tbody>
          {vehicles.slice(0, 500).map((vehicle) => (
            <tr key={vehicle.phvCode}>
              <td>{valueOrDash(vehicle.registrationNumber)}</td>
              <td>{valueOrDash(vehicle.modelDescription)}</td>
              <td>{valueOrDash(vehicle.engineNumber)}</td>
              <td>{valueOrDash(vehicle.chassisNumber)}</td>
              <td>{valueOrDash(vehicle.siteCode)}</td>
              <td>
                {valueOrDash(
                  contractorNames.get(vehicle.contractorId) ??
                    vehicle.contractedTo ??
                    vehicle.contractorId,
                )}
              </td>
              <td>{valueOrDash(vehicle.yearManufactured)}</td>
              <td>{valueOrDash(vehicle.colour)}</td>
              <td>{dateValue(vehicle.takeOnDate)}</td>
              <td>{valueOrDash(vehicle.takeOnOdo)}</td>
              <td>{dateValue(vehicle.returnDate)}</td>
              <td>{valueOrDash(vehicle.returnOdo)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ReportFilter({
  kind,
  query,
  contractors,
  sites,
}: Readonly<{
  kind: PrivateHireReportKind;
  query: Record<string, string | string[] | undefined>;
  contractors: PrivateHireContractorRecord[];
  sites: SiteRecord[];
}>) {
  if (kind === "all") return null;
  const search = queryValue(query.search);
  const code = queryValue(query.code);
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report filter</p>
          <h2>Choose the report scope</h2>
        </div>
      </div>
      {kind === "one" ? (
        <div className="vehicle-search-row">
          <label className="sr-only" htmlFor="private-hire-report-search">
            Vehicle search
          </label>
          <input
            className="vehicle-search"
            id="private-hire-report-search"
            name="search"
            defaultValue={search}
            placeholder="Registration, engine, chassis, or PHV code"
          />
          <button className="button button-primary" type="submit">
            Run report
          </button>
        </div>
      ) : kind === "company" ? (
        <div className="vehicle-search-row">
          <label className="form-label" htmlFor="private-hire-report-company">
            Hire company
          </label>
          <select
            className="form-select"
            id="private-hire-report-company"
            name="code"
            defaultValue={code}
          >
            <option value="">Select a hire company</option>
            {contractors.map((contractor) => (
              <option key={contractor.contractorId} value={contractor.contractorId}>
                {contractor.companyName} ({contractor.contractorId})
              </option>
            ))}
          </select>
          <button className="button button-primary" type="submit">
            Run report
          </button>
        </div>
      ) : (
        <div className="vehicle-search-row">
          <label className="form-label" htmlFor="private-hire-report-code">
            {kind === "site" ? "Site" : "Contracted-to site"}
          </label>
          {sites.length > 0 ? (
            <select
              className="form-select"
              id="private-hire-report-code"
              name="code"
              defaultValue={code}
              required
            >
              <option value="">Select a site</option>
              {sites.map((site) => (
                <option key={site.siteCode} value={site.siteCode}>
                  {siteOptionLabel(site)}
                </option>
              ))}
            </select>
          ) : (
            <>
              <input
                className="vehicle-search"
                id="private-hire-report-code"
                name="code"
                type="number"
                min="1"
                defaultValue={code}
                required
              />
              <p className="muted-copy" role="status">
                Site options are temporarily unavailable, so the existing reference can be entered.
              </p>
            </>
          )}
          <button className="button button-primary" type="submit">
            Run report
          </button>
        </div>
      )}
    </form>
  );
}

const PrivateHireReportPageContent = renderPrivateHireReportPageContent;

async function renderPrivateHireReportPageContent({
  searchParams,
  kind = "all",
  routePath = "/private-hire/reports/all-vehicles",
}: Readonly<{
  searchParams?: Promise<Record<string, string | string[] | undefined>>;
  kind?: PrivateHireReportKind;
  routePath?: string;
}>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (!hasRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to view Private Hire reports.</h2>
        </section>
      </main>
    );

  const query = searchParams ? await searchParams : {};
  try {
    const [vehicles, contractors, sites] = await Promise.all([
      getPrivateHireVehicles(),
      getPrivateHireContractors(),
      getSites().catch(() => []),
    ]);
    const code = getNumber(queryValue(query.code));
    const search = queryValue(query.search).trim().toLowerCase();
    const filtered =
      kind === "one" && search
        ? vehicles.filter((vehicle) =>
            [
              String(vehicle.phvCode),
              vehicle.registrationNumber,
              vehicle.engineNumber ?? "",
              vehicle.chassisNumber ?? "",
            ].some((value) => value.toLowerCase().includes(search)),
          )
        : kind === "site" && code
          ? vehicles.filter((vehicle) => vehicle.siteCode === code)
          : (kind === "department" || kind === "department-in-service") && code
            ? vehicles.filter(
                (vehicle) =>
                  vehicle.contractedTo === code &&
                  (kind !== "department-in-service" || vehicle.returnDate === null),
              )
            : kind === "company" && code
              ? vehicles.filter((vehicle) => vehicle.contractorId === code)
              : kind === "one" && !search
                ? []
                : vehicles;
    const reportDate = REPORT_DATE_FORMATTER.format(new Date());

    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="private-hire-report-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Private Hire reports</p>
              <h1 id="private-hire-report-title">{reportTitle(kind)}</h1>
              <p>Date of report: {reportDate}</p>
            </div>
            <Link className="button button-secondary" href="/private-hire/maintenance-menu">
              Menu
            </Link>
          </header>
          <PrivateHireNotice query={query} />
          <ReportFilter kind={kind} query={query} contractors={contractors} sites={sites} />
          <section
            className="vehicle-status-maintenance-panel report-print-area"
            aria-labelledby="private-hire-report-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Report results</p>
                <h2 id="private-hire-report-results-title">
                  {filtered.length} record{filtered.length === 1 ? "" : "s"}
                </h2>
              </div>
              <ReportPrintButton />
            </div>
            <ReportTable vehicles={filtered} contractors={contractors} />
          </section>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof PrivateHireApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable path={routePath} subject="Private Hire reports" />
      </main>
    );
  }
}

export default function PrivateHireReportPage(
  props: Readonly<{
    searchParams?: Promise<Record<string, string | string[] | undefined>>;
    kind?: PrivateHireReportKind;
    routePath?: string;
  }>,
) {
  return (
    <StreamedRoute>
      <PrivateHireReportPageContent {...props} />
    </StreamedRoute>
  );
}

export function PrivateHireReportRoute({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return <PrivateHireReportPage searchParams={searchParams} />;
}
