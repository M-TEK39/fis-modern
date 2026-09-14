import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import {
  FinanceFrame,
  FinanceRestricted,
  FinanceUnavailable,
} from "@/app/(fleet-operations)/finance/_components";
import {
  canMaintainAllFinanceData,
  hasFinanceRole,
  hasFinancialReportsRole,
} from "@/app/(fleet-operations)/finance/_utils";
import { departmentOptions } from "@/app/(fleet-operations)/finance/_location-options";
import { DepartmentApiError, getDepartments } from "@/lib/api/reference-data/api-departments";
import { FinanceReportTable } from "@/app/(fleet-operations)/finance/report-table";
import {
  FinanceApiError,
  runFinanceAction,
  type FinanceOption,
} from "@/lib/api/finance/api-finance";
import { mapFinanceReport, type FinanceReport } from "@/lib/api/finance/api-finance-reports";
import { getSession } from "@/lib/auth/session";

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

function positiveInteger(value: string) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : undefined;
}

function optionList(options: FinanceOption[], emptyLabel: string, includeAllDepartments = false) {
  return (
    <>
      <option value="">{emptyLabel}</option>
      {includeAllDepartments ? <option value="0">XXXXX DEPT: ALL DEPARTMENTS</option> : null}
      {options.map((item) => (
        <option key={item.value} value={item.value}>
          {item.label}
        </option>
      ))}
    </>
  );
}

const FinanceOutstandingActionContent = renderFinanceOutstandingActionContent;

async function renderFinanceOutstandingActionContent({ params, searchParams }: PageProps) {
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
  if (!hasFinanceRole(session.roles) || !hasFinancialReportsRole(session.roles))
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
  const profileDepartmentCode = positiveInteger(session.departmentCode ?? "");
  const canSelectAllDepartments = canMaintainAllFinanceData(
    session.roles,
    session.canMaintainFinanceDataAllDepartments,
  );
  let departments: FinanceOption[] = [];
  let error: string | null = null;
  try {
    const departmentRecords = await getDepartments();
    departments = departmentOptions(
      canSelectAllDepartments
        ? departmentRecords
        : departmentRecords.filter(
            (department) => department.departmentCode === profileDepartmentCode,
          ),
    );
  } catch (caught) {
    if (caught instanceof FinanceApiError || caught instanceof DepartmentApiError)
      error = caught.message;
    else throw caught;
  }
  let report: FinanceReport | null = null;
  if (queryValue(query, "run") === "1") {
    const departmentCode =
      queryValue(query, "departmentCode") ||
      (profileDepartmentCode ? String(profileDepartmentCode) : "");
    if (action === "department-site-vehicle" && !departmentCode)
      error = "Select a department before running the report.";
    else {
      try {
        report = mapFinanceReport(
          await runFinanceAction("api/report/finance/outstanding", {
            mode: action,
            departmentCode,
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
  const needsDepartment = action === "department-site-vehicle";
  const selectedDepartmentCode =
    queryValue(query, "departmentCode") ||
    (profileDepartmentCode ? String(profileDepartmentCode) : "");
  const selectedDepartment = departments.find(
    (department) => department.value === selectedDepartmentCode,
  );
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
          {needsDepartment ? (
            <div className="form-field">
              <label className="form-label" htmlFor="outstanding-department">
                Report Department
              </label>
              {canSelectAllDepartments ? (
                <select
                  className="form-select"
                  id="outstanding-department"
                  name="departmentCode"
                  defaultValue={selectedDepartmentCode}
                  required
                >
                  {optionList(departments, "Select Department", true)}
                </select>
              ) : profileDepartmentCode ? (
                <>
                  <input name="departmentCode" type="hidden" value={profileDepartmentCode} />
                  <div className="form-readonly-value">
                    {selectedDepartment?.label ?? `Department (${profileDepartmentCode})`}
                  </div>
                </>
              ) : (
                <div className="form-readonly-value">
                  No Finance profile department is available.
                </div>
              )}
            </div>
          ) : null}
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
          printOrientation="portrait"
        />
      ) : null}
    </FinanceFrame>
  );
}

export default function FinanceOutstandingActionPage(props: PageProps) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <FinanceOutstandingActionContent {...props} />
    </Suspense>
  );
}
