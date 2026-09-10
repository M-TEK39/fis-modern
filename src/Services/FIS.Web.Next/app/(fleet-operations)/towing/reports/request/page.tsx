import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";
import {
  getTowings,
  TowingApiError,
  type TowingRecord,
} from "@/lib/api/fleet-operations/api-towing";

const REPORTS_ROLE = "Reports";
export type TowingRequestReportPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};
function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
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
function SearchForm({
  callReference,
  startDate,
  endDate,
}: Readonly<{ callReference: string; startDate: string; endDate: string }>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <div className="form-grid">
        <div className="form-field">
          <label className="form-label" htmlFor="towing-call-reference">
            Reference Number
          </label>
          <input
            className="form-input"
            id="towing-call-reference"
            name="callReference"
            maxLength={5}
            defaultValue={callReference}
            placeholder="GMT reference"
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="towing-report-start">
            Begin Date
          </label>
          <input
            className="form-input"
            id="towing-report-start"
            name="startDate"
            type="date"
            defaultValue={startDate}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="towing-report-end">
            End Date
          </label>
          <input
            className="form-input"
            id="towing-report-end"
            name="endDate"
            type="date"
            defaultValue={endDate}
          />
        </div>
      </div>
      <p className="form-hint">
        Enter a reference to print one request, or use the date range for a request report.
      </p>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Submit
        </button>
        <Link className="button button-secondary" href="/towing/reports">
          Report Menu
        </Link>
      </div>
    </form>
  );
}
function Rows({ records }: Readonly<{ records: TowingRecord[] }>) {
  if (records.length === 0)
    return (
      <div className="vehicle-empty-state">
        <p className="eyebrow">No records found</p>
        <h2>No towing requests matched the report criteria.</h2>
        <p className="muted-copy">Check the reference or date range and try again.</p>
      </div>
    );
  return (
    <div className="vehicle-table-wrapper" aria-live="polite">
      <table className="vehicle-table">
        <caption className="sr-only">Road side assistance request report</caption>
        <thead>
          <tr>
            <th scope="col">Reference</th>
            <th scope="col">VMF</th>
            <th scope="col">Request Date</th>
            <th scope="col">Location</th>
            <th scope="col">Problem</th>
            <th scope="col">Keys</th>
          </tr>
        </thead>
        <tbody>
          {records.map((item) => (
            <tr key={item.towingCode}>
              <td>{valueOrDash(item.callReference)}</td>
              <td>{item.vmfCode}</td>
              <td>{formatDate(item.requestDate)}</td>
              <td>{valueOrDash(item.locationStart)}</td>
              <td>{valueOrDash(item.vehicleProblem)}</td>
              <td>{valueOrDash(item.keys)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export default async function TowingRequestReportPage({
  searchParams,
  routePath = "/towing/reports/request",
}: TowingRequestReportPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Towing request reports could not be loaded.</h2>
        </section>
      </main>
    );
  if (!hasReportsRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to run Towing reports.</h2>
        </section>
      </main>
    );
  const query = await searchParams;
  const callReference = (
    getQueryValue(query.callReference) ??
    getQueryValue(query.cccode) ??
    ""
  ).trim();
  const startDate = (getQueryValue(query.startDate) ?? "").trim();
  const endDate = (getQueryValue(query.endDate) ?? "").trim();
  const hasCriteria = Boolean(callReference || startDate || endDate);
  let records: TowingRecord[] = [];
  try {
    if (hasCriteria) {
      const all = await getTowings();
      const normalizedReference = callReference.toLocaleLowerCase();
      records = all.filter((item) => {
        if (
          normalizedReference &&
          String(item.callReference ?? "").toLocaleLowerCase() !== normalizedReference
        )
          return false;
        const date = item.requestDate?.slice(0, 10);
        if (startDate && (!date || date < startDate)) return false;
        if (endDate && (!date || date > endDate)) return false;
        return true;
      });
    }
  } catch (error) {
    if (error instanceof TowingApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS towing request report failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Towing request report could not be loaded.</h2>
          <Link className="button button-primary" href={routePath}>
            Try again
          </Link>
        </section>
      </main>
    );
  }
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="towing-request-report-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Road Side Assistance</p>
            <h1 id="towing-request-report-title">Road Side Assistance Request Report</h1>
            <p>Print or review a request by its GMT reference, or run the date-range report.</p>
          </div>
          <Link className="button button-secondary" href="/towing/reports">
            Report Menu
          </Link>
        </header>
        <SearchForm callReference={callReference} startDate={startDate} endDate={endDate} />
        {hasCriteria ? (
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="towing-request-report-results"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Report results</p>
                <h2 id="towing-request-report-results">Requests found</h2>
              </div>
            </div>
            <Rows records={records} />
          </section>
        ) : null}
      </section>
    </main>
  );
}
