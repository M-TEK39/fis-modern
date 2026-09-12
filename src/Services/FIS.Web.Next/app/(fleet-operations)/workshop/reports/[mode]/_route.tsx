import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";
import ReportResultsPanel from "@/components/ui/report-results-panel";
import ReportRowsTable from "@/components/ui/report-rows-table";
import SearchTypeFieldset from "@/components/ui/search-type-fieldset";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  getLegacyReport,
  LegacyReportApiError,
  type LegacyReport,
} from "@/lib/api/reports/api-legacy-reports";
import { getSession } from "@/lib/auth/session";
import { workshopReportMode, type WorkshopReportMode } from "./_utils";
type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export type WorkshopReportPageProps = {
  mode: WorkshopReportMode;
  searchParams: SearchParams;
  routePath?: string;
  menuHref?: string;
};

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function validDate(value: string | undefined) {
  return value && /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : "";
}

function parsePositiveInt(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasWorkshopReportAccess(roles: readonly string[]) {
  return roles.some((role) =>
    ["Workshop", "Reports"].some(
      (expected) => role.localeCompare(expected, undefined, { sensitivity: "accent" }) === 0,
    ),
  );
}

function reportTitle(mode: WorkshopReportMode) {
  return {
    "one-vehicle": "Workshop Report on One Vehicle",
    "print-job-card": "Print a Workshop Job Card",
    period: "Workshop Report for a Period",
    "in-workshop": "List of Vehicles Still in Workshop",
    merchants: "List of All Merchants",
  }[mode];
}

function reportKey(mode: WorkshopReportMode) {
  return {
    "one-vehicle": "workshop-one-vehicle",
    "print-job-card": "workshop-print-job-card",
    period: "workshop",
    "in-workshop": "workshop-in-shop",
    merchants: "workshop-merchants",
  }[mode];
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function ReportForm({
  mode,
  query,
}: Readonly<{ mode: WorkshopReportMode; query: Record<string, string> }>) {
  if (mode === "in-workshop" || mode === "merchants") return null;

  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <input name="run" type="hidden" value="1" />
      {mode === "one-vehicle" || mode === "print-job-card" ? (
        <div className="form-grid">
          <SearchTypeFieldset
            selectedType={query.searchMode}
            legend="Find vehicle by"
            name="searchMode"
          />
          <div className="form-field">
            <label className="form-label" htmlFor="workshop-report-vehicle">
              Vehicle number
            </label>
            <input
              className="form-input"
              id="workshop-report-vehicle"
              name="vehicleNumber"
              defaultValue={query.vehicleNumber}
              maxLength={30}
              placeholder={query.searchMode === "GP" ? "GP number" : "GG number"}
              required
            />
          </div>
          {mode === "print-job-card" ? (
            <div className="form-field">
              <label className="form-label" htmlFor="workshop-report-code">
                Job card number (optional)
              </label>
              <input
                className="form-input"
                id="workshop-report-code"
                name="workshopCode"
                defaultValue={query.workshopCode}
                inputMode="numeric"
              />
            </div>
          ) : null}
        </div>
      ) : null}
      {mode === "period" ? (
        <div className="form-grid">
          <fieldset className="vehicle-search-options">
            <legend>Garage</legend>
            <label className="vehicle-checkbox-label">
              <input
                type="radio"
                name="garage"
                value="Radioall"
                defaultChecked={!query.garage || query.garage === "Radioall"}
              />{" "}
              All
            </label>
            <label className="vehicle-checkbox-label">
              <input
                type="radio"
                name="garage"
                value="Radiojhb"
                defaultChecked={query.garage === "Radiojhb"}
              />{" "}
              JHB
            </label>
            <label className="vehicle-checkbox-label">
              <input
                type="radio"
                name="garage"
                value="Radiopta"
                defaultChecked={query.garage === "Radiopta"}
              />{" "}
              PTA
            </label>
          </fieldset>
          <fieldset className="vehicle-search-options">
            <legend>Entry type</legend>
            <label className="vehicle-checkbox-label">
              <input
                type="radio"
                name="category"
                value="Radioall"
                defaultChecked={!query.category || query.category === "Radioall"}
              />{" "}
              All
            </label>
            <label className="vehicle-checkbox-label">
              <input
                type="radio"
                name="category"
                value="Radioacc"
                defaultChecked={query.category === "Radioacc"}
              />{" "}
              Accident
            </label>
            <label className="vehicle-checkbox-label">
              <input
                type="radio"
                name="category"
                value="Radiomec"
                defaultChecked={query.category === "Radiomec"}
              />{" "}
              Mechanical
            </label>
          </fieldset>
          <div className="form-field">
            <label className="form-label" htmlFor="workshop-report-from">
              Begin Date
            </label>
            <input
              className="form-input"
              id="workshop-report-from"
              name="from"
              type="date"
              defaultValue={query.from}
              required
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="workshop-report-to">
              End Date
            </label>
            <input
              className="form-input"
              id="workshop-report-to"
              name="to"
              type="date"
              defaultValue={query.to}
              required
            />
          </div>
        </div>
      ) : null}
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Generate report
        </button>
        <Link className="button button-secondary" href="/reports/workshop">
          Reports menu
        </Link>
      </div>
    </form>
  );
}

function ReportResults({
  report,
  mode,
}: Readonly<{ report: LegacyReport; mode: WorkshopReportMode }>) {
  if (report.rows.length === 0)
    return (
      <section className="vehicle-empty-state" aria-live="polite">
        <p className="eyebrow">No records found</p>
        <h2>No Workshop records matched the selected filters.</h2>
        <p className="muted-copy">Adjust the report parameters and try again.</p>
      </section>
    );

  return (
    <ReportResultsPanel
      headingId="workshop-report-results-title"
      heading={`${report.totalCount} record(s) returned`}
      trailing={<span className="form-hint">Compatibility result</span>}
      letterheadTitle={mode === "print-job-card" ? "WORKSHOP JOB CARD — RECEPTION" : undefined}
    >
      {report.approximationReason ? (
        <div className="notice notice-info" role="status">
          {report.approximationReason}
        </div>
      ) : null}
      <ReportRowsTable columns={report.columns} rows={report.rows} caption={report.title} />
    </ReportResultsPanel>
  );
}

function ApiUnavailable({ routePath }: Readonly<{ routePath: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h1>Workshop report data could not be loaded.</h1>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href={routePath}>
        Try again
      </Link>
    </section>
  );
}

const WorkshopReportPageContent = renderWorkshopReportPageContent;

async function renderWorkshopReportPageContent({
  mode,
  searchParams,
  routePath = `/workshop/reports/${mode}`,
  menuHref = "/reports/workshop",
}: WorkshopReportPageProps) {
  await connection();
  const session = await getSession();
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
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  if (!hasWorkshopReportAccess(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h1>You do not have permission to run Workshop reports.</h1>
        </section>
      </main>
    );

  const rawQuery = await searchParams;
  const query = Object.fromEntries(
    Object.entries(rawQuery).map(([key, value]) => [key, queryValue(value) ?? ""]),
  );
  const searchMode = query.searchMode === "GP" || query.Radio1 === "Radiogp" ? "GP" : "GG";
  const vehicleNumber = (query.vehicleNumber || query.xnum || query.txtGGNum || "")
    .trim()
    .slice(0, 30);
  const workshopCode = parsePositiveInt(query.workshopCode || query.wwCode || query.wwcod);
  const from = validDate(query.from || query.BDAT);
  const to = validDate(query.to || query.EDAT);
  const garage = query.garage || query.Radio1 || "Radioall";
  const category = query.category || query.Radio2 || "Radioall";
  const run = query.run === "1" || mode === "in-workshop" || mode === "merchants";
  let report: LegacyReport | null = null;
  let errorMessage: string | null = null;

  try {
    if (run) {
      if ((mode === "one-vehicle" || mode === "print-job-card") && !vehicleNumber && !workshopCode)
        errorMessage = "Enter a vehicle number or job-card number before generating this report.";
      else if (mode === "period" && (!from || !to))
        errorMessage = "Enter both a begin date and an end date before generating this report.";
      else if (mode === "period" && from > to)
        errorMessage = "The begin date must be before the end date.";
      else
        report = await getLegacyReport(reportKey(mode), {
          searchMode,
          vehicleNumber: vehicleNumber || undefined,
          workshopCode: workshopCode ?? undefined,
          from: from || undefined,
          to: to || undefined,
          garage,
          category,
        });
    }
  } catch (error) {
    if (error instanceof LegacyReportApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS Workshop report request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="workshop-report-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Workshop reports</p>
            <h1 id="workshop-report-title">{reportTitle(mode)}</h1>
            <p>Run the legacy report against the current compatible Workshop data.</p>
          </div>
          <Link className="button button-secondary" href={menuHref}>
            Reports menu
          </Link>
        </header>
        <ReportForm
          mode={mode}
          query={{
            ...query,
            searchMode,
            vehicleNumber,
            workshopCode: workshopCode ? String(workshopCode) : "",
            from,
            to,
            garage,
            category,
          }}
        />
        {errorMessage ? (
          <div className="notice notice-error" role="alert">
            {errorMessage}
          </div>
        ) : null}
        {report ? (
          <ReportResults mode={mode} report={report} />
        ) : mode === "in-workshop" || mode === "merchants" ? (
          <section className="vehicle-status-card">
            <p className="eyebrow">No records found</p>
            <h2>No Workshop records were returned.</h2>
          </section>
        ) : (
          <section className="vehicle-status-card">
            <p className="eyebrow">Parameters required</p>
            <h2>Enter the report parameters and generate the report.</h2>
          </section>
        )}
      </section>
    </main>
  );
}

export function WorkshopReportPage(props: WorkshopReportPageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <WorkshopReportPageContent {...props} />
    </Suspense>
  );
}

async function WorkshopReportRouteContent({
  params,
  searchParams,
}: Readonly<{ params: Promise<{ mode: string }>; searchParams: SearchParams }>) {
  const { mode } = await params;
  return <WorkshopReportPageContent mode={workshopReportMode(mode)} searchParams={searchParams} />;
}

export default function WorkshopReportRoute(
  props: Parameters<typeof WorkshopReportRouteContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <WorkshopReportRouteContent {...props} />
    </Suspense>
  );
}
