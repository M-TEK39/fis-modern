import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/actions/auth";
import SessionRecovery from "@/app/home/session-recovery";
import { AccidentApiError, getAccidentDriverReport, type AccidentDriverReportMode } from "@/lib/api-accidents";
import { getSession } from "@/lib/session";

const ACCIDENTS_ROLE = "Accidents";

type DriverReportPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getMode(value: string | undefined): AccidentDriverReportMode {
  return value === "id" || value === "Radioid" ? "id" : "name";
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some((candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0);
}

function valueOrDash(value: string | number | null) {
  return value === null || value === "" ? "-" : String(value);
}

function LoadingState() {
  return <div className="loading-card" aria-busy="true"><span className="spinner" aria-hidden="true" /><p>Loading driver report...</p></div>;
}

function ErrorState() {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>The driver report could not be loaded.</h2><p className="muted-copy">Retry when the FIS API is available.</p><Link className="button button-primary" href="/accidents/reports/driver">Try again</Link></section>;
}

async function DriverReportContent({ searchParams }: DriverReportPageProps) {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <SessionRecovery returnPath="/accidents/reports/driver" />;
  if (session.status === "unavailable") return <ErrorState />;
  if (!hasRole(session.roles, ACCIDENTS_ROLE)) {
    return <section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to run accident reports.</h2></section>;
  }

  const query = await searchParams;
  const mode = getMode(getQueryValue(query.mode) ?? getQueryValue(query.Radio1));
  const searchTerm = (getQueryValue(query.searchTerm) ?? getQueryValue(query.txtDname) ?? "").trim();
  const shouldRun = getQueryValue(query.run) === "1";
  let rows = null;
  if (shouldRun && searchTerm) {
    try {
      rows = await getAccidentDriverReport(searchTerm, mode);
    } catch (error) {
      if (error instanceof AccidentApiError && error.reason === "unauthorized") return <SessionRecovery returnPath="/accidents/reports/driver" />;
      console.error("FIS accident driver report failed", error instanceof Error ? error.message : "unknown error");
      return <ErrorState />;
    }
  }

  return (
    <>
      <form className="vehicle-status-maintenance-panel" method="get">
        <fieldset className="vehicle-search-options">
          <legend>Search by</legend>
          <label className="vehicle-checkbox-label"><input type="radio" name="mode" value="name" defaultChecked={mode === "name"} /> Driver name</label>
          <label className="vehicle-checkbox-label"><input type="radio" name="mode" value="id" defaultChecked={mode === "id"} /> ID number</label>
        </fieldset>
        <div className="field">
          <label htmlFor="driver-report-search">{mode === "name" ? "Driver name" : "ID number"}</label>
          <input id="driver-report-search" name="searchTerm" maxLength={20} defaultValue={searchTerm} required />
        </div>
        <input name="run" type="hidden" value="1" />
        <div className="button-row"><button className="button button-primary" type="submit">SUBMIT</button><Link className="button button-secondary" href="/accidents/reports">Report Menu</Link></div>
      </form>

      {rows !== null ? (
        rows.length === 0 ? (
          <div className="vehicle-empty-state"><p className="eyebrow">No accidents found</p><h2>No accidents matched this driver search.</h2><p className="muted-copy">Try a different driver name or ID number.</p></div>
        ) : (
          <>
            <div className="vehicle-table-wrapper" aria-live="polite">
              <table className="vehicle-table">
                <caption className="sr-only">Accident report by driver name or ID number</caption>
                <thead><tr><th scope="col">Registration Number</th><th scope="col">Fleet Number</th><th scope="col">Driver Name</th><th scope="col">ID Number</th><th scope="col">Accident Date</th><th scope="col">Dept/Site Number</th><th scope="col">Department/Site Desciption</th><th scope="col">Damage Amount</th></tr></thead>
                <tbody>{rows.map((row, index) => <tr key={`${row.fleetNumber ?? "vehicle"}-${row.accidentDate ?? "date"}-${index}`}><td>{valueOrDash(row.registrationNumber)}</td><td>{valueOrDash(row.fleetNumber)}</td><td>{valueOrDash(row.driverName)}</td><td>{valueOrDash(row.driverEmployNumber)}</td><td>{row.accidentDate?.slice(0, 10) ?? "-"}</td><td>{valueOrDash(row.departmentNumber)}</td><td>{valueOrDash(row.siteDescription)}</td><td>{valueOrDash(row.costOfRepair)}</td></tr>)}</tbody>
              </table>
            </div>
            <p className="vehicle-pagination-meta">Total Number: {rows.length}</p>
          </>
        )
      ) : null}

      <div className="vehicle-footer-actions"><Link className="button button-secondary" href="/accidents">Accident Menu</Link><Link className="button button-secondary" href="/home">Home</Link><form action={logoutAction}><button className="button button-secondary" type="submit">Sign out</button></form></div>
    </>
  );
}

export default async function AccidentDriverReportPage({ searchParams }: DriverReportPageProps) {
  await connection();
  return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="driver-report-title"><header className="vehicle-page-header"><div><p className="eyebrow">Accident reports</p><h1 id="driver-report-title">Accident Report By Driver Name Or ID Number</h1><p>Search by the beginning of a driver name or ID number.</p></div><Link className="button button-secondary" href="/accidents/reports">Report Menu</Link></header><Suspense fallback={<LoadingState />}><DriverReportContent searchParams={searchParams} /></Suspense></section></main>;
}
