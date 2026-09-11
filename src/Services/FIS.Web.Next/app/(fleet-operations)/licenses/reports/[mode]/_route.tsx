import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";
import ReportResultsPanel from "@/components/ui/report-results-panel";
import ReportRowsTable from "@/components/ui/report-rows-table";

import Link from "next/link";
import { notFound } from "next/navigation";

import { LicenseShell } from "@/app/(fleet-operations)/licenses/_components";
import { valueOrDash } from "@/app/(fleet-operations)/licenses/_utils";
import {
  accessRestricted,
  getLicenseSession,
  hasLicenseAccess,
  queryValue,
  sessionMessage,
} from "@/app/(fleet-operations)/licenses/_page";
import { getDepartments, DepartmentApiError } from "@/lib/api/reference-data/api-departments";
import {
  getLicenseReport,
  LICENSE_REPORT_MODES,
  LicenseReportApiError,
  type LicenseReportMode,
} from "@/lib/api/reports/api-license-reports";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export type LicenseReportPageProps = {
  params: Promise<{ mode: string }>;
  searchParams: SearchParams;
  forcedMode?: LicenseReportMode;
  routePath?: string;
};

const TITLES: Record<LicenseReportMode, string> = {
  "gg-number": "Licence Report for a GG Number",
  "gp-number": "Licence Report for a Prov Reg Number",
  "register-number": "Licence Report for a Register Number",
  "engine-number": "Licence Report for an Engine Number",
  "chassis-number": "Licence Report for a Chassis Number",
  site: "Licence Report for a Site",
  all: "Licence Report on ALL Vehicles",
  "dept-period": "Licences, for a Dept, with All Sites, for a period",
  "expire-date": "Report on Licence EXPIRE DATE",
  "month-fees": "Licences fees for a month",
  "old-expire": "Licences of In Service Vehicles, with OLD Expire Dates",
  sap: "SAP Information",
  cof: "COF Information",
  "model-fees": "table : Make & Model with Licence Fee",
  "gg-model-fees": "All Vehicles with Make & Model & Tare & Licence Fee",
  workgroup: "Data Workgroup - Report for Each Or Group of Vehicle",
  "workgroup-latest": "Data Workgroup - Only Latest Report for Each Or Group of Vehicle",
  "ggmt-received": "Licence Received by GGMT : Receiver Name and Date",
};

const IDENTIFIER_MODES = new Set<LicenseReportMode>([
  "gg-number",
  "gp-number",
  "register-number",
  "engine-number",
  "chassis-number",
  "sap",
  "cof",
  "ggmt-received",
]);

function reportMode(
  mode: LicenseReportMode,
): "GG" | "GP" | "REGISTER" | "ENGINE" | "CHASSIS" | undefined {
  const modes = {
    "gg-number": "GG",
    "gp-number": "GP",
    "register-number": "REGISTER",
    "engine-number": "ENGINE",
    "chassis-number": "CHASSIS",
  } as const;
  return modes[mode as keyof typeof modes];
}

function errorCard(message: string) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>{message}</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
    </section>
  );
}

const ReportForm = renderReportForm;

function renderReportForm({
  mode,
  query,
  departments,
}: Readonly<{
  mode: LicenseReportMode;
  query: Record<string, string>;
  departments: Awaited<ReturnType<typeof getDepartments>>;
}>) {
  const identifier = IDENTIFIER_MODES.has(mode);
  const needsDates = mode === "dept-period" || mode === "expire-date";
  const needsMonth = mode === "month-fees";
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <input name="run" type="hidden" value="1" />
      {identifier ? (
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="license-report-search">
              {mode === "gp-number"
                ? "Prov Reg Number"
                : mode === "register-number"
                  ? "Register Number"
                  : mode === "engine-number"
                    ? "Engine Number"
                    : mode === "chassis-number"
                      ? "Chassis Number"
                      : "GG Number"}
            </label>
            <input
              className="form-input"
              id="license-report-search"
              name="search"
              defaultValue={query.search}
              maxLength={50}
              required
            />
          </div>
        </div>
      ) : null}
      {mode === "site" ? (
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="license-report-location">
              Garage
            </label>
            <select
              className="form-select"
              id="license-report-location"
              name="location"
              defaultValue={query.location}
            >
              <option value="">All garages</option>
              <option value="jhb">Johannesburg</option>
              <option value="pta">Pretoria</option>
            </select>
          </div>
        </div>
      ) : null}
      {mode === "all" || mode === "old-expire" ? (
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="license-report-status">
              Vehicle status
            </label>
            <select
              className="form-select"
              id="license-report-status"
              name="status"
              defaultValue={mode === "old-expire" ? "inservice" : query.status}
            >
              <option value="">All vehicles</option>
              <option value="inservice">In Service</option>
              <option value="notinservice">Not In Service</option>
            </select>
          </div>
        </div>
      ) : null}
      {mode === "dept-period" ? (
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="license-report-department">
              Department
            </label>
            <select
              className="form-select"
              id="license-report-department"
              name="departmentCode"
              defaultValue={query.departmentCode}
            >
              <option value="">All Departments</option>
              {departments
                .toSorted((a, b) => (a.description ?? "").localeCompare(b.description ?? ""))
                .map((department) => (
                  <option
                    key={department.departmentCode}
                    value={department.departmentNumber ?? department.departmentCode}
                  >
                    {valueOrDash(department.description)} (
                    {department.departmentNumber ?? department.departmentCode})
                  </option>
                ))}
            </select>
          </div>
        </div>
      ) : null}
      {needsDates ? (
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="license-report-from">
              {mode === "expire-date" ? "Expire Begin Date" : "Begin Date"}
            </label>
            <input
              className="form-input"
              id="license-report-from"
              name="from"
              type="date"
              defaultValue={query.from}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="license-report-to">
              {mode === "expire-date" ? "Expire End Date" : "End Date"}
            </label>
            <input
              className="form-input"
              id="license-report-to"
              name="to"
              type="date"
              defaultValue={query.to}
            />
          </div>
        </div>
      ) : null}
      {mode === "old-expire" ? (
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="license-report-cutoff">
              Expire Date Cutoff
            </label>
            <input
              className="form-input"
              id="license-report-cutoff"
              name="to"
              type="date"
              defaultValue={query.to}
            />
          </div>
        </div>
      ) : null}
      {needsMonth ? (
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="license-report-month">
              Month
            </label>
            <input
              className="form-input"
              id="license-report-month"
              name="month"
              type="number"
              min="1"
              max="12"
              defaultValue={query.month}
              required
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="license-report-year">
              Year
            </label>
            <input
              className="form-input"
              id="license-report-year"
              name="year"
              type="number"
              min="2000"
              max="2100"
              defaultValue={query.year}
              required
            />
          </div>
        </div>
      ) : null}
      {mode === "workgroup" || mode === "workgroup-latest" ? (
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="license-report-workgroup">
              Group or Vehicle
            </label>
            <input
              className="form-input"
              id="license-report-workgroup"
              name="search"
              defaultValue={query.search}
            />
          </div>
        </div>
      ) : null}
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Generate report
        </button>
        <Link className="button button-secondary" href="/licenses/reports">
          Report Menu
        </Link>
      </div>
    </form>
  );
}

function Results({ report }: Readonly<{ report: Awaited<ReturnType<typeof getLicenseReport>> }>) {
  if (report.rows.length === 0)
    return (
      <section className="vehicle-empty-state" aria-live="polite">
        <p className="eyebrow">No records found</p>
        <h2>No licences matched the selected filters.</h2>
        <p className="muted-copy">Adjust the report parameters and try again.</p>
      </section>
    );
  return (
    <ReportResultsPanel
      headingId="license-report-results-title"
      heading={`${report.rows.length} record(s) returned`}
    >
      <ReportRowsTable
        columns={report.columns.map((column) => ({ key: column, header: column }))}
        rows={report.rows}
        caption="Licence report results"
      />
    </ReportResultsPanel>
  );
}

const LicenseReportPageContent = renderLicenseReportPageContent;

async function renderLicenseReportPageContent({
  params,
  searchParams,
  forcedMode,
  routePath = "/licenses/reports",
}: LicenseReportPageProps) {
  const session = await getLicenseSession();
  const problem = sessionMessage(session, routePath);
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasLicenseAccess(session)) return accessRestricted();
  const modeValue = (forcedMode ?? (await params).mode.toLowerCase()) as LicenseReportMode;
  if (!LICENSE_REPORT_MODES.includes(modeValue)) notFound();
  const raw = await searchParams;
  const query = Object.fromEntries(
    Object.entries(raw).map(([key, value]) => [key, queryValue(value)]),
  );
  const run = query.run === "1";
  let departments: Awaited<ReturnType<typeof getDepartments>> = [];
  let report: Awaited<ReturnType<typeof getLicenseReport>> | null = null;
  let loadError: string | null = null;
  try {
    if (modeValue === "dept-period") departments = await getDepartments();
    if (run) {
      report = await getLicenseReport(modeValue, {
        search: query.search || undefined,
        searchMode: reportMode(modeValue),
        departmentCode: query.departmentCode || undefined,
        location: query.location === "jhb" || query.location === "pta" ? query.location : undefined,
        status:
          query.status === "inservice" || query.status === "notinservice"
            ? query.status
            : undefined,
        from: query.from || undefined,
        to: query.to || undefined,
        month: Number(query.month) || undefined,
        year: Number(query.year) || undefined,
      });
    }
  } catch (error) {
    if (error instanceof LicenseReportApiError && error.reason === "unauthorized")
      return (
        <LicenseShell title={TITLES[modeValue]} description="Licence report parameters and output.">
          {errorCard("Your session has expired. Sign in again before continuing.")}
        </LicenseShell>
      );
    loadError =
      error instanceof DepartmentApiError
        ? "Department data could not be loaded."
        : "Licence report data could not be loaded.";
    console.error(
      "FIS licence report request failed",
      error instanceof Error ? error.message : "unknown error",
    );
  }
  return (
    <LicenseShell title={TITLES[modeValue]} description="Licence report parameters and output.">
      <ReportForm mode={modeValue} query={query} departments={departments} />
      {loadError ? (
        <div className="notice notice-error" role="alert">
          {loadError}
        </div>
      ) : null}
      {report ? (
        <Results report={report} />
      ) : (
        <section className="vehicle-status-card">
          <p className="eyebrow">Parameters required</p>
          <h2>Enter the report parameters and generate the report.</h2>
        </section>
      )}
      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/licenses/reports">
          Report Menu
        </Link>
        <Link className="button button-secondary" href="/licenses">
          Licence Menu
        </Link>
      </div>
    </LicenseShell>
  );
}

export function LicenseReportPage(props: LicenseReportPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LicenseReportPageContent {...props} />
    </Suspense>
  );
}

export default LicenseReportPage;
