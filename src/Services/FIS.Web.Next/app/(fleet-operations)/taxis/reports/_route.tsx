import DataTableHeader from "@/components/ui/data-table-header";
import ReportPrintButton from "@/components/ui/report-print-button";

import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  TaxiHeader,
  TaxiNotice,
  TaxiPagination,
  TaxiRestricted,
  TaxiUnavailable,
} from "@/app/(fleet-operations)/taxis/_components";
import {
  dateValue,
  queryValue,
  taxiPageHref,
  valueOrDash,
} from "@/app/(fleet-operations)/taxis/_utils";
import { ReportPagination } from "@/app/(fleet-operations)/reports/_components";
import {
  getLegacyReport,
  LegacyReportApiError,
  type LegacyReport,
} from "@/lib/api/reports/api-legacy-reports";
import { getTaxiPage, TaxiApiError, type TaxiRecord } from "@/lib/api/fleet-operations/api-taxis";
import { getSession } from "@/lib/auth/session";
import { hasTaxiAccess } from "@/app/(fleet-operations)/taxis/access";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
export type TaxiReportKind =
  | "one-taxi-number"
  | "logs-per-user"
  | "old-requisitions"
  | "taxis-per-company"
  | "taxis-per-department"
  | "taxis-inservice-per-department"
  | "logs-requisitions-status"
  | "financial";

export type TaxiReportPageProps = {
  searchParams: SearchParams;
  kind?: TaxiReportKind;
  routePath?: string;
};

function ReportSearch({
  kind,
  query,
}: Readonly<{ kind: TaxiReportKind; query: Record<string, string | string[] | undefined> }>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <input type="hidden" name="mode" value={kind} />
      <div className="vehicle-search-row">
        <label className="form-label" htmlFor="taxi-report-search">
          {kind === "one-taxi-number" ? "Registration, GG, or requisition" : "Search report"}
        </label>
        <input
          className="vehicle-search"
          id="taxi-report-search"
          name="search"
          defaultValue={queryValue(query.search)}
          placeholder="Optional search"
        />
        <button className="button button-primary" type="submit">
          Run report
        </button>
      </div>
    </form>
  );
}

function TaxiRows({ rows }: Readonly<{ rows: TaxiRecord[] }>) {
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Taxi report results</caption>
        <DataTableHeader
          columns={[
            { key: "column-1", label: <>Requisition</> },
            { key: "column-2", label: <>Date required</> },
            { key: "column-3", label: <>Official</> },
            { key: "column-4", label: <>GG / vehicle</> },
            { key: "column-5", label: <>Registration</> },
            { key: "column-6", label: <>Contractor</> },
            { key: "column-7", label: <>Department</> },
          ]}
        />
        <tbody>
          {rows.length === 0 ? (
            <tr>
              <td colSpan={7}>No records found.</td>
            </tr>
          ) : (
            rows.map((taxi) => (
              <tr key={taxi.requestId}>
                <td>
                  <Link href={`/taxis/requests?mode=edit&requestId=${taxi.requestId}`}>
                    {valueOrDash(taxi.rekNum)}
                  </Link>
                </td>
                <td>{dateValue(taxi.dateRequired)}</td>
                <td>{valueOrDash(taxi.official)}</td>
                <td>{valueOrDash(taxi.vmfCode)}</td>
                <td>{valueOrDash(taxi.regNum)}</td>
                <td>{valueOrDash(taxi.contractorId)}</td>
                <td>{valueOrDash(taxi.departmentName ?? taxi.departmentCode)}</td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}

function LegacyReportGrid({
  report,
  pageHref,
}: Readonly<{ report: LegacyReport; pageHref: (page: number) => string }>) {
  return (
    <>
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2>
            {report.totalCount} record{report.totalCount === 1 ? "" : "s"}
          </h2>
        </div>
        <ReportPrintButton />
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">{report.title}</caption>
          <thead>
            <tr>
              {report.columns.map((column) => (
                <th scope="col" key={column.key}>
                  {column.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {report.rows.length === 0 ? (
              <tr>
                <td colSpan={Math.max(1, report.columns.length)}>No records found.</td>
              </tr>
            ) : (
              report.rows.map((row, index) => (
                <tr key={`${report.reportKey}-${index}`}>
                  {report.columns.map((column) => (
                    <td key={column.key}>{valueOrDash(row[column.key])}</td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
      {report.isApproximate && report.approximationReason ? (
        <p className="muted-copy">Report note: {report.approximationReason}</p>
      ) : null}
      <ReportPagination report={report} pageHref={pageHref} label="Taxi report pages" />
    </>
  );
}

function reportTitle(kind: TaxiReportKind) {
  return {
    "one-taxi-number": "Report On One Taxi Number",
    "logs-per-user": "Number Of Taxi Logs Captured Per User For Date",
    "old-requisitions": "Old Requisitions For Period",
    "taxis-per-company": "Taxis Per Hire Company",
    "taxis-per-department": "List Of All Taxis In Various Departments",
    "taxis-inservice-per-department": "List Of All Taxis In Service In Various Departments",
    "logs-requisitions-status": "Taxi Logs and Requisitions Status Report",
    financial: "Financial Reports: Taxis",
  }[kind];
}

function reportKey(kind: TaxiReportKind) {
  if (kind === "taxis-per-department") return "taxis-list-per-department";
  if (kind === "taxis-inservice-per-department") return "taxis-list-inservice-per-department";
  return "taxis-financial";
}

const TaxiReportsPageContent = renderTaxiReportsPageContent;

async function renderTaxiReportsPageContent({
  searchParams,
  kind: forcedKind,
  routePath: requestedRoutePath,
}: TaxiReportPageProps) {
  await connection();
  const session = await getSession();
  const routePath =
    requestedRoutePath ?? (forcedKind ? `/taxis/reports/${forcedKind}` : "/taxis/reports");
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (!hasTaxiAccess(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <TaxiRestricted subject="Taxi reports" />
      </main>
    );

  const query = await searchParams;
  const kind = forcedKind ?? ((queryValue(query.mode) as TaxiReportKind) || "one-taxi-number");
  const search = queryValue(query.search).trim();
  const parsedPage = Number(queryValue(query.page));
  const page = Number.isSafeInteger(parsedPage) && parsedPage > 0 ? parsedPage : 1;
  try {
    if (kind === "one-taxi-number") {
      const taxiPage = search ? await getTaxiPage({ page, search }) : null;
      const rows = taxiPage?.items ?? [];
      return (
        <main className="page-shell vehicle-page-shell">
          <section className="vehicle-card">
            <TaxiHeader
              title={reportTitle(kind)}
              description="Enter the registration number, GG number, or requisition to view a taxi report."
            />
            <TaxiNotice query={query} />
            <ReportSearch kind={kind} query={query} />
            <section className="vehicle-status-maintenance-panel report-print-area">
              <div className="vehicle-form-section-header">
                <div>
                  <p className="eyebrow">Report results</p>
                  <h2>
                    {taxiPage?.total ?? 0} record{(taxiPage?.total ?? 0) === 1 ? "" : "s"}
                  </h2>
                </div>
                <div className="report-print-hide">
                  <ReportPrintButton />
                </div>
              </div>
              <TaxiRows rows={rows} />
              {taxiPage ? (
                <TaxiPagination
                  path={routePath}
                  query={query}
                  page={taxiPage.page}
                  totalPages={taxiPage.totalPages}
                />
              ) : null}
            </section>
          </section>
        </main>
      );
    }

    const report = await getLegacyReport(reportKey(kind), search ? { search } : {}, { page });
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card">
          <TaxiHeader
            title={reportTitle(kind)}
            description="Run the legacy-compatible taxi report with the selected search filter."
          />
          <TaxiNotice query={query} />
          <ReportSearch kind={kind} query={query} />
          <section className="vehicle-status-maintenance-panel report-print-area">
            <LegacyReportGrid
              report={report}
              pageHref={(requestedPage) => taxiPageHref(routePath, query, requestedPage)}
            />
          </section>
        </section>
      </main>
    );
  } catch (error) {
    if (
      (error instanceof TaxiApiError || error instanceof LegacyReportApiError) &&
      error.reason === "unauthorized"
    )
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <TaxiUnavailable path={routePath} subject="Taxi reports" />
      </main>
    );
  }
}

export default function TaxiReportsPage(props: TaxiReportPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <TaxiReportsPageContent {...props} />
    </Suspense>
  );
}
