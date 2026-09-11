import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import {
  FinanceFrame,
  FinanceRestricted,
  FinanceUnavailable,
  hasFinanceRole,
} from "@/app/(fleet-operations)/finance/_components";
import {
  FinanceApiError,
  getFinanceProvinces,
  type FinanceOption,
} from "@/lib/api/finance/api-finance";
import { FinanceReportTable } from "@/app/(fleet-operations)/finance/report-table";
import { getWesbankFinanceReport, type FinanceReport } from "@/lib/api/finance/api-finance-reports";
import { getSession } from "@/lib/auth/session";

type Query = Record<string, string | string[] | undefined>;
type PageProps = Readonly<{ params: Promise<{ action: string }>; searchParams: Promise<Query> }>;

const ACTIONS = ["summary-all", "summary-selection", "detailed-all", "detailed-selection"] as const;

const REPORTS: Record<string, readonly { key: string; label: string; download: boolean }[]> = {
  "summary-all": [
    {
      key: "summary-all",
      label: "Show Summary Wesbank Expenses Report for All Provinces",
      download: false,
    },
    {
      key: "department-summary",
      label: "Show Summary Wesbank Expenses Report for All Provinces and Departments",
      download: false,
    },
    {
      key: "site-summary",
      label: "Show Summary Wesbank Expenses Report for All Provinces, Departments and Sites",
      download: false,
    },
    {
      key: "summary-all-download",
      label: "Download Summary Wesbank Expenses Report for All Provinces",
      download: true,
    },
    {
      key: "department-summary-download",
      label: "Download Summary Wesbank Expenses Report for All Provinces and Departments",
      download: true,
    },
    {
      key: "site-summary-download",
      label: "Download Summary Wesbank Expenses Report for All Provinces, Departments and Sites",
      download: true,
    },
  ],
  "summary-selection": [
    {
      key: "summary-province",
      label: "Show Summary Wesbank Expenses Report per Province",
      download: false,
    },
    {
      key: "department-province-summary",
      label: "Show Summary Wesbank Expenses Report per Province and Department",
      download: false,
    },
    {
      key: "site-province-summary",
      label: "Show Summary Wesbank Expenses Report per Province, Department and Site",
      download: false,
    },
    {
      key: "summary-province-download",
      label: "Download Summary Wesbank Expenses Report per Province",
      download: true,
    },
    {
      key: "department-province-summary-download",
      label: "Download Summary Wesbank Expenses Report per Province and Department",
      download: true,
    },
    {
      key: "site-province-summary-download",
      label: "Download Summary Wesbank Expenses Report per Province, Department and Site",
      download: true,
    },
  ],
  "detailed-all": [
    {
      key: "detailed-fuel-download",
      label:
        "Download Detailed Wesbank Expenses Report (Fuel Only) for All Provinces, Departments and Sites",
      download: true,
    },
    {
      key: "detailed-other-download",
      label:
        "Download Detailed Wesbank Expenses Report (Excluding Fuel) for All Provinces, Departments and Sites",
      download: true,
    },
  ],
  "detailed-selection": [
    {
      key: "detailed-fuel-province-download",
      label: "Download Detailed Wesbank Expenses Report (Fuel Only) per Province",
      download: true,
    },
    {
      key: "detailed-other-province-download",
      label: "Download Detailed Wesbank Expenses Report (Excluding Fuel) per Province",
      download: true,
    },
  ],
};

function queryValue(query: Query, name: string) {
  const value = query[name];
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

function titleFor(action: string) {
  return (
    (
      {
        "summary-all": "Summary Wesbank Expenses Reports (All Inclusive)",
        "summary-selection": "Summary Wesbank Expenses Reports (Selection)",
        "detailed-all": "Detailed Wesbank Expenses Reports (All Inclusive)",
        "detailed-selection": "Detailed Wesbank Expenses Reports (Selection)",
      } as Record<string, string>
    )[action] ?? "Wesbank Expenses Reports"
  );
}

function outputHref(action: string, reportAction: string, query: Query, format: "html" | "excel") {
  const params = new URLSearchParams({ kind: "wesbank", action, reportAction, format });
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

async function WesbankReportContent({ params, searchParams }: PageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  const { action: rawAction } = await params;
  const action = rawAction.trim().toLowerCase();
  if (session.status !== "authenticated")
    return (
      <FinanceFrame title={titleFor(action)} description="Wesbank expense reporting.">
        <FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." />
      </FinanceFrame>
    );
  if (!hasFinanceRole(session.roles))
    return (
      <FinanceFrame title={titleFor(action)} description="Wesbank expense reporting.">
        <FinanceRestricted />
      </FinanceFrame>
    );
  if (!ACTIONS.includes(action as (typeof ACTIONS)[number]))
    return (
      <FinanceFrame title="Wesbank Expenses Reports" description="Wesbank expense reporting.">
        <FinanceRestricted message="The requested Wesbank report is not available." />
      </FinanceFrame>
    );

  const query = await searchParams;
  const reports = REPORTS[action] ?? [];
  const selectedReport = queryValue(query, "reportAction");
  const reportDefinition = reports.find((report) => report.key === selectedReport);
  const requiresProvince = action === "summary-selection" || action === "detailed-selection";
  let provinces: FinanceOption[] = [];
  let error: string | null = null;
  try {
    if (requiresProvince) provinces = await getFinanceProvinces();
  } catch (caught) {
    if (caught instanceof FinanceApiError) error = caught.message;
    else throw caught;
  }

  let report: FinanceReport | null = null;
  let output: { href: string; label: string } | null = null;
  if (queryValue(query, "run") === "1") {
    const startDate = queryValue(query, "startDate");
    const endDate = queryValue(query, "endDate");
    const provinceCode = queryValue(query, "provinceCode");
    if (!reportDefinition) error = "Select a Wesbank report action.";
    else if (!startDate || !endDate)
      error = "Select a start date and end date before running the report.";
    else if (requiresProvince && !provinceCode)
      error = "Select a province before running the report.";
    else if (reportDefinition.download)
      output = {
        href: outputHref(action, reportDefinition.key, query, "excel"),
        label: "Download report",
      };
    else {
      try {
        report = await getWesbankFinanceReport({ mode: action, provinceCode, startDate, endDate });
        output = {
          href: outputHref(action, reportDefinition.key, query, "html"),
          label: "Open printable report",
        };
      } catch (caught) {
        error =
          caught instanceof FinanceApiError
            ? caught.message
            : "The Wesbank report could not be generated.";
      }
    }
  }

  return (
    <FinanceFrame title={titleFor(action)} description="Wesbank expense reporting.">
      {error ? (
        <div className="notice notice-error" role="alert">
          {error}
        </div>
      ) : null}
      <form className="vehicle-status-maintenance-panel" method="get">
        <input name="run" type="hidden" value="1" />
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="wesbank-start">
              Start Date
            </label>
            <input
              className="form-input"
              id="wesbank-start"
              name="startDate"
              type="date"
              defaultValue={queryValue(query, "startDate")}
              required
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="wesbank-end">
              End Date
            </label>
            <input
              className="form-input"
              id="wesbank-end"
              name="endDate"
              type="date"
              defaultValue={queryValue(query, "endDate")}
              required
            />
          </div>
          {requiresProvince ? (
            <div className="form-field">
              <label className="form-label" htmlFor="wesbank-province">
                Province
              </label>
              <select
                className="form-select"
                id="wesbank-province"
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
          <h2 className="form-section-title">
            {action.startsWith("detailed") ? "Download Reports" : "Show Reports"}
          </h2>
          <div className="button-row">
            {reports
              .filter((item) => !item.download)
              .map((item) => (
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
        {reports.some((item) => item.download) ? (
          <div className="form-section">
            <h2 className="form-section-title">Download Reports</h2>
            <div className="button-row">
              {reports
                .filter((item) => item.download)
                .map((item) => (
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
        ) : null}
        <div className="button-row">
          <Link className="button button-secondary" href="/finance/wesbank">
            Back
          </Link>
        </div>
      </form>
      <div className="notice notice-info" role="note">
        Detailed reports can be large. Save Excel files before opening them.
      </div>
      {output ? (
        <section className="vehicle-status-maintenance-panel" aria-labelledby="wesbank-output">
          <h2 id="wesbank-output">Report ready</h2>
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
          basePath={`/finance/wesbank/${action}`}
          query={query}
          page={Number(queryValue(query, "page")) || 1}
        />
      ) : null}
    </FinanceFrame>
  );
}

export default function WesbankReportPage(props: PageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <WesbankReportContent {...props} />
    </Suspense>
  );
}
