import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import RouteLoading from "@/components/app-shell/route-loading";
import {
  ClearanceApiError,
  DEFAULT_CLEARANCE_REPORT_PAGE_SIZE,
  getClearanceUniversalReport,
  getMerchants,
  type ClearanceReportPage,
  type MerchantRecord,
} from "@/lib/api/fleet-operations/api-clearance";
import { getSession } from "@/lib/auth/session";

const REPORTS_ROLE = "Reports";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function validDate(value: string) {
  return /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : "";
}

function parseMerchantCode(value: string) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function parsePage(value: string) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : 1;
}

function reportPageHref(
  routePath: string,
  startDate: string,
  endDate: string,
  merchantCode: number | null,
  page: number,
) {
  const params = new URLSearchParams();
  if (startDate) params.set("startDate", startDate);
  if (endDate) params.set("endDate", endDate);
  if (merchantCode !== null) params.set("merchantCode", String(merchantCode));
  if (page > 1) params.set("page", String(page));
  const query = params.toString();
  return `${routePath}${query ? `?${query}` : ""}`;
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to run Clearance reports.</h2>
    </section>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>Clearance report data could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href="/clearance/reports/universal">
        Try again
      </Link>
    </section>
  );
}

function ReportPagination({
  report,
  pageHref,
}: Readonly<{
  report: Pick<ClearanceReportPage, "page" | "totalPages">;
  pageHref: (page: number) => string;
}>) {
  if (report.totalPages <= 1) return null;

  return (
    <nav className="vehicle-pagination" aria-label="Clearance report pages">
      {report.page > 1 ? (
        <Link className="vehicle-pagination-button" href={pageHref(report.page - 1)}>
          Previous
        </Link>
      ) : (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Previous
        </span>
      )}
      <span className="vehicle-pagination-meta" aria-live="polite">
        Page {report.page} of {report.totalPages}
      </span>
      {report.page < report.totalPages ? (
        <Link className="vehicle-pagination-button" href={pageHref(report.page + 1)}>
          Next
        </Link>
      ) : (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Next
        </span>
      )}
    </nav>
  );
}

function ReportResults({
  report,
  pageHref,
}: Readonly<{
  report: ClearanceReportPage;
  pageHref: (page: number) => string;
}>) {
  const rows = report.items;
  const firstRow = report.total === 0 ? 0 : (report.page - 1) * report.pageSize + 1;
  const lastRow = Math.min(report.page * report.pageSize, report.total);

  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="clearance-results-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">
            {report.total} record{report.total === 1 ? "" : "s"}
          </p>
          <h2 id="clearance-results-title">Clearance records</h2>
        </div>
      </div>
      <p className="muted-copy" aria-live="polite">
        {report.total === 0
          ? "0 records"
          : `Showing ${firstRow}–${lastRow} of ${report.total} records · ${report.pageSize} rows per page`}
      </p>
      {rows.length === 0 ? (
        <p className="muted-copy">No clearance records matched the selected filters.</p>
      ) : (
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">Universal clearance report</caption>
            <DataTableHeader
              columns={[
                { key: "column-1", label: <>Fleet Number</> },
                { key: "column-2", label: <>Clearance Comment</> },
                { key: "column-3", label: <>Merchant Name</> },
                { key: "column-4", label: <>Clearance Number</> },
                { key: "column-5", label: <>Clearance Date</> },
              ]}
            />
            <tbody>
              {rows.map((row) => (
                <tr
                  key={
                    row.clearanceCode ??
                    `${row.fleetNumber ?? "row"}-${row.clearanceNumber ?? "number"}-${row.clearanceDate ?? "date"}`
                  }
                >
                  <td>{valueOrDash(row.fleetNumber)}</td>
                  <td>{valueOrDash(row.clearanceComment)}</td>
                  <td>{valueOrDash(row.merchantName)}</td>
                  <td>{valueOrDash(row.clearanceNumber)}</td>
                  <td>{formatDate(row.clearanceDate)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <ReportPagination report={report} pageHref={pageHref} />
    </section>
  );
}

const ClearanceUniversalReportContent = renderClearanceUniversalReportContent;

async function renderClearanceUniversalReportContent({
  searchParams,
  routePath = "/clearance/reports/universal",
}: Readonly<{ searchParams: SearchParams; routePath?: string }>) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <SessionRecovery returnPath={routePath} />;
  if (session.status === "unavailable") return <ApiUnavailable />;
  if (!hasRole(session.roles, REPORTS_ROLE)) return <AccessRestricted />;

  const query = await searchParams;
  const startDate = validDate(
    (getQueryValue(query.startDate) ?? getQueryValue(query.Start_date) ?? "").trim(),
  );
  const endDate = validDate(
    (getQueryValue(query.endDate) ?? getQueryValue(query.End_date) ?? "").trim(),
  );
  const merchantCode = parseMerchantCode(
    (getQueryValue(query.merchantCode) ?? getQueryValue(query.cmbMerchant) ?? "").trim(),
  );
  const requestedPage = parsePage((getQueryValue(query.page) ?? "").trim());
  const submitted = Boolean(startDate || endDate || merchantCode);

  let merchants: MerchantRecord[] = [];
  let report: ClearanceReportPage | null = null;
  let errorMessage: string | null = null;
  try {
    merchants = await getMerchants();
    if (submitted) {
      if (startDate && endDate && startDate > endDate) {
        errorMessage = "The report start date must be before the end date.";
      } else {
        report = await getClearanceUniversalReport({
          startDate,
          endDate,
          merchantCode: merchantCode ?? undefined,
          page: requestedPage,
          pageSize: DEFAULT_CLEARANCE_REPORT_PAGE_SIZE,
        });
      }
    }
  } catch (error) {
    if (error instanceof ClearanceApiError && error.reason === "unauthorized")
      return <SessionRecovery returnPath={routePath} />;
    console.error(
      "FIS clearance universal report failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return <ApiUnavailable />;
  }

  return (
    <>
      <form className="vehicle-status-maintenance-panel" method="get">
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="clearance-report-start">
              Start Date
            </label>
            <input
              className="form-input"
              id="clearance-report-start"
              name="startDate"
              type="date"
              defaultValue={startDate}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="clearance-report-end">
              End Date
            </label>
            <input
              className="form-input"
              id="clearance-report-end"
              name="endDate"
              type="date"
              defaultValue={endDate}
            />
          </div>
          <div className="form-field form-group-full">
            <label className="form-label" htmlFor="clearance-report-merchant">
              Merchant
            </label>
            <select
              className="form-select"
              id="clearance-report-merchant"
              name="merchantCode"
              defaultValue={merchantCode ?? "0"}
            >
              <option value="0">All merchants</option>
              {merchants.map((merchant) => (
                <option key={merchant.merchantCode} value={merchant.merchantCode}>
                  {valueOrDash(merchant.merchantName)}
                </option>
              ))}
            </select>
          </div>
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            Submit
          </button>
          <Link className="button button-secondary" href="/clearance/reports">
            Report Menu
          </Link>
        </div>
      </form>
      {errorMessage ? (
        <div className="notice notice-error" role="alert">
          {errorMessage}
        </div>
      ) : null}
      {submitted && !errorMessage && report ? (
        <ReportResults
          report={report}
          pageHref={(page) => reportPageHref(routePath, startDate, endDate, merchantCode, page)}
        />
      ) : null}
    </>
  );
}

export default function ClearanceUniversalReportPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="clearance-universal-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Clearance reports</p>
            <h1 id="clearance-universal-title">Clearance Universal Report</h1>
            <p>Filter clearance records by date and merchant.</p>
          </div>
          <Link className="button button-secondary" href="/clearance/reports">
            Report Menu
          </Link>
        </header>
        <Suspense fallback={<RouteLoading />}>
          <ClearanceUniversalReportContent searchParams={searchParams} />
        </Suspense>
      </section>
    </main>
  );
}
