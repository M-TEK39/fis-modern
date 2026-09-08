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
  getFinanceSites,
  getFinanceYears,
  runFinanceAction,
  type FinanceOption,
} from "@/lib/api-finance";
import { mapFinanceReport, type FinanceReport } from "@/lib/api-finance-reports";
import { getSession } from "@/lib/session";

type Query = Record<string, string | string[] | undefined>;
type PageProps = Readonly<{ params: Promise<{ action: string }>; searchParams: Promise<Query> }>;

const ACTIONS = [
  "department-site-vehicle",
  "department",
  "department-site",
  "month-end-vehicle",
  "allocation-exception",
] as const;

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
        "department-site-vehicle": "Outstanding Amounts per Department, Site and Vehicle",
        department: "Outstanding Amounts per Department",
        "department-site": "Outstanding Amounts per Department and Site",
        "month-end-vehicle": "Outstanding Amounts at Month End per Vehicle",
        "allocation-exception": "Allocation Exceptions (Un-Interfaced Transactions)",
      } as Record<string, string>
    )[action] ?? "Outstanding Amounts Reports"
  );
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

export default async function FinanceOutstandingActionPage({ params, searchParams }: PageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  const { action: rawAction } = await params;
  const action = rawAction.trim().toLowerCase();
  if (session.status !== "authenticated")
    return (
      <FinanceFrame title={titleFor(action)} description="Outstanding amounts reporting.">
        <FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." />
      </FinanceFrame>
    );
  if (!hasFinanceRole(session.roles))
    return (
      <FinanceFrame title={titleFor(action)} description="Outstanding amounts reporting.">
        <FinanceRestricted />
      </FinanceFrame>
    );
  if (!ACTIONS.includes(action as (typeof ACTIONS)[number]))
    return (
      <FinanceFrame
        title="Outstanding Amounts Reports"
        description="Outstanding amounts reporting."
      >
        <FinanceRestricted message="The requested outstanding report is not available." />
      </FinanceFrame>
    );

  const query = await searchParams;
  let departments: FinanceOption[] = [];
  let sites: FinanceOption[] = [];
  let years: FinanceOption[] = [];
  let error: string | null = null;
  try {
    [departments, sites, years] = await Promise.all([
      getFinanceDepartments(),
      getFinanceSites(),
      getFinanceYears(),
    ]);
  } catch (caught) {
    if (caught instanceof FinanceApiError) error = caught.message;
    else throw caught;
  }
  let report: FinanceReport | null = null;
  if (queryValue(query, "run") === "1") {
    const departmentCode = queryValue(query, "departmentCode");
    const siteCode = queryValue(query, "siteCode");
    const financialYear = queryValue(query, "financialYear");
    const vmfCode = positiveInteger(queryValue(query, "vmfCode"));
    if (!departmentCode || !financialYear)
      error = "Select a department and financial year before running the report.";
    else if ((action === "department-site" || action === "department-site-vehicle") && !siteCode)
      error = "Select a site before running the report.";
    else if ((action === "department-site-vehicle" || action === "month-end-vehicle") && !vmfCode)
      error = "Enter a vehicle VMF code before running the report.";
    else {
      try {
        report = mapFinanceReport(
          await runFinanceAction("api/report/finance/outstanding", {
            mode: action,
            departmentCode,
            siteCode,
            financialYear,
            vmfCode,
          }),
          titleFor(action),
        );
      } catch (caught) {
        error =
          caught instanceof FinanceApiError
            ? caught.message
            : "The outstanding report could not be generated.";
      }
    }
  }
  const needsSite = action === "department-site" || action === "department-site-vehicle";
  const needsVehicle = action === "department-site-vehicle" || action === "month-end-vehicle";
  return (
    <FinanceFrame title={titleFor(action)} description="Outstanding amounts reporting.">
      {error ? (
        <div className="notice notice-error" role="alert">
          {error}
        </div>
      ) : null}
      <form className="vehicle-status-maintenance-panel" method="get">
        <input name="run" type="hidden" value="1" />
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="outstanding-department">
              Report Department
            </label>
            <select
              className="form-select"
              id="outstanding-department"
              name="departmentCode"
              defaultValue={queryValue(query, "departmentCode")}
              required
            >
              {optionList(departments, "Select Department")}
            </select>
          </div>
          {needsSite ? (
            <div className="form-field">
              <label className="form-label" htmlFor="outstanding-site">
                Site
              </label>
              <select
                className="form-select"
                id="outstanding-site"
                name="siteCode"
                defaultValue={queryValue(query, "siteCode")}
                required
              >
                {optionList(sites, "Select Site")}
              </select>
            </div>
          ) : null}
          {needsVehicle ? (
            <div className="form-field">
              <label className="form-label" htmlFor="outstanding-vmf">
                Vehicle VMF Code
              </label>
              <input
                className="form-input"
                id="outstanding-vmf"
                name="vmfCode"
                defaultValue={queryValue(query, "vmfCode")}
                inputMode="numeric"
                required
              />
            </div>
          ) : null}
          <div className="form-field">
            <label className="form-label" htmlFor="outstanding-year">
              Financial Year
            </label>
            <select
              className="form-select"
              id="outstanding-year"
              name="financialYear"
              defaultValue={queryValue(query, "financialYear")}
              required
            >
              {optionList(years, "Select Financial Year")}
            </select>
          </div>
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            View Report
          </button>
          <Link className="button button-secondary" href="/finance">
            Finance Menu
          </Link>
        </div>
      </form>
      {report ? (
        <FinanceReportTable
          report={report}
          basePath={`/finance/outstanding/${action}`}
          query={query}
          page={Number(queryValue(query, "page")) || 1}
        />
      ) : null}
    </FinanceFrame>
  );
}
