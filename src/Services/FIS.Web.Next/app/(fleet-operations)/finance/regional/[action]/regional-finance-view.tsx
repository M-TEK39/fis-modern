import Link from "next/link";
import type { ReactNode } from "react";

import { FinanceFrame } from "@/app/(fleet-operations)/finance/_components";
import { FinanceReportTable } from "@/app/(fleet-operations)/finance/report-table";
import { ReportPagination } from "@/app/(fleet-operations)/reports/_components";
import ReportResultsPanel from "@/components/ui/report-results-panel";
import ReportRowsTable from "@/components/ui/report-rows-table";
import type { FinanceOption } from "@/lib/api/finance/api-finance";
import type { FinanceReport } from "@/lib/api/finance/api-finance-reports";
import type { LegacyReport } from "@/lib/api/reports/api-legacy-reports";

type Query = Record<string, string | string[] | undefined>;

function queryValue(query: Query, name: string) {
  const value = query[name];
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
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

function renderSummaryReportButtons(
  summaryReports: readonly { key: string; label: string; download: boolean }[],
  download: boolean,
) {
  return summaryReports.reduce<ReactNode[]>((buttons, item) => {
    if (item.download !== download) return buttons;
    buttons.push(
      <button
        className="button button-secondary"
        key={item.key}
        name="reportAction"
        type="submit"
        value={item.key}
      >
        {item.label}
      </button>,
    );
    return buttons;
  }, []);
}

function legacyAssetPageHref(action: string, query: Query, page: number, pageSize: number) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    const item = Array.isArray(value) ? value[0] : value;
    if (!item || ["page", "pageSize"].includes(key)) continue;
    params.set(key, item);
  }
  params.set("run", "1");
  params.set("page", String(page));
  params.set("pageSize", String(pageSize));
  return `/finance/regional/${action}?${params.toString()}`;
}

function legacyAssetPrintHref(report: LegacyReport, query: Query) {
  const params = new URLSearchParams({ kind: "legacy-asset", reportKey: report.reportKey });
  const province = queryValue(query, "provinceCode");
  const department = queryValue(query, "departmentCode");
  const site = queryValue(query, "siteCode");

  if (province) params.set("province", province);
  if (department) params.set("department", department);
  if (site) params.set("site", site);

  return `/finance/reports/output?${params.toString()}`;
}

function LegacyAssetReportResults({
  action,
  query,
  report,
}: Readonly<{
  action: string;
  query: Query;
  report: LegacyReport;
}>) {
  return (
    <ReportResultsPanel
      headingId="regional-asset-report-results"
      eyebrow={`${report.totalCount} record${report.totalCount === 1 ? "" : "s"}`}
      heading={report.title}
      trailing={
        <a
          className="button button-secondary"
          href={legacyAssetPrintHref(report, query)}
          target="_blank"
          rel="noreferrer"
        >
          Open printable report
        </a>
      }
      printButton={false}
    >
      {report.rows.length === 0 ? (
        <div className="vehicle-empty-state">
          <p>No assets matched the selected report parameters.</p>
        </div>
      ) : (
        <ReportRowsTable columns={report.columns} rows={report.rows} caption={report.title} />
      )}
      <ReportPagination
        report={report}
        pageHref={(page) => legacyAssetPageHref(action, query, page, report.pageSize)}
        label="Regional asset report pages"
      />
    </ReportResultsPanel>
  );
}

export function RegionalFinanceView({
  action,
  title,
  query,
  assetReport,
  departments,
  sites,
  provinces,
  summaryReports,
  error,
  output,
  report,
  legacyAssetReport,
}: Readonly<{
  action: string;
  title: string;
  query: Query;
  assetReport: boolean;
  departments: FinanceOption[];
  sites: FinanceOption[];
  provinces: FinanceOption[];
  summaryReports: readonly { key: string; label: string; download: boolean }[];
  error: string | null;
  output: { href: string; label: string } | null;
  report: FinanceReport | null;
  legacyAssetReport: LegacyReport | null;
}>) {
  return (
    <FinanceFrame title={title} description="Regional finance reporting.">
      {error ? (
        <div className="notice notice-error" role="alert">
          {error}
        </div>
      ) : null}
      {assetReport ? (
        <form className="vehicle-status-maintenance-panel" method="get">
          <input name="run" type="hidden" value="1" />
          <div className="form-grid">
            {action === "assets-province" ? (
              <div className="form-field">
                <label className="form-label" htmlFor="regional-province">
                  Province
                </label>
                <select
                  className="form-select"
                  id="regional-province"
                  name="provinceCode"
                  defaultValue={queryValue(query, "provinceCode")}
                  required
                >
                  {optionList(provinces, "Select Province")}
                </select>
              </div>
            ) : null}
            {action === "assets-department" || action === "assets-site" ? (
              <div className="form-field">
                <label className="form-label" htmlFor="regional-department">
                  Department
                </label>
                <select
                  className="form-select"
                  id="regional-department"
                  name="departmentCode"
                  defaultValue={queryValue(query, "departmentCode")}
                  required
                >
                  {optionList(departments, "Select Department")}
                </select>
              </div>
            ) : null}
            {action === "assets-site" ? (
              <div className="form-field">
                <label className="form-label" htmlFor="regional-site">
                  Site
                </label>
                <select
                  className="form-select"
                  id="regional-site"
                  name="siteCode"
                  defaultValue={queryValue(query, "siteCode")}
                  required
                >
                  {optionList(sites, "Select Site")}
                </select>
              </div>
            ) : null}
          </div>
          <div className="button-row">
            <button className="button button-primary" type="submit">
              View Report
            </button>
            <Link className="button button-secondary" href="/finance/regional/assets">
              Back
            </Link>
          </div>
        </form>
      ) : (
        <form className="vehicle-status-maintenance-panel" method="get">
          <input name="run" type="hidden" value="1" />
          <div className="form-grid">
            <div className="form-field">
              <label className="form-label" htmlFor="regional-start">
                Posting Start Date
              </label>
              <input
                className="form-input"
                id="regional-start"
                name="startDate"
                type="date"
                defaultValue={queryValue(query, "startDate")}
                required
              />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="regional-end">
                Posting End Date
              </label>
              <input
                className="form-input"
                id="regional-end"
                name="endDate"
                type="date"
                defaultValue={queryValue(query, "endDate")}
                required
              />
            </div>
            {action === "summary-per-province" ? (
              <div className="form-field">
                <label className="form-label" htmlFor="regional-summary-province">
                  Province
                </label>
                <select
                  className="form-select"
                  id="regional-summary-province"
                  name="provinceCode"
                  defaultValue={queryValue(query, "provinceCode")}
                  required
                >
                  {optionList(provinces, "Select Province")}
                </select>
              </div>
            ) : null}
          </div>
          <div className="form-section">
            <h2 className="form-section-title">Show Reports</h2>
            <div className="button-row">{renderSummaryReportButtons(summaryReports, false)}</div>
          </div>
          <div className="form-section">
            <h2 className="form-section-title">Download Reports</h2>
            <div className="button-row">{renderSummaryReportButtons(summaryReports, true)}</div>
          </div>
          <div className="button-row">
            <Link className="button button-secondary" href="/finance/regional">
              Back
            </Link>
          </div>
        </form>
      )}
      {assetReport ? (
        <div className="notice notice-info" role="note">
          The asset list is read from the compatibility report endpoint and includes New and
          In-Service vehicles.
        </div>
      ) : (
        <div className="notice notice-info" role="note">
          Detailed reports can be large. Save Excel files before opening them.
        </div>
      )}
      {output ? (
        <section className="vehicle-status-maintenance-panel" aria-labelledby="regional-output">
          <h2 id="regional-output">Report ready</h2>
          <p className="muted-copy">
            The report is generated through the authenticated server path.
          </p>
          <a className="button button-primary" href={output.href} target="_blank" rel="noreferrer">
            {output.label}
          </a>
        </section>
      ) : null}
      {legacyAssetReport ? (
        <LegacyAssetReportResults action={action} query={query} report={legacyAssetReport} />
      ) : null}
      {report ? (
        <FinanceReportTable
          report={report}
          basePath={`/finance/regional/${action}`}
          query={query}
          page={Number(queryValue(query, "page")) || 1}
          printOrientation="landscape"
        />
      ) : null}
    </FinanceFrame>
  );
}
