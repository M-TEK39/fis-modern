import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/actions/auth";
import SessionRecovery from "@/app/home/session-recovery";
import {
  AccidentApiError,
  getAccidentAllReport,
  type AccidentAllReportDateMode,
  type AccidentVehicleReportRow,
} from "@/lib/api-accidents";
import { getSession } from "@/lib/session";

const ACCIDENTS_ROLE = "Accidents";
type QueryValue = string | string[] | undefined;
type ReportQuery = Record<string, QueryValue>;

function getQueryValue(query: ReportQuery, ...keys: string[]) {
  for (const key of keys) {
    const value = query[key];
    if (value !== undefined) {
      return Array.isArray(value) ? value[0] : value;
    }
  }

  return undefined;
}

function getMode(query: ReportQuery): { mode: AccidentAllReportDateMode; invalid: boolean } {
  const rawMode = (getQueryValue(query, "mode", "Radio1") ?? "").trim().toLowerCase();
  switch (rawMode) {
    case "2002-current":
    case "radionou":
    case "current":
      return { mode: "2002-current", invalid: false };
    case "1999-2001":
    case "radioou":
    case "middle":
      return { mode: "1999-2001", invalid: false };
    case "before-1999":
    case "radiobou":
    case "before":
      return { mode: "before-1999", invalid: false };
    case "":
      return { mode: "2002-current", invalid: false };
    default:
      return { mode: "2002-current", invalid: true };
  }
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some((candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0);
}

function valueOrDash(value: string | number | null) {
  return value === null || value === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) ?? "-";
}

function reportTitle(mode: AccidentAllReportDateMode) {
  switch (mode) {
    case "before-1999":
      return "Accidents Before 1999";
    case "1999-2001":
      return "Accidents From 1999 to 2001";
    default:
      return "Accidents From 2002 to Current";
  }
}

function LoadingState() {
  return <div className="loading-card" aria-busy="true"><span className="spinner" aria-hidden="true" /><p>Loading all accident report...</p></div>;
}

function ErrorState() {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>The all-accidents report could not be loaded.</h2><p className="muted-copy">Retry when the FIS API is available.</p><Link className="button button-primary" href="/accidents/reports/all">Try again</Link></section>;
}

function AllAccidentsReportTable({ rows, mode }: { rows: AccidentVehicleReportRow[]; mode: AccidentAllReportDateMode }) {
  return <section className="vehicle-status-maintenance-panel" aria-labelledby="all-accident-results-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Report results</p><h2 id="all-accident-results-title">{reportTitle(mode)}</h2></div><span className="form-hint">{rows.length} record(s)</span></div><div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">{reportTitle(mode)}</caption><thead><tr><th scope="col">Prov Reg Number</th><th scope="col">GG Number</th><th scope="col">Garage</th><th scope="col">Accid Date</th><th scope="col">Accid Place</th><th scope="col">Fin Year</th><th scope="col">Accident Description</th><th scope="col">Accident Category</th><th scope="col">Trip Auth</th><th scope="col">Driver Name</th><th scope="col">ID Number</th><th scope="col">Site</th><th scope="col">Trans Officer</th><th scope="col">TO Tel</th><th scope="col">HQ Ref</th><th scope="col">GG Ref</th><th scope="col">SA Ref</th><th scope="col">Case Num</th><th scope="col">GG Car Damage</th><th scope="col">GG Car Damage</th><th scope="col">Private Party Regno</th></tr></thead><tbody>{rows.map((row, index) => <tr key={`${row.accidentCode}-${index}`}><td>{valueOrDash(row.registrationNumber)}</td><td>{valueOrDash(row.fleetNumber)}</td><td>{valueOrDash(row.locationDescription)}</td><td>{formatDate(row.accidentDate)}</td><td>{valueOrDash(row.accidentPlace)}</td><td>{valueOrDash(row.financialYear)}</td><td>{valueOrDash(row.description)}</td><td>{valueOrDash(row.accidentTypeDescription)}</td><td>{valueOrDash(row.tripAuthority)}</td><td>{valueOrDash(row.driverName)}</td><td>{valueOrDash(row.driverEmployNumber)}</td><td>{valueOrDash(row.departmentNumber)}</td><td>{valueOrDash(row.transportOfficerName)}</td><td>{valueOrDash(row.transportOfficerTelephone)}</td><td>{valueOrDash(row.hqReference)}</td><td>{valueOrDash(row.ggReference)}</td><td>{valueOrDash(row.saReference)}</td><td>{valueOrDash(row.caseNumber)}</td><td>{valueOrDash(row.costOfRepair)}</td><td>{valueOrDash(row.damageDescription)}</td><td>{valueOrDash(row.thirdPartyRegistration)}</td></tr>)}</tbody></table></div><p className="vehicle-pagination-meta">Total Number: {rows.length}</p></section>;
}

async function AllAccidentsReportContent({ searchParams }: { searchParams: Promise<ReportQuery> }) {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <SessionRecovery returnPath="/accidents/reports/all" />;
  if (session.status === "unavailable") return <ErrorState />;
  if (!hasRole(session.roles, ACCIDENTS_ROLE)) {
    return <section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to run accident reports.</h2></section>;
  }

  const query = await searchParams;
  const { mode, invalid } = getMode(query);
  const shouldRun = getQueryValue(query, "run") === "1" || getQueryValue(query, "mode", "Radio1") !== undefined;
  const errorMessage = invalid ? "Choose a valid accident date range." : null;
  let rows: AccidentVehicleReportRow[] | null = null;
  if (shouldRun && !errorMessage) {
    try {
      rows = await getAccidentAllReport(mode);
    } catch (error) {
      if (error instanceof AccidentApiError && error.reason === "unauthorized") return <SessionRecovery returnPath="/accidents/reports/all" />;
      console.error("FIS all accident report failed", error instanceof Error ? error.message : "unknown error");
      return <ErrorState />;
    }
  }

  return <>
    {errorMessage ? <div className="notice notice-error" role="alert">{errorMessage}</div> : null}
    <form className="vehicle-status-maintenance-panel" method="get">
      <fieldset className="vehicle-search-options"><legend>Accident Date</legend><label className="vehicle-checkbox-label"><input name="mode" type="radio" value="2002-current" defaultChecked={mode === "2002-current"} /> 2002 - Current</label><label className="vehicle-checkbox-label"><input name="mode" type="radio" value="1999-2001" defaultChecked={mode === "1999-2001"} /> 1999 - 2001</label><label className="vehicle-checkbox-label"><input name="mode" type="radio" value="before-1999" defaultChecked={mode === "before-1999"} /> Before 1999</label></fieldset>
      <input name="run" type="hidden" value="1" />
      <div className="button-row"><button className="button button-primary" type="submit">Submit</button><Link className="button button-secondary" href="/accidents/reports">Report Menu</Link></div>
    </form>
    {rows ? (rows.length > 0 ? <AllAccidentsReportTable rows={rows} mode={mode} /> : <section className="vehicle-empty-state" aria-live="polite"><p className="eyebrow">No accidents found</p><h2>No accidents matched the selected date range.</h2><p className="muted-copy">Choose another date range and submit again.</p></section>) : null}
    <div className="vehicle-footer-actions"><Link className="button button-secondary" href="/accidents">Accident Menu</Link><Link className="button button-secondary" href="/accidents/reports/all">Clear</Link><Link className="button button-secondary" href="/home">Home</Link><form action={logoutAction}><button className="button button-secondary" type="submit">Sign out</button></form></div>
  </>;
}

export default async function AllAccidentsReportPage({ searchParams }: { searchParams: Promise<ReportQuery> }) {
  await connection();
  return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="all-accident-title"><header className="vehicle-page-header"><div><p className="eyebrow">Accident reports</p><h1 id="all-accident-title">All Accidents - All Detail</h1><p>Review accident records by the date bands used in the legacy report.</p></div><Link className="button button-secondary" href="/accidents/reports">Report Menu</Link></header><Suspense fallback={<LoadingState />}><AllAccidentsReportContent searchParams={searchParams} /></Suspense></section></main>;
}
