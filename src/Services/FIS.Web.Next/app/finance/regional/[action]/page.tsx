import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import {
  FinanceFrame,
  FinanceRestricted,
  FinanceUnavailable,
  hasFinanceRole,
} from "@/app/finance/_components";
import { FinanceReportTable } from "@/app/finance/report-table";
import {
  FinanceApiError,
  getFinanceDepartments,
  getFinanceProvinces,
  getFinanceSites,
  type FinanceOption,
} from "@/lib/api-finance";
import { getLegacyReport, LegacyReportApiError, type LegacyReport } from "@/lib/api-legacy-reports";
import {
  getRegionalFinanceReport,
  REGIONAL_SUMMARY_ACTIONS,
  type FinanceReport,
} from "@/lib/api-finance-reports";
import { getSession } from "@/lib/session";

type Query = Record<string, string | string[] | undefined>;
type PageProps = Readonly<{ params: Promise<{ action: string }>; searchParams: Promise<Query> }>;

const ACTIONS = [
  "summary-per-province",
  "summary-all",
  "assets-all",
  "assets-province",
  "assets-department",
  "assets-site",
] as const;

const SUMMARY_REPORTS: readonly {
  key: (typeof REGIONAL_SUMMARY_ACTIONS)[number];
  label: string;
  download: boolean;
}[] = [
  { key: "summary", label: "Show Summary Invoice Report", download: false },
  {
    key: "department-cost-type",
    label: "Show Summary Invoice Report Per Department By Cost Type",
    download: false,
  },
  {
    key: "site-cost-type",
    label: "Show Summary Invoice Report Per Department Per Site By Cost Type",
    download: false,
  },
  { key: "summary-download", label: "Download Summary Invoice Report", download: true },
  {
    key: "department-cost-type-download",
    label: "Download Summary Invoice Report Per Department By Cost Type",
    download: true,
  },
  {
    key: "site-cost-type-download",
    label: "Download Summary Invoice Report Per Department Per Site By Cost Type",
    download: true,
  },
];

function queryValue(query: Query, name: string) {
  const value = query[name];
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

function titleFor(action: string) {
  return (
    (
      {
        "summary-per-province": "Summary Invoice Reports (Per Province)",
        "summary-all": "Summary Invoice Reports (All)",
        "assets-all": "Asset List: All Vehicles in FIS",
        "assets-province": "Asset List: Vehicles by Province",
        "assets-department": "Asset List: Vehicles by Department",
        "assets-site": "Asset List: Vehicles by Site",
      } as Record<string, string>
    )[action] ?? "Regional Module: Finance"
  );
}

function outputHref(action: string, reportAction: string, query: Query, format: "html" | "excel") {
  const params = new URLSearchParams({ kind: "regional", action, reportAction, format });
  for (const name of ["provinceCode", "startDate", "endDate"]) {
    const value = queryValue(query, name);
    if (value) params.set(name, value);
  }
  return `/finance/reports/output?${params.toString()}`;
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

function legacyToFinanceReport(report: LegacyReport): FinanceReport {
  return { title: report.title, rows: report.rows, supportsDateFilter: null };
}

function regionalSummaryType(action: string, reportAction: string) {
  const provincePrefix = action === "summary-per-province" ? "PerProvince" : "";
  return reportAction.replace(/-download$/, "") === "summary"
    ? `SummaryReport${provincePrefix}`
    : reportAction.replace(/-download$/, "") === "department-cost-type"
      ? `SummaryReport${provincePrefix}DeptCostType`
      : `SummaryReport${provincePrefix}ByCostType`;
}

export default async function RegionalFinanceActionPage({ params, searchParams }: PageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  const { action: rawAction } = await params;
  const action = rawAction.trim().toLowerCase();
  if (session.status !== "authenticated")
    return (
      <FinanceFrame title={titleFor(action)} description="Regional finance reporting.">
        <FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." />
      </FinanceFrame>
    );
  if (!hasFinanceRole(session.roles))
    return (
      <FinanceFrame title={titleFor(action)} description="Regional finance reporting.">
        <FinanceRestricted />
      </FinanceFrame>
    );
  if (!ACTIONS.includes(action as (typeof ACTIONS)[number]))
    return (
      <FinanceFrame title="Regional Module: Finance" description="Regional finance reporting.">
        <FinanceRestricted message="The requested Regional Finance report is not available." />
      </FinanceFrame>
    );

  const query = await searchParams;
  const assetReport = action.startsWith("assets-");
  let departments: FinanceOption[] = [];
  let sites: FinanceOption[] = [];
  let provinces: FinanceOption[] = [];
  let error: string | null = null;
  try {
    if (assetReport) {
      departments = await getFinanceDepartments();
      if (action === "assets-province") provinces = await getFinanceProvinces();
      if (action === "assets-site") {
        const departmentCode = Number(queryValue(query, "departmentCode"));
        sites = await getFinanceSites(
          Number.isSafeInteger(departmentCode) && departmentCode > 0 ? departmentCode : undefined,
        );
      }
    } else if (action === "summary-per-province") {
      provinces = await getFinanceProvinces();
    }
  } catch (caught) {
    if (caught instanceof FinanceApiError) error = caught.message;
    else throw caught;
  }

  let report: FinanceReport | null = null;
  let output: { href: string; label: string } | null = null;
  if (queryValue(query, "run") === "1") {
    if (assetReport) {
      const departmentCode = queryValue(query, "departmentCode");
      const provinceCode = queryValue(query, "provinceCode");
      const siteCode = queryValue(query, "siteCode");
      if (action === "assets-province" && !provinceCode)
        error = "Select a province before viewing the report.";
      else if (action === "assets-department" && !departmentCode)
        error = "Select a department before viewing the report.";
      else if (action === "assets-site" && (!departmentCode || !siteCode))
        error = "Select a department and site before viewing the report.";
      else {
        try {
          const legacy = await getLegacyReport("asset-list", {
            province: provinceCode || undefined,
            department: departmentCode || undefined,
            site: siteCode || undefined,
          });
          report = legacyToFinanceReport(legacy);
        } catch (caught) {
          if (caught instanceof LegacyReportApiError) error = caught.message;
          else throw caught;
        }
      }
    } else {
      const reportAction = queryValue(
        query,
        "reportAction",
      ) as (typeof REGIONAL_SUMMARY_ACTIONS)[number];
      const reportDefinition = SUMMARY_REPORTS.find((item) => item.key === reportAction);
      const startDate = queryValue(query, "startDate");
      const endDate = queryValue(query, "endDate");
      const provinceCode = queryValue(query, "provinceCode");
      if (!reportDefinition) error = "Select a Regional Finance report action.";
      else if (!startDate || !endDate)
        error = "Select a posting start date and end date before running the report.";
      else if (action === "summary-per-province" && !provinceCode)
        error = "Select a province before running the report.";
      else if (reportDefinition.download)
        output = {
          href: outputHref(action, reportDefinition.key, query, "excel"),
          label: "Download report",
        };
      else {
        try {
          report = await getRegionalFinanceReport({
            mode: action,
            summaryType: regionalSummaryType(action, reportDefinition.key),
            provinceCode,
            startDate,
            endDate,
          });
          output = {
            href: outputHref(action, reportDefinition.key, query, "html"),
            label: "Open printable report",
          };
        } catch (caught) {
          error =
            caught instanceof FinanceApiError
              ? caught.message
              : "The Regional Finance report could not be generated.";
        }
      }
    }
  }

  return (
    <FinanceFrame title={titleFor(action)} description="Regional finance reporting.">
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
            <div className="button-row">
              {SUMMARY_REPORTS.filter((item) => !item.download).map((item) => (
                <button
                  className="button button-secondary"
                  key={item.key}
                  name="reportAction"
                  type="submit"
                  value={item.key}
                >
                  {item.label}
                </button>
              ))}
            </div>
          </div>
          <div className="form-section">
            <h2 className="form-section-title">Download Reports</h2>
            <div className="button-row">
              {SUMMARY_REPORTS.filter((item) => item.download).map((item) => (
                <button
                  className="button button-secondary"
                  key={item.key}
                  name="reportAction"
                  type="submit"
                  value={item.key}
                >
                  {item.label}
                </button>
              ))}
            </div>
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
      {report ? (
        <FinanceReportTable
          report={report}
          basePath={`/finance/regional/${action}`}
          query={query}
          page={Number(queryValue(query, "page")) || 1}
        />
      ) : null}
    </FinanceFrame>
  );
}
