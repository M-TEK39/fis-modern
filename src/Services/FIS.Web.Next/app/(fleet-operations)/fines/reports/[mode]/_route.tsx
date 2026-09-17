import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { ReportPagination } from "@/app/(fleet-operations)/reports/_components";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import GovernmentReportLetterhead from "@/components/ui/government-report-letterhead";
import ReportPrintButton from "@/components/ui/report-print-button";
import SearchTypeFieldset from "@/components/ui/search-type-fieldset";
import {
  FineApiError,
  getFineReport,
  getFineSites,
  searchFineVehicles,
  type FineReport,
  type FineReportMode,
  type FineSearchType,
  type FineSite,
  type FineVehicleOption,
} from "@/lib/api/fleet-operations/api-fines";
import { getSession } from "@/lib/auth/session";
import { hasFinesAccess } from "@/app/(fleet-operations)/fines/access";
type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export type FineReportPageProps = {
  searchParams: SearchParams;
  forcedMode?: string | Promise<string>;
  routePath?: string | Promise<string>;
  legacyResult?: boolean;
};

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positiveInt(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function reportPageHref(
  routePath: string,
  query: Record<string, string | string[] | undefined>,
  page: number,
) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (["page", "pageSize", "includeAll"].includes(key)) continue;
    const text = Array.isArray(value) ? value[0] : value;
    if (text?.trim()) params.set(key, text);
  }
  params.set("page", String(page));
  return `${routePath}?${params.toString()}`;
}

function validDate(value: string) {
  return /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : "";
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function reportRowKey(row: Record<string, string | null>, fallback: string) {
  return row.__rowKey ?? row["Fine Code"] ?? row["Offence Date"] ?? fallback;
}

function normalizeMode(value: string | undefined): string {
  return value?.trim().toLowerCase() ?? "";
}

function apiMode(value: string): FineReportMode | "select" | "" {
  const mapped = {
    "one-vehicle": "one-vehicle",
    "appear-date": "appear-date",
    letter: "reissue-submission",
    "reissue-submission": "reissue-submission",
    "fine-detail": "fine-detail",
    "traffic-dept": "traffic-dept-detail",
    "traffic-dept-detail": "traffic-dept-detail",
    "dept-period": "dept-site-period",
    "dept-site-period": "dept-site-period",
    vehicle: "vehicle-period",
    "vehicle-period": "vehicle-period",
    metro: "metro-period",
    "metro-period": "metro-period",
    all: "all",
    select: "select",
  }[value] as FineReportMode | "select" | undefined;
  return mapped ?? "";
}

function vehicleSearchType(value: string | undefined): FineSearchType {
  return value === "GG" || value === "Radiogg" ? "GG" : "GP";
}

function VehicleFields({
  searchType,
  searchQuery,
  vehicles,
  selectedVmfCode,
  includeDates,
  startDate,
  endDate,
}: Readonly<{
  searchType: FineSearchType;
  searchQuery: string;
  vehicles: FineVehicleOption[];
  selectedVmfCode: number | null;
  includeDates: boolean;
  startDate: string;
  endDate: string;
}>) {
  return (
    <>
      <SearchTypeFieldset selectedType={searchType} legend="Find vehicle by" firstOption="GP" />
      <div className="form-field">
        <label className="form-label" htmlFor="fine-report-vehicle-search">
          {searchType === "GG" ? "GG Number" : "GP Number"}
        </label>
        <input
          className="form-input"
          id="fine-report-vehicle-search"
          maxLength={8}
          name="searchQuery"
          defaultValue={searchQuery}
          placeholder={searchType === "GG" ? "GG number" : "Current or historical GP number"}
        />
      </div>
      <div className="form-field">
        <label className="form-label" htmlFor="fine-report-vehicle">
          Vehicle
        </label>
        <select
          className="form-select"
          id="fine-report-vehicle"
          name="vmfCode"
          defaultValue={selectedVmfCode ?? ""}
        >
          <option value="">Select vehicle...</option>
          {vehicles.map((vehicle) => (
            <option key={vehicle.vmfCode} value={vehicle.vmfCode}>
              {valueOrDash(vehicle.fleetNumber)} / {valueOrDash(vehicle.registrationNumber)} (
              {vehicle.vmfCode})
              {vehicle.isHistoricalMatch && vehicle.matchedRegistration
                ? ` — historical GP ${vehicle.matchedRegistration}`
                : ""}
            </option>
          ))}
        </select>
        {vehicles.length === 0 && searchQuery ? (
          <p className="form-hint">
            No exact vehicle match was found. Search by the other identifier.
          </p>
        ) : null}
      </div>
      {includeDates ? (
        <>
          <div className="form-field">
            <label className="form-label" htmlFor="fine-report-start">
              Begin Date
            </label>
            <input
              className="form-input"
              id="fine-report-start"
              name="startDate"
              type="date"
              defaultValue={startDate}
              required
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="fine-report-end">
              End Date
            </label>
            <input
              className="form-input"
              id="fine-report-end"
              name="endDate"
              type="date"
              defaultValue={endDate}
              required
            />
          </div>
        </>
      ) : null}
    </>
  );
}

function ReportForm({
  mode,
  searchType,
  searchQuery,
  vehicles,
  selectedVmfCode,
  sites,
  startDate,
  endDate,
  siteCode,
  issuer,
}: Readonly<{
  mode: string;
  searchType: FineSearchType;
  searchQuery: string;
  vehicles: FineVehicleOption[];
  selectedVmfCode: number | null;
  sites: FineSite[];
  startDate: string;
  endDate: string;
  siteCode: number | null;
  issuer: string;
}>) {
  if (mode === "traffic-dept-detail") return null;

  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <input name="run" type="hidden" value="1" />
      {mode === "one-vehicle" || mode === "reissue-submission" || mode === "vehicle-period" ? (
        <div className="form-grid">
          <VehicleFields
            searchType={searchType}
            searchQuery={searchQuery}
            vehicles={vehicles}
            selectedVmfCode={selectedVmfCode}
            includeDates={mode === "vehicle-period"}
            startDate={startDate}
            endDate={endDate}
          />
        </div>
      ) : null}
      {mode === "appear-date" ? (
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="fine-report-appear-date">
              Appear Date
            </label>
            <input
              className="form-input"
              id="fine-report-appear-date"
              name="appearDate"
              type="date"
              defaultValue={startDate}
              required
            />
          </div>
        </div>
      ) : null}
      {mode === "dept-site-period" ? (
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="fine-report-site">
              Dept / Site
            </label>
            <select
              className="form-select"
              id="fine-report-site"
              name="siteCode"
              defaultValue={siteCode ?? ""}
            >
              <option value="">All sites</option>
              {sites.map((site) => (
                <option key={site.siteCode} value={site.siteCode}>
                  {valueOrDash(site.departmentNumber)} / {valueOrDash(site.description)} (
                  {site.siteCode})
                </option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="fine-report-department">
              Department number contains
            </label>
            <input
              className="form-input"
              id="fine-report-department"
              name="department"
              maxLength={30}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="fine-report-dept-start">
              Begin Date
            </label>
            <input
              className="form-input"
              id="fine-report-dept-start"
              name="startDate"
              type="date"
              defaultValue={startDate}
              required
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="fine-report-dept-end">
              End Date
            </label>
            <input
              className="form-input"
              id="fine-report-dept-end"
              name="endDate"
              type="date"
              defaultValue={endDate}
              required
            />
          </div>
        </div>
      ) : null}
      {mode === "metro-period" ? (
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="fine-report-issuer">
              Offence Issuer (Metro)
            </label>
            <input
              className="form-input"
              id="fine-report-issuer"
              name="issuer"
              defaultValue={issuer}
              maxLength={100}
              required
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="fine-report-metro-start">
              Begin Date
            </label>
            <input
              className="form-input"
              id="fine-report-metro-start"
              name="startDate"
              type="date"
              defaultValue={startDate}
              required
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="fine-report-metro-end">
              End Date
            </label>
            <input
              className="form-input"
              id="fine-report-metro-end"
              name="endDate"
              type="date"
              defaultValue={endDate}
              required
            />
          </div>
        </div>
      ) : null}
      {mode === "all" ? (
        <p className="muted-copy">Run the report to list all legacy Fines records.</p>
      ) : null}
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Submit
        </button>
        <Link className="button button-secondary" href="/fines/reports">
          Reports Menu
        </Link>
      </div>
    </form>
  );
}

function ReportResults({
  report,
  detail,
  previewPath,
  officialLetter,
  pageHref,
}: Readonly<{
  report: FineReport;
  detail: boolean;
  previewPath?: string;
  officialLetter: boolean;
  pageHref: (page: number) => string;
}>) {
  if (report.rows.length === 0)
    return (
      <section className="vehicle-empty-state" aria-live="polite">
        <p className="eyebrow">No records found</p>
        <h2>No fines matched the selected filters.</h2>
        <p className="muted-copy">Try a different vehicle, date range, department, or issuer.</p>
        <ReportPagination report={report} pageHref={pageHref} label="Fines report pages" />
      </section>
    );

  if (detail) {
    return (
      <section
        className={`vehicle-status-maintenance-panel report-print-area${officialLetter ? " fine-reissue-letter" : ""}`}
        aria-labelledby="fine-detail-results-title"
      >
        {officialLetter ? (
          <GovernmentReportLetterhead
            governmentMotorTransport
            title="SUBMISSION TO RE-ISSUE THE TRAFFIC FINE/SUMMONS"
          />
        ) : null}
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Report results</p>
            <h2 id="fine-detail-results-title">{report.title}</h2>
          </div>
          <ReportPrintButton />
        </div>
        {report.rows.map((row) => (
          <article className="vehicle-status-maintenance-panel" key={reportRowKey(row, "fine")}>
            {officialLetter ? (
              <p className="fine-reissue-letter-introduction">
                With due respect, please replace the named person on the traffic fine or summons
                record with the transport officer allocated to the vehicle at the time of the
                offence.
              </p>
            ) : null}
            <h3>Fine {valueOrDash(row["Fine Code"])}</h3>
            <div className="vehicle-table-wrapper">
              <table className="vehicle-table">
                <caption className="sr-only">Fine detail</caption>
                <tbody>
                  {report.columns.map((column) => (
                    <tr key={column.key}>
                      <th scope="row">{column.header}</th>
                      <td>{valueOrDash(row[column.key])}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </article>
        ))}
        <ReportPagination report={report} pageHref={pageHref} label="Fines report pages" />
      </section>
    );
  }

  return (
    <section
      className="vehicle-status-maintenance-panel report-print-area"
      aria-labelledby="fine-report-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="fine-report-results-title">{report.title}</h2>
        </div>
        <div className="button-row">
          <span className="form-hint">{report.totalCount} record(s)</span>
          <ReportPrintButton />
        </div>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">{report.title}</caption>
          <thead>
            <tr>
              {report.columns.map((column) => (
                <th key={column.key} scope="col">
                  {column.header}
                </th>
              ))}
              {previewPath ? (
                <th className="report-print-hide" scope="col">
                  Action
                </th>
              ) : null}
            </tr>
          </thead>
          <tbody>
            {report.rows.map((row) => (
              <tr key={reportRowKey(row, "row")}>
                {report.columns.map((column) => (
                  <td key={column.key}>{valueOrDash(row[column.key])}</td>
                ))}
                {previewPath ? (
                  <td className="report-print-hide">
                    <Link
                      className="button button-secondary button-small"
                      href={`${previewPath}?run=1&fineCode=${encodeURIComponent(row["Fine Code"] ?? "")}`}
                    >
                      Show
                    </Link>
                  </td>
                ) : null}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <ReportPagination report={report} pageHref={pageHref} label="Fines report pages" />
    </section>
  );
}

function ApiUnavailable({ path }: Readonly<{ path: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>Fines report data could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href={path}>
        Try again
      </Link>
    </section>
  );
}

const FineReportPageContent = renderFineReportPageContent;

async function renderFineReportPageContent({
  searchParams,
  forcedMode,
  routePath: routePathValue = "/fines/reports",
  legacyResult = false,
}: FineReportPageProps) {
  await connection();
  const [routePath, session] = await Promise.all([Promise.resolve(routePathValue), getSession()]);
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
        <ApiUnavailable path={routePath} />
      </main>
    );
  if (!hasFinesAccess(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to run Fines reports.</h2>
        </section>
      </main>
    );

  const query = await searchParams;
  const requestedMode = forcedMode === undefined ? queryValue(query.mode) : await forcedMode;
  const mode = apiMode(normalizeMode(requestedMode));
  if (mode === "" || mode === "select") notFound();

  const searchType = vehicleSearchType(queryValue(query.searchType) ?? queryValue(query.Radio1));
  const searchQuery = (
    queryValue(query.searchQuery) ??
    queryValue(query.xnumber) ??
    queryValue(query.txtGGNum) ??
    queryValue(query.code) ??
    ""
  )
    .trim()
    .slice(0, 100);
  const selectedVmfCode = positiveInt(queryValue(query.vmfCode) ?? queryValue(query.vmf));
  const fineCode = positiveInt(queryValue(query.fineCode) ?? queryValue(query.FCode));
  const startDate = validDate(
    (queryValue(query.startDate) ?? queryValue(query.BDAT) ?? queryValue(query.xdat) ?? "").trim(),
  );
  const endDate = validDate((queryValue(query.endDate) ?? queryValue(query.EDAT) ?? "").trim());
  const siteCode = positiveInt(queryValue(query.siteCode) ?? queryValue(query.cmb_site));
  const issuer = (
    queryValue(query.issuer) ??
    queryValue(query.Code) ??
    queryValue(query.Traf_name) ??
    ""
  )
    .trim()
    .slice(0, 100);
  const page = positiveInt(queryValue(query.page)) ?? 1;
  const shouldRun = queryValue(query.run) === "1" || legacyResult;

  let vehicles: FineVehicleOption[] = [];
  let sites: FineSite[] = [];
  let report: FineReport | null = null;
  let errorMessage: string | null = null;
  let resolvedVmfCode = selectedVmfCode;

  try {
    if (
      searchQuery &&
      (mode === "one-vehicle" || mode === "reissue-submission" || mode === "vehicle-period")
    ) {
      vehicles = await searchFineVehicles(searchType, searchQuery);
      if (!resolvedVmfCode && vehicles.length === 1) resolvedVmfCode = vehicles[0].vmfCode;
    }
    if (mode === "dept-site-period") sites = await getFineSites();

    if (shouldRun) {
      if ((mode === "one-vehicle" || mode === "vehicle-period") && !resolvedVmfCode)
        errorMessage = "Select a vehicle before running this report.";
      else if (mode === "appear-date" && !startDate)
        errorMessage = "Enter an appear date before running this report.";
      else if (
        (mode === "dept-site-period" || mode === "vehicle-period" || mode === "metro-period") &&
        (!startDate || !endDate)
      )
        errorMessage = "Enter both a begin date and an end date before running this report.";
      else if (
        (mode === "dept-site-period" || mode === "vehicle-period" || mode === "metro-period") &&
        startDate > endDate
      )
        errorMessage = "The report begin date must be before the end date.";
      else if (mode === "metro-period" && !issuer)
        errorMessage = "Enter an offence issuer before running this report.";
      else if (mode === "reissue-submission" && !resolvedVmfCode && !fineCode)
        errorMessage = "Select a vehicle before running this report.";
      else {
        const reportMode = fineCode && mode === "reissue-submission" ? "fine-detail" : mode;
        report = await getFineReport(
          reportMode,
          {
            vmf: resolvedVmfCode ?? undefined,
            fineCode: fineCode ?? undefined,
            appearDate: startDate || undefined,
            from: startDate || undefined,
            to: endDate || undefined,
            site: siteCode ?? undefined,
            issuer: issuer || undefined,
            department: queryValue(query.department)?.trim() || undefined,
          },
          { page },
        );
      }
    } else if (mode === "traffic-dept-detail") {
      report = await getFineReport("traffic-dept-detail", {}, { page });
    }
  } catch (error) {
    if (error instanceof FineApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS fines report request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable path={routePath} />
      </main>
    );
  }

  const detail =
    mode === "one-vehicle" ||
    mode === "appear-date" ||
    mode === "fine-detail" ||
    (mode === "reissue-submission" && fineCode !== null);
  const title =
    report?.title ??
    (
      {
        "one-vehicle": "Fines Report on ONE Vehicle",
        "appear-date": "Due Date To Appear In Court Report",
        "reissue-submission": "Submission to Re-Issue a Traffic Fine",
        "traffic-dept-detail": "Traffic Dept Information",
        "dept-site-period": "Fines Report for a Department (or Site)",
        "vehicle-period": "Fines Report per Vehicle",
        "metro-period": "Fines Report per Metro",
        all: "A List of all Fines",
        "fine-detail": "Fine Detail",
      } as Record<string, string>
    )[mode] ??
    "Fines Report";

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="fine-report-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Fines reports</p>
            <h1 id="fine-report-title">{title}</h1>
            <p>Legacy Fines report filters and fields, rendered from the current database.</p>
          </div>
          <Link className="button button-secondary" href="/fines/reports">
            Reports Menu
          </Link>
        </header>
        <ReportForm
          mode={mode}
          searchType={searchType}
          searchQuery={searchQuery}
          vehicles={vehicles}
          selectedVmfCode={resolvedVmfCode}
          sites={sites}
          startDate={startDate}
          endDate={endDate}
          siteCode={siteCode}
          issuer={issuer}
        />
        {errorMessage ? (
          <div className="notice notice-error" role="alert">
            {errorMessage}
          </div>
        ) : null}
        {report ? (
          <ReportResults
            report={report}
            detail={detail}
            previewPath={mode === "reissue-submission" ? routePath : undefined}
            officialLetter={legacyResult || (mode === "reissue-submission" && fineCode !== null)}
            pageHref={(requestedPage) => reportPageHref(routePath, query, requestedPage)}
          />
        ) : null}
        <div className="vehicle-footer-actions">
          <Link
            className="button button-secondary"
            href={
              mode === "dept-site-period" ||
              mode === "vehicle-period" ||
              mode === "metro-period" ||
              mode === "all"
                ? "/fines/reports/select"
                : "/fines/reports"
            }
          >
            Back
          </Link>
        </div>
      </section>
    </main>
  );
}

export function FineReportPage(props: FineReportPageProps) {
  return (
    <StreamedRoute>
      <FineReportPageContent {...props} />
    </StreamedRoute>
  );
}

export function FineReportRoute({
  params,
  searchParams,
}: {
  params: Promise<{ mode: string }>;
  searchParams: SearchParams;
}) {
  const mode = params.then(({ mode: routeMode }) => routeMode);
  return (
    <FineReportPage
      forcedMode={mode}
      routePath={mode.then((routeMode) => `/fines/reports/${routeMode}`)}
      searchParams={searchParams}
    />
  );
}

export default FineReportRoute;
