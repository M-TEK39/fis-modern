import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { hasTripAuthorityAccess, getTripSession, tripAccessRestricted, tripSessionMessage } from "@/app/trips/_page";
import { getLegacyReport, LegacyReportApiError, type LegacyReport } from "@/lib/api-legacy-reports";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function financialYearOptions() {
  const currentYear = new Date().getFullYear();
  return Array.from({ length: 11 }, (_, index) => currentYear - index).map((startYear) => ({
    code: String(startYear),
    label: `${startYear}/${startYear + 1}`,
  }));
}

function valueForRow(row: Record<string, string | null>, key: string) {
  const matchingKey = Object.keys(row).find((candidate) => candidate.localeCompare(key, undefined, { sensitivity: "accent" }) === 0);
  return matchingKey ? row[matchingKey] || "-" : "-";
}

function ReportResults({ report }: Readonly<{ report: LegacyReport }>) {
  return <section className="vehicle-status-maintenance-panel" aria-labelledby="driver-info-results-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">{report.totalCount} record{report.totalCount === 1 ? "" : "s"}</p><h2 id="driver-info-results-title">{report.title}</h2></div></div>{report.isApproximate && report.approximationReason ? <div className="notice notice-info" role="status">{report.approximationReason}</div> : null}{report.rows.length === 0 ? <div className="vehicle-empty-state"><p>No driver information found for the selected financial year.</p></div> : <div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Driver information report</caption><thead><tr>{report.columns.map((column) => <th key={column.key} scope="col">{column.header}</th>)}</tr></thead><tbody>{report.rows.map((row, index) => <tr key={`${report.reportKey}-${index}`}>{report.columns.map((column) => <td key={column.key}>{valueForRow(row, column.key)}</td>)}</tr>)}</tbody></table></div>}</section>;
}

function ApiUnavailable() {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>The driver information report could not be loaded.</h2><p className="muted-copy">Retry when the FIS API is available.</p><Link className="button button-primary" href="/trips/driver-info">Try again</Link></section>;
}

export default async function DriverInfoPage({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getTripSession();
  const sessionProblem = tripSessionMessage(session, "/trips/driver-info");
  if (sessionProblem) return sessionProblem;
  if (session.status !== "authenticated") return tripAccessRestricted("Your session could not be loaded.");
  if (!hasTripAuthorityAccess(session)) return tripAccessRestricted();

  const query = await searchParams;
  const selectedYear = (queryValue(query.financialYear) ?? queryValue(query.FinYear) ?? "").trim();
  const shouldRun = queryValue(query.run) === "1" || selectedYear.length > 0;
  const errorMessage = shouldRun && !/^\d{4}$/.test(selectedYear) ? "Select a valid financial year." : null;
  let report: LegacyReport | null = null;
  if (shouldRun && !errorMessage) {
    try {
      report = await getLegacyReport("driver-information-finyear", { FinYear: selectedYear });
    } catch (error) {
      if (error instanceof LegacyReportApiError && error.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/trips/driver-info" /></main>;
      console.error("FIS driver information report request failed", error instanceof Error ? error.message : "unknown error");
      return <main className="page-shell vehicle-page-shell"><ApiUnavailable /></main>;
    }
  }

  return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="driver-info-title"><header className="vehicle-page-header"><div><p className="eyebrow">Trip tools</p><h1 id="driver-info-title">Driver Information</h1><p>Driver information over a financial year selection.</p></div><Link className="button button-secondary" href="/trip-authorities">Back to Trips</Link></header>{errorMessage ? <div className="notice notice-error" role="alert">{errorMessage}</div> : null}<form className="vehicle-status-maintenance-panel" method="get"><div className="form-grid"><div className="form-field"><label className="form-label" htmlFor="driver-info-financial-year">Financial Year</label><select className="form-select" id="driver-info-financial-year" name="financialYear" defaultValue={selectedYear}><option value="">Select Financial Year</option>{financialYearOptions().map((option) => <option key={option.code} value={option.code}>{option.label}</option>)}</select></div></div><input name="run" type="hidden" value="1" /><div className="button-row"><button className="button button-primary" type="submit">Submit</button><Link className="button button-secondary" href="/trips/driver-info">Clear</Link></div></form>{report ? <ReportResults report={report} /> : null}<div className="vehicle-footer-actions"><Link className="button button-secondary" href="/trip-authorities">Back to Trips</Link><Link className="button button-secondary" href="/home">Home</Link></div></section></main>;
}
