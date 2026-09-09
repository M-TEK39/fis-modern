import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import {
  FinanceFrame,
  FinanceRestricted,
  FinanceUnavailable,
  hasFinanceRole,
} from "@/app/finance/_components";
import { departmentOptions, siteOptions } from "@/app/finance/_location-options";
import { DepartmentApiError, getDepartments } from "@/lib/api-departments";
import {
  FinanceApiError,
  getFinancePostingMonths,
  getFinanceProvinces,
  getFinanceYears,
  type FinanceOption,
  type FinanceRow,
} from "@/lib/api-finance";
import { SiteApiError, getSites } from "@/lib/api-sites";
import {
  getBillingHistory,
  getDedicatedFinanceReport,
  getDedicatedFinanceReportData,
  getReversalTree,
  getUniversalFinanceReport,
  FINANCE_REPORT_ACTIONS,
  type FinanceReport,
} from "@/lib/api-finance-reports";
import { getSession } from "@/lib/session";

type Query = Record<string, string | string[] | undefined>;
type ReportPageProps = Readonly<{
  params: Promise<{ action: string }>;
  searchParams: Promise<Query>;
}>;

const UNIVERSAL_ACTIONS = ["financial-year", "date-range", "journal"] as const;
const REPORT_ACTIONS = [...UNIVERSAL_ACTIONS, ...FINANCE_REPORT_ACTIONS] as readonly string[];

function queryValue(query: Query, name: string) {
  const value = query[name];
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

function positiveInteger(value: string) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : undefined;
}

function titleFor(action: string) {
  return (
    (
      {
        department: "Print Financial Reports by Department",
        province: "Print Financial Reports by Province",
        site: "Print Financial Reports by Site",
        "vehicle-billing-history": "Vehicle Billing History Report by Financial Year",
        "reversals-tree": "Reversals Tree Report from Journal Number",
        "income-department": "Income Report by Department",
        "income-split-summary": "Summary Income Split Report",
        "income-split-detailed": "Detailed Income Split Report",
        "income-department-site": "Income Report by Department and Site",
        "download-income-department": "Download Income Report by Department",
        "download-income-department-site": "Download Income Report by Department and Site",
        "download-income-department-site-vehicle":
          "Download Income Report by Department, Site and Vehicle",
      } as Record<string, string>
    )[action] ?? "Financial Reports"
  );
}

function descriptionFor(action: string) {
  if (action === "vehicle-billing-history")
    return "Review a vehicle billing history for a financial year.";
  if (action === "reversals-tree") return "Trace reversal journals from a journal number.";
  if (action.startsWith("income") || action.startsWith("download-income"))
    return "Generate income reports with the existing Finance filters.";
  return "Financial reporting and invoice output.";
}

function formatFor(reportAction: string) {
  if (reportAction.endsWith("-excel")) return "csv";
  if (reportAction === "detailed-table") return "json";
  return "html";
}

function rowValue(row: FinanceRow, ...names: string[]) {
  const expected = names.map((name) => name.toLowerCase());
  const entry = Object.entries(row).find(([key]) => expected.includes(key.toLowerCase()));
  if (!entry || entry[1] === null || entry[1] === undefined || entry[1] === "") return "-";
  return String(entry[1]);
}

function formValue(query: Query, name: string) {
  return queryValue(query, name);
}

function optionList(options: FinanceOption[], emptyLabel: string) {
  return (
    <>
      <option value="">{emptyLabel}</option>
      {options.map((item) => (
        <option key={item.value} value={item.value}>
          {item.label}
        </option>
      ))}
    </>
  );
}

function reportButton(reportAction: string, label: string, className = "button button-secondary") {
  return (
    <button className={className} name="reportAction" type="submit" value={reportAction}>
      {label}
    </button>
  );
}

function ReportForm({
  action,
  query,
  departments,
  sites,
  provinces,
  years,
  postingMonths,
}: Readonly<{
  action: string;
  query: Query;
  departments: ReturnType<typeof departmentOptions>;
  sites: ReturnType<typeof siteOptions>;
  provinces: FinanceOption[];
  years: FinanceOption[];
  postingMonths: FinanceOption[];
}>) {
  const mode = action === "site" || action === "province" ? action : "department";
  const dateRange =
    action === "income-department" ||
    action === "income-department-site" ||
    action.startsWith("download-income");
  const financialYear =
    action === "vehicle-billing-history" ||
    action === "income-split-summary" ||
    action === "income-split-detailed";
  const journal = action === "reversals-tree";
  const dedicated = action === "department" || action === "site";
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <input name="run" type="hidden" value="1" />
      <div className="form-grid">
        {financialYear ? (
          <div className="form-field">
            <label className="form-label" htmlFor="finance-report-year">
              Financial Year
            </label>
            <select
              className="form-select"
              id="finance-report-year"
              name="financialYear"
              defaultValue={formValue(query, "financialYear")}
              required
            >
              {optionList(years, "Select Financial Year")}
            </select>
          </div>
        ) : null}
        {journal ? (
          <div className="form-field">
            <label className="form-label" htmlFor="finance-report-journal">
              Reversal Journal Number
            </label>
            <input
              className="form-input"
              id="finance-report-journal"
              name="journalNumber"
              defaultValue={formValue(query, "journalNumber")}
              inputMode="numeric"
              required
            />
          </div>
        ) : null}
        {dateRange ? (
          <>
            <div className="form-field">
              <label className="form-label" htmlFor="finance-report-from">
                Start Date
              </label>
              <input
                className="form-input"
                id="finance-report-from"
                name="startDate"
                type="date"
                defaultValue={formValue(query, "startDate")}
                required
              />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="finance-report-to">
                End Date
              </label>
              <input
                className="form-input"
                id="finance-report-to"
                name="endDate"
                type="date"
                defaultValue={formValue(query, "endDate")}
                required
              />
            </div>
          </>
        ) : null}
        {action === "vehicle-billing-history" ? (
          <div className="form-field">
            <label className="form-label" htmlFor="finance-report-vmf">
              Vehicle VMF Code
            </label>
            <input
              className="form-input"
              id="finance-report-vmf"
              name="vmfCode"
              defaultValue={formValue(query, "vmfCode")}
              inputMode="numeric"
              required
            />
          </div>
        ) : null}
        {!journal && !financialYear && !dateRange ? (
          <div className="form-field">
            <label className="form-label" htmlFor="finance-report-department">
              Department
            </label>
            <select
              className="form-select"
              id="finance-report-department"
              name="departmentCode"
              defaultValue={formValue(query, "departmentCode")}
              required={mode !== "province"}
            >
              {optionList(departments, "Select Department")}
            </select>
          </div>
        ) : null}
        {mode === "site" ? (
          <div className="form-field">
            <label className="form-label" htmlFor="finance-report-site">
              Site
            </label>
            <select
              className="form-select"
              id="finance-report-site"
              name="siteCode"
              defaultValue={formValue(query, "siteCode")}
              required
            >
              {optionList(sites, "Select Site")}
            </select>
          </div>
        ) : null}
        {mode === "province" ? (
          <div className="form-field">
            <label className="form-label" htmlFor="finance-report-province">
              Province
            </label>
            <select
              className="form-select"
              id="finance-report-province"
              name="province"
              defaultValue={formValue(query, "province")}
              required
            >
              {optionList(provinces, "Select Province")}
            </select>
          </div>
        ) : null}
        {dedicated ? (
          <div className="form-field">
            <label className="form-label" htmlFor="finance-report-posting-month">
              Posting Month
            </label>
            <select
              className="form-select"
              id="finance-report-posting-month"
              name="batchDate"
              defaultValue={formValue(query, "batchDate") || postingMonths[0]?.value || ""}
              required
            >
              {optionList(postingMonths, "Select Posting Month")}
            </select>
          </div>
        ) : null}
      </div>
      <div className="button-row">
        {journal ? (
          reportButton("journal", "Submit Details", "button button-primary")
        ) : action === "vehicle-billing-history" ? (
          reportButton("financial-year", "Submit", "button button-primary")
        ) : dateRange ? (
          reportButton("date-range", "Submit", "button button-primary")
        ) : financialYear ? (
          reportButton("financial-year", "Submit", "button button-primary")
        ) : (
          <>
            {reportButton(
              "summary-by-cost-type",
              "Show Summarised Invoice",
              "button button-primary",
            )}
            {reportButton(
              "summary-by-cost-type-journal",
              "Show Invoice by Cost Type, Site and Journal",
            )}
            {reportButton("summary-html", "Show HTML Invoice")}
            {reportButton("summary-pdf", "Show PDF Invoice")}
            {reportButton("detailed-html", "Show Detailed HTML Invoice")}
            {reportButton("detailed-pdf", "Show Detailed PDF Invoice")}
            {reportButton("detailed-table", "Show Invoice in a Table")}
            {reportButton("detailed-excel", "Download Excel Invoice")}
            {reportButton("detailed-vip-taxi-pdf", "TAXI and VIP PDF")}
            {reportButton("detailed-fuel-pdf", "Detailed Fuel Invoice")}
            {reportButton("detailed-fuel-excel", "Download Fuel Invoice")}
            {reportButton("detailed-toll-oil-pdf", "Detailed Toll and Oil Invoice")}
            {reportButton("detailed-toll-oil-excel", "Download Toll and Oil Invoice")}
            {reportButton("detailed-surcharge-pdf", "Show Surcharge Invoice")}
            {reportButton("detailed-surcharge-excel", "Download Surcharge Invoice")}
          </>
        )}
        <Link className="button button-secondary" href="/finance">
          Back
        </Link>
      </div>
    </form>
  );
}

function outputHref(
  reportAction: string,
  action: string,
  query: Query,
  id: number,
  postingMonthCode: number,
  filterBy: "Department" | "Site",
) {
  const params = new URLSearchParams({
    kind: "dedicated",
    reportAction,
    action,
    id: String(id),
    postingMonthCode: String(postingMonthCode),
    filterBy,
    format: formatFor(reportAction),
  });
  return `/finance/reports/output?${params.toString()}`;
}

function universalOutputHref(action: string, reportAction: string, query: Query) {
  const params = new URLSearchParams({
    kind:
      action === "reversals-tree"
        ? "reversal"
        : action === "vehicle-billing-history"
          ? "billing"
          : "universal",
    reportAction,
    action,
    format: reportAction.endsWith("excel")
      ? "excel"
      : reportAction.endsWith("pdf")
        ? "html"
        : "html",
  });
  for (const name of [
    "departmentCode",
    "siteCode",
    "province",
    "financialYear",
    "batchDate",
    "vmfCode",
    "startDate",
    "endDate",
    "journalNumber",
  ]) {
    const value = queryValue(query, name);
    if (value) params.set(name, value);
  }
  return `/finance/reports/output?${params.toString()}`;
}

function ReportTable({ report, page }: Readonly<{ report: FinanceReport; page: number }>) {
  const pageSize = 12;
  const rows = report.rows.slice((page - 1) * pageSize, page * pageSize);
  const columns = report.rows.length > 0 ? Object.keys(report.rows[0]) : [];
  if (report.rows.length === 0)
    return (
      <div className="vehicle-empty-state">
        <p>No report rows found for the selected parameters.</p>
      </div>
    );
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="finance-report-results">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{report.rows.length} record(s)</p>
          <h2 id="finance-report-results">{report.title}</h2>
        </div>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">{report.title}</caption>
          <thead>
            <tr>
              {columns.map((column) => (
                <th key={column} scope="col">
                  {column.replaceAll("_", " ")}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row, index) => (
              <tr key={`${report.title}-${index}`}>
                {columns.map((column) => (
                  <td key={column}>{rowValue(row, column)}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {report.rows.length > pageSize ? (
        <p className="muted-copy">
          Showing page {page} of {Math.ceil(report.rows.length / pageSize)}.
        </p>
      ) : null}
    </section>
  );
}

export async function FinanceReportsRoute({
  action,
  searchParams,
}: Readonly<{ action: string; searchParams: Promise<Query> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    return (
      <FinanceFrame title={titleFor(action)} description={descriptionFor(action)}>
        <FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." />
      </FinanceFrame>
    );
  if (!hasFinanceRole(session.roles))
    return (
      <FinanceFrame title={titleFor(action)} description={descriptionFor(action)}>
        <FinanceRestricted />
      </FinanceFrame>
    );

  const query = await searchParams;
  const normalizedAction = action.trim().toLowerCase();
  let departments: ReturnType<typeof departmentOptions> = [];
  let sites: ReturnType<typeof siteOptions> = [];
  let provinces: FinanceOption[] = [];
  let years: FinanceOption[] = [];
  let postingMonths: FinanceOption[] = [];
  let lookupError: string | null = null;
  try {
    const [departmentRecords, siteRecords, loadedProvinces, loadedYears] = await Promise.all([
      getDepartments(),
      getSites(),
      getFinanceProvinces(),
      getFinanceYears(),
    ]);
    departments = departmentOptions(departmentRecords);
    sites = siteOptions(siteRecords);
    provinces = loadedProvinces;
    years = loadedYears;
    if (normalizedAction === "department" || normalizedAction === "site")
      postingMonths = await getFinancePostingMonths(
        normalizedAction === "site" ? "Site" : "Department",
      );
  } catch (error) {
    if (
      error instanceof FinanceApiError ||
      error instanceof DepartmentApiError ||
      error instanceof SiteApiError
    )
      lookupError = error.message;
    else throw error;
  }

  const reportAction = queryValue(query, "reportAction").toLowerCase();
  const submitted = queryValue(query, "run") === "1";
  let report: FinanceReport | null = null;
  let output: { href: string; label: string } | null = null;
  let error: string | null = lookupError;
  if (submitted) {
    if (!REPORT_ACTIONS.includes(reportAction)) error = "Select a valid Finance report action.";
    else {
      try {
        const dedicated = getDedicatedFinanceReport(reportAction);
        const id = positiveInteger(
          normalizedAction === "site"
            ? queryValue(query, "siteCode")
            : queryValue(query, "departmentCode"),
        );
        const postingMonthCode = positiveInteger(queryValue(query, "batchDate"));
        if (dedicated && (normalizedAction === "department" || normalizedAction === "site")) {
          if (!id)
            error =
              normalizedAction === "site"
                ? "Select a site before running the report."
                : "Select a department before running the report.";
          else if (!postingMonthCode) error = "Select a posting month before running the report.";
          else if (dedicated.defaultFormat === "json")
            report = await getDedicatedFinanceReportData(reportAction, {
              id,
              postingMonthCode,
              filterBy: normalizedAction === "site" ? "Site" : "Department",
            });
          else
            output = {
              href: outputHref(
                reportAction,
                normalizedAction,
                query,
                id,
                postingMonthCode,
                normalizedAction === "site" ? "Site" : "Department",
              ),
              label:
                formatFor(reportAction) === "csv" ? "Download report" : "Open printable report",
            };
        } else if (normalizedAction === "vehicle-billing-history") {
          const vmfCode = positiveInteger(queryValue(query, "vmfCode"));
          const financialYear = positiveInteger(queryValue(query, "financialYear"));
          if (!vmfCode || !financialYear)
            error = "Select a vehicle VMF code and financial year before running the report.";
          else report = await getBillingHistory(vmfCode, financialYear);
        } else if (normalizedAction === "reversals-tree") {
          const journalNumber = queryValue(query, "journalNumber");
          if (!journalNumber) error = "Enter a reversal journal number before running the report.";
          else report = await getReversalTree(journalNumber);
        } else if (
          normalizedAction === "income-department" ||
          normalizedAction === "income-department-site" ||
          normalizedAction.startsWith("download-income") ||
          normalizedAction === "department" ||
          normalizedAction === "site" ||
          normalizedAction === "province"
        ) {
          report = await getUniversalFinanceReport({
            mode: normalizedAction,
            action: reportAction,
            departmentCode: queryValue(query, "departmentCode"),
            siteCode: queryValue(query, "siteCode"),
            province: queryValue(query, "province"),
            financialYear: queryValue(query, "financialYear"),
            batchDate: queryValue(query, "batchDate"),
            vmfCode: positiveInteger(queryValue(query, "vmfCode")),
            startDate: queryValue(query, "startDate"),
            endDate: queryValue(query, "endDate"),
          });
          if (reportAction.endsWith("-pdf") || reportAction.endsWith("-excel"))
            output = {
              href: universalOutputHref(normalizedAction, reportAction, query),
              label: reportAction.endsWith("excel") ? "Download report" : "Open printable report",
            };
        } else {
          report = await getUniversalFinanceReport({
            mode: normalizedAction,
            action: reportAction,
            financialYear: queryValue(query, "financialYear"),
            startDate: queryValue(query, "startDate"),
            endDate: queryValue(query, "endDate"),
          });
        }
      } catch (caught) {
        error =
          caught instanceof FinanceApiError
            ? caught.message
            : "The Finance report could not be generated.";
      }
    }
  }

  const page = positiveInteger(queryValue(query, "page")) ?? 1;
  return (
    <FinanceFrame title={titleFor(normalizedAction)} description={descriptionFor(normalizedAction)}>
      {error ? (
        <div className="notice notice-error" role="alert">
          {error}
        </div>
      ) : null}
      <ReportForm
        action={normalizedAction}
        query={query}
        departments={departments}
        sites={sites}
        provinces={provinces}
        years={years}
        postingMonths={postingMonths}
      />
      {output ? (
        <section
          className="vehicle-status-maintenance-panel"
          aria-labelledby="finance-report-output"
        >
          <h2 id="finance-report-output">Report ready</h2>
          <p className="muted-copy">The report is generated on the authenticated server path.</p>
          <a className="button button-primary" href={output.href} target="_blank" rel="noreferrer">
            {output.label}
          </a>
        </section>
      ) : null}
      {report ? <ReportTable report={report} page={page} /> : null}
    </FinanceFrame>
  );
}

export default async function FinanceReportsPage({ params, searchParams }: ReportPageProps) {
  const { action } = await params;
  return <FinanceReportsRoute action={action} searchParams={searchParams} />;
}
