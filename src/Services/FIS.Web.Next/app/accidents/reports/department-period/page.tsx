import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/actions/auth";
import SessionRecovery from "@/app/home/session-recovery";
import {
  AccidentApiError,
  getAccidentDepartmentPeriodReport,
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

function normalizeDate(value: string) {
  const normalized = value.trim();
  if (/^\d{4}-\d{2}-\d{2}$/.test(normalized)) {
    return normalized;
  }

  const yearFirst = normalized.match(/^(\d{4})[\/-](\d{1,2})[\/-](\d{1,2})$/);
  if (yearFirst) {
    return `${yearFirst[1]}-${yearFirst[2].padStart(2, "0")}-${yearFirst[3].padStart(2, "0")}`;
  }

  const dayFirst = normalized.match(/^(\d{1,2})[\/-](\d{1,2})[\/-](\d{4})$/);
  if (dayFirst) {
    return `${dayFirst[3]}-${dayFirst[2].padStart(2, "0")}-${dayFirst[1].padStart(2, "0")}`;
  }

  return "";
}

function isValidDate(value: string) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return false;
  }

  const [year, month, day] = value.split("-").map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day;
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some((candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0);
}

function valueOrDash(value: string | number | null) {
  return value === null || value === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  if (!value) {
    return "-";
  }

  const normalized = normalizeDate(value.slice(0, 10));
  return normalized || value;
}

function LoadingState() {
  return <div className="loading-card" aria-busy="true"><span className="spinner" aria-hidden="true" /><p>Loading department accident report...</p></div>;
}

function ErrorState() {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>The department accident report could not be loaded.</h2><p className="muted-copy">Retry when the FIS API is available.</p><Link className="button button-primary" href="/accidents/reports/department-period">Try again</Link></section>;
}

function DepartmentPeriodReportTable({ rows }: { rows: AccidentVehicleReportRow[] }) {
  return <section className="vehicle-status-maintenance-panel" aria-labelledby="department-period-results-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Report results</p><h2 id="department-period-results-title">Department/Site Accident Report</h2></div><span className="form-hint">{rows.length} record(s)</span></div><div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Accident report for the selected department or site and period</caption><thead><tr><th scope="col">Prov Reg Number</th><th scope="col">GG Number</th><th scope="col">Call Refer</th><th scope="col">Garage</th><th scope="col">Accid Date</th><th scope="col">Accid Place</th><th scope="col">Accident Category</th><th scope="col">Trip Auth</th><th scope="col">Driver Name</th><th scope="col">Dept/Site Code</th><th scope="col">Dept/Site</th><th scope="col">Trans Officer</th><th scope="col">TO Tel</th><th scope="col">HQ Ref</th><th scope="col">GG Ref</th><th scope="col">Case Num</th><th scope="col">GG Car Damage</th><th scope="col">Priv Car Damage</th><th scope="col">Cost Claim Agains Dept</th><th scope="col">Notes</th></tr></thead><tbody>{rows.map((row, index) => <tr key={`${row.accidentCode}-${index}`}><td>{valueOrDash(row.registrationNumber)}</td><td>{valueOrDash(row.fleetNumber)}</td><td>{valueOrDash(row.callRefer)}</td><td>{valueOrDash(row.locationDescription)}</td><td>{formatDate(row.accidentDate)}</td><td>{valueOrDash(row.accidentPlace)}</td><td>{valueOrDash(row.accidentTypeDescription)}</td><td>{valueOrDash(row.tripAuthority)}</td><td>{valueOrDash(row.driverName)}</td><td>{valueOrDash(row.departmentNumber)}</td><td>{valueOrDash(row.siteDescription)}</td><td>{valueOrDash(row.transportOfficerName)}</td><td>{valueOrDash(row.transportOfficerTelephone)}</td><td>{valueOrDash(row.hqReference)}</td><td>{valueOrDash(row.ggReference)}</td><td>{valueOrDash(row.caseNumber)}</td><td>{valueOrDash(row.costOfRepair)}</td><td>{valueOrDash(row.thirdPartyClaim)}</td><td>{valueOrDash(row.claimAgainstDepartment)}</td><td>{valueOrDash(row.notes)}</td></tr>)}</tbody></table></div><p className="vehicle-pagination-meta">Total Number: {rows.length}</p></section>;
}

async function DepartmentPeriodReportContent({ searchParams }: { searchParams: Promise<ReportQuery> }) {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <SessionRecovery returnPath="/accidents/reports/department-period" />;
  if (session.status === "unavailable") return <ErrorState />;
  if (!hasRole(session.roles, ACCIDENTS_ROLE)) {
    return <section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to run accident reports.</h2></section>;
  }

  const query = await searchParams;
  const departmentNumber = (getQueryValue(query, "departmentNumber", "xdept") ?? "").trim();
  const startDate = normalizeDate(getQueryValue(query, "startDate", "BDAT") ?? "");
  const endDate = normalizeDate(getQueryValue(query, "endDate", "EDAT") ?? "");
  const shouldRun = getQueryValue(query, "run") === "1" || ["departmentNumber", "xdept", "startDate", "BDAT", "endDate", "EDAT"].some((key) => getQueryValue(query, key) !== undefined);
  let errorMessage: string | null = null;
  if (shouldRun && (!startDate || !endDate || !isValidDate(startDate) || !isValidDate(endDate))) {
    errorMessage = "Enter a valid begin date and end date.";
  } else if (shouldRun && endDate < startDate) {
    errorMessage = "The begin date must be on or before the end date.";
  }

  let rows: AccidentVehicleReportRow[] | null = null;
  if (shouldRun && !errorMessage) {
    try {
      rows = await getAccidentDepartmentPeriodReport(departmentNumber, startDate, endDate);
    } catch (error) {
      if (error instanceof AccidentApiError && error.reason === "unauthorized") return <SessionRecovery returnPath="/accidents/reports/department-period" />;
      console.error("FIS accident department period report failed", error instanceof Error ? error.message : "unknown error");
      return <ErrorState />;
    }
  }

  return <>
    {errorMessage ? <div className="notice notice-error" role="alert">{errorMessage}</div> : null}
    <form className="vehicle-status-maintenance-panel" method="get">
      <div className="form-grid">
        <div className="field"><label htmlFor="accident-department-period-department">Department/Site Code</label><input id="accident-department-period-department" name="departmentNumber" maxLength={30} defaultValue={departmentNumber} /></div>
        <div className="field"><label htmlFor="accident-department-period-start">Begin Date</label><input id="accident-department-period-start" name="startDate" type="date" defaultValue={startDate} required /></div>
        <div className="field"><label htmlFor="accident-department-period-end">End Date</label><input id="accident-department-period-end" name="endDate" type="date" defaultValue={endDate} required /></div>
      </div>
      <input name="run" type="hidden" value="1" />
      <div className="button-row"><button className="button button-primary" type="submit">Submit</button><Link className="button button-secondary" href="/accidents/reports">Report Menu</Link></div>
    </form>
    {rows ? (rows.length > 0 ? <DepartmentPeriodReportTable rows={rows} /> : <section className="vehicle-empty-state" aria-live="polite"><p className="eyebrow">No accidents found</p><h2>No accidents matched the selected department and period.</h2><p className="muted-copy">Update the department or dates and submit again.</p></section>) : null}
    <div className="vehicle-footer-actions"><Link className="button button-secondary" href="/accidents">Accident Menu</Link><Link className="button button-secondary" href="/accidents/reports/department-period">Clear</Link><Link className="button button-secondary" href="/home">Home</Link><form action={logoutAction}><button className="button button-secondary" type="submit">Sign out</button></form></div>
  </>;
}

export default async function DepartmentPeriodReportPage({ searchParams }: { searchParams: Promise<ReportQuery> }) {
  await connection();
  return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="accident-department-period-title"><header className="vehicle-page-header"><div><p className="eyebrow">Accident reports</p><h1 id="accident-department-period-title">Accident Report for a Department/Site, for a Period</h1><p>Review the legacy department or site accident report for an inclusive date period.</p></div><Link className="button button-secondary" href="/accidents/reports">Report Menu</Link></header><Suspense fallback={<LoadingState />}><DepartmentPeriodReportContent searchParams={searchParams} /></Suspense></section></main>;
}
