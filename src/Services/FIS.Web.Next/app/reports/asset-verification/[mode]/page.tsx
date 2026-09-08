import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { getLegacyReport, LegacyReportApiError, type LegacyReport } from "@/lib/api-legacy-reports";
import { getSites, type SiteRecord } from "@/lib/api-sites";
import { getSession } from "@/lib/session";

const REPORT_MODES = ["per-site-province-date", "not-verified", "verified-by-date-range"] as const;
type AssetVerificationReportMode = (typeof REPORT_MODES)[number];
type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export type AssetVerificationReportPageProps = {
  mode: AssetVerificationReportMode;
  searchParams: SearchParams;
  routePath?: string;
  menuHref?: string;
};

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function parseSiteCode(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function validDate(value: string | undefined) {
  return value && /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : "";
}

function hasReportsRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare("Reports", undefined, { sensitivity: "accent" }) === 0,
  );
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function reportTitle(mode: AssetVerificationReportMode) {
  return {
    "per-site-province-date": "Report Per Site / Province / Verification Date",
    "not-verified": "Vehicles Not Verified",
    "verified-by-date-range": "Vehicles Verified By Date Range - Excel Report",
  }[mode];
}

function reportKey(mode: AssetVerificationReportMode) {
  return `asset-verification-${mode}`;
}

function ReportForm({
  mode,
  query,
  sites,
}: Readonly<{
  mode: AssetVerificationReportMode;
  query: Record<string, string>;
  sites: SiteRecord[];
}>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <input name="run" type="hidden" value="1" />
      {mode === "per-site-province-date" ? (
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="asset-report-site">
              Site
            </label>
            <select
              className="form-select"
              id="asset-report-site"
              name="site"
              defaultValue={query.site}
            >
              <option value="">All sites</option>
              {sites.map((site) => (
                <option key={site.siteCode} value={site.siteCode}>
                  {valueOrDash(site.departmentNumber)} - {valueOrDash(site.description)} (
                  {site.siteCode})
                </option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="asset-report-province">
              Province
            </label>
            <select
              className="form-select"
              id="asset-report-province"
              name="province"
              defaultValue={query.province}
            >
              <option value="">All provinces</option>
              {[
                "Eastern Cape",
                "Free State",
                "Gauteng",
                "Kwazulu Natal",
                "Limpopo",
                "Mpumalanga",
                "Northern Cape",
                "North West",
                "Western Cape",
              ].map((province) => (
                <option key={province} value={province}>
                  {province}
                </option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="asset-report-from">
              Verification date from
            </label>
            <input
              className="form-input"
              id="asset-report-from"
              name="from"
              type="date"
              defaultValue={query.from}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="asset-report-to">
              Verification date to
            </label>
            <input
              className="form-input"
              id="asset-report-to"
              name="to"
              type="date"
              defaultValue={query.to}
            />
          </div>
        </div>
      ) : mode === "verified-by-date-range" ? (
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="asset-report-date-from">
              From <span className="required">*</span>
            </label>
            <input
              className="form-input"
              id="asset-report-date-from"
              name="from"
              type="date"
              defaultValue={query.from}
              required
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="asset-report-date-to">
              To <span className="required">*</span>
            </label>
            <input
              className="form-input"
              id="asset-report-date-to"
              name="to"
              type="date"
              defaultValue={query.to}
              required
            />
          </div>
        </div>
      ) : (
        <p className="muted-copy">
          This report lists active vehicles with current contracts that do not have an asset
          verification record.
        </p>
      )}
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Generate report
        </button>
        <Link className="button button-secondary" href="/reports/asset-verification">
          Reports menu
        </Link>
      </div>
    </form>
  );
}

function ReportResults({ report }: Readonly<{ report: LegacyReport }>) {
  if (report.rows.length === 0) {
    return (
      <section className="vehicle-empty-state" aria-live="polite">
        <p className="eyebrow">No records found</p>
        <h2>No vehicles matched the selected report.</h2>
        <p className="muted-copy">Adjust the report parameters and try again.</p>
      </section>
    );
  }

  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="asset-report-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="asset-report-results-title">{report.totalCount} record(s) returned</h2>
        </div>
        <span className="form-hint">
          {report.isApproximate ? "Compatibility result" : "Legacy-aligned result"}
        </span>
      </div>
      {report.isApproximate && report.approximationReason ? (
        <div className="notice notice-info" role="status">
          {report.approximationReason}
        </div>
      ) : null}
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
            </tr>
          </thead>
          <tbody>
            {report.rows.map((row, index) => (
              <tr key={`${row[report.columns[0]?.key] ?? "row"}-${index}`}>
                {report.columns.map((column) => (
                  <td key={column.key}>{valueOrDash(row[column.key])}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function ApiUnavailable({ routePath }: Readonly<{ routePath: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h1>Asset Verification report data could not be loaded.</h1>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href={routePath}>
        Try again
      </Link>
    </section>
  );
}

export async function AssetVerificationReportPage({
  mode,
  searchParams,
  routePath = `/reports/asset-verification/${mode}`,
  menuHref = "/reports/asset-verification",
}: AssetVerificationReportPageProps) {
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
  if (!hasReportsRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h1>You do not have permission to run Asset Verification reports.</h1>
        </section>
      </main>
    );

  const rawQuery = await searchParams;
  const query = Object.fromEntries(
    Object.entries(rawQuery).map(([key, value]) => [key, queryValue(value) ?? ""]),
  );
  const site = parseSiteCode(query.site || query.cmbDeptNumber);
  const province = (query.province || query.cmbDeptName).trim();
  const from = validDate(query.from || query.sverdate);
  const to = validDate(query.to || query.everdate);
  const run = query.run === "1";
  let sites: SiteRecord[] = [];
  let report: LegacyReport | null = null;
  let errorMessage: string | null = null;

  try {
    if (mode === "per-site-province-date") {
      sites = (await getSites())
        .filter((candidate) => candidate.siteActive)
        .toSorted((left, right) =>
          (left.departmentNumber ?? left.description ?? "").localeCompare(
            right.departmentNumber ?? right.description ?? "",
          ),
        );
    }

    if (run) {
      if (mode === "verified-by-date-range" && (!from || !to))
        errorMessage = "Enter both a start date and an end date before generating this report.";
      else if (mode !== "not-verified" && from && to && from > to)
        errorMessage = "The start date must be before the end date.";
      else if (mode === "per-site-province-date" && !site && !province && !from && !to)
        errorMessage =
          "Choose a site, province, or verification date range before generating this report.";
      else {
        report = await getLegacyReport(reportKey(mode), {
          site: site ?? undefined,
          province: province || undefined,
          from: from || undefined,
          to: to || undefined,
        });
      }
    }
  } catch (error) {
    if (error instanceof LegacyReportApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS asset verification report request failed",
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
      <section className="vehicle-card" aria-labelledby="asset-report-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Vehicle asset verification reports</p>
            <h1 id="asset-report-title">{reportTitle(mode)}</h1>
            <p>Run the legacy report against the current compatible verification data.</p>
          </div>
          <Link className="button button-secondary" href={menuHref}>
            Reports menu
          </Link>
        </header>
        <ReportForm
          mode={mode}
          query={{ ...query, site: site ? String(site) : "", province, from, to }}
          sites={sites}
        />
        {errorMessage ? (
          <div className="notice notice-error" role="alert">
            {errorMessage}
          </div>
        ) : null}
        {report ? (
          <ReportResults report={report} />
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

export function isAssetVerificationReportMode(value: string): value is AssetVerificationReportMode {
  return REPORT_MODES.includes(value as AssetVerificationReportMode);
}

export function assetVerificationReportMode(value: string): AssetVerificationReportMode {
  if (!isAssetVerificationReportMode(value)) notFound();
  return value;
}

export default async function AssetVerificationReportRoute({
  params,
  searchParams,
}: Readonly<{ params: Promise<{ mode: string }>; searchParams: SearchParams }>) {
  const { mode } = await params;
  return (
    <AssetVerificationReportPage
      mode={assetVerificationReportMode(mode)}
      searchParams={searchParams}
    />
  );
}
