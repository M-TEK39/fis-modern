import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { dateValue, queryValue, TaxiHeader, TaxiNotice, TaxiRestricted, TaxiUnavailable, valueOrDash } from "@/app/taxis/_components";
import { getLegacyReport, LegacyReportApiError, type LegacyReport } from "@/lib/api-legacy-reports";
import { getTaxis, TaxiApiError, type TaxiRecord } from "@/lib/api-taxis";
import { getSession } from "@/lib/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
export type TaxiReportKind = "one-taxi-number" | "logs-per-user" | "old-requisitions" | "taxis-per-company" | "taxis-per-department" | "taxis-inservice-per-department" | "logs-requisitions-status";

function ReportSearch({ kind, query }: Readonly<{ kind: TaxiReportKind; query: Record<string, string | string[] | undefined> }>) {
  return <form className="vehicle-status-maintenance-panel" method="get"><input type="hidden" name="mode" value={kind} /><div className="vehicle-search-row"><label className="form-label" htmlFor="taxi-report-search">{kind === "one-taxi-number" ? "Registration, GG, or requisition" : "Search report"}</label><input className="vehicle-search" id="taxi-report-search" name="search" defaultValue={queryValue(query.search)} placeholder="Optional search" /><button className="button button-primary" type="submit">Run report</button></div></form>;
}

function TaxiRows({ rows }: Readonly<{ rows: TaxiRecord[] }>) {
  return <div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Taxi report results</caption><thead><tr><th scope="col">Requisition</th><th scope="col">Date required</th><th scope="col">Official</th><th scope="col">GG / vehicle</th><th scope="col">Registration</th><th scope="col">Contractor</th><th scope="col">Department</th></tr></thead><tbody>{rows.length === 0 ? <tr><td colSpan={7}>No records found.</td></tr> : rows.slice(0, 500).map((taxi) => <tr key={taxi.requestId}><td><Link href={`/taxis/requests?mode=edit&requestId=${taxi.requestId}`}>{valueOrDash(taxi.rekNum)}</Link></td><td>{dateValue(taxi.dateRequired)}</td><td>{valueOrDash(taxi.official)}</td><td>{valueOrDash(taxi.vmfCode)}</td><td>{valueOrDash(taxi.regNum)}</td><td>{valueOrDash(taxi.contractorId)}</td><td>{valueOrDash(taxi.departmentName ?? taxi.departmentCode)}</td></tr>)}</tbody></table></div>;
}

function LegacyReportGrid({ report }: Readonly<{ report: LegacyReport }>) {
  return <><div className="vehicle-form-section-header"><div><p className="eyebrow">Report results</p><h2>{report.totalCount} record{report.totalCount === 1 ? "" : "s"}</h2></div></div><div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">{report.title}</caption><thead><tr>{report.columns.map((column) => <th scope="col" key={column.key}>{column.header}</th>)}</tr></thead><tbody>{report.rows.length === 0 ? <tr><td colSpan={Math.max(1, report.columns.length)}>No records found.</td></tr> : report.rows.slice(0, 500).map((row, index) => <tr key={`${report.reportKey}-${index}`}>{report.columns.map((column) => <td key={column.key}>{valueOrDash(row[column.key])}</td>)}</tr>)}</tbody></table></div>{report.isApproximate && report.approximationReason ? <p className="muted-copy">Report note: {report.approximationReason}</p> : null}</>;
}

function reportTitle(kind: TaxiReportKind) {
  return ({
    "one-taxi-number": "Report On One Taxi Number",
    "logs-per-user": "Number Of Taxi Logs Captured Per User For Date",
    "old-requisitions": "Old Requisitions For Period",
    "taxis-per-company": "Taxis Per Hire Company",
    "taxis-per-department": "List Of All Taxis In Various Departments",
    "taxis-inservice-per-department": "List Of All Taxis In Service In Various Departments",
    "logs-requisitions-status": "Taxi Logs and Requisitions Status Report",
  })[kind];
}

function reportKey(kind: TaxiReportKind) {
  if (kind === "taxis-per-department") return "taxis-list-per-department";
  if (kind === "taxis-inservice-per-department") return "taxis-list-inservice-per-department";
  return "taxis-financial";
}

export default async function TaxiReportsPage({ searchParams, kind: forcedKind }: Readonly<{ searchParams: SearchParams; kind?: TaxiReportKind }>) {
  await connection();
  const session = await getSession();
  const routePath = "/taxis/reports";
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
  if (!session.roles.some((role) => role.localeCompare("Private Hire Vehicles", undefined, { sensitivity: "accent" }) === 0)) return <main className="page-shell vehicle-page-shell"><TaxiRestricted subject="Taxi reports" /></main>;

  const query = await searchParams;
  const kind = forcedKind ?? (queryValue(query.mode) as TaxiReportKind || "one-taxi-number");
  const search = queryValue(query.search).trim().toLowerCase();
  try {
    if (kind === "one-taxi-number") {
      const taxis = await getTaxis();
      const filtered = search ? taxis.filter((taxi) => [taxi.rekNum, taxi.vmfCode ?? "", taxi.regNum ?? "", taxi.official ?? ""].some((value) => value.toLowerCase().includes(search))) : [];
      return <main className="page-shell vehicle-page-shell"><section className="vehicle-card"><TaxiHeader title={reportTitle(kind)} description="Enter the registration number, GG number, or requisition to view a taxi report." /><TaxiNotice query={query} /><ReportSearch kind={kind} query={query} /><section className="vehicle-status-maintenance-panel"><div className="vehicle-form-section-header"><div><p className="eyebrow">Report results</p><h2>{filtered.length} record{filtered.length === 1 ? "" : "s"}</h2></div></div><TaxiRows rows={filtered} /></section></section></main>;
    }

    const report = await getLegacyReport(reportKey(kind), search ? { search } : {});
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-card"><TaxiHeader title={reportTitle(kind)} description="Run the legacy-compatible taxi report with the selected search filter." /><TaxiNotice query={query} /><ReportSearch kind={kind} query={query} /><section className="vehicle-status-maintenance-panel"><LegacyReportGrid report={report} /></section></section></main>;
  } catch (error) {
    if ((error instanceof TaxiApiError || error instanceof LegacyReportApiError) && error.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={`${routePath}/${kind}`} /></main>;
    return <main className="page-shell vehicle-page-shell"><TaxiUnavailable path={`${routePath}/${kind}`} subject="Taxi reports" /></main>;
  }
}
