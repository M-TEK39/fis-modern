import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import {
  FinanceFrame,
  FinanceRestricted,
  FinanceUnavailable,
} from "@/app/(fleet-operations)/finance/_components";
import {
  canSelectAllFinanceDepartments,
  hasGeneralFinanceReportsAccess,
} from "@/app/(fleet-operations)/finance/_utils";
import { departmentOptions } from "@/app/(fleet-operations)/finance/_location-options";
import { DepartmentApiError, getDepartments } from "@/lib/api/reference-data/api-departments";
import { getSession } from "@/lib/auth/session";

type Query = Record<string, string | string[] | undefined>;

function queryValue(query: Query, name: string) {
  const value = query[name];
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

function positiveInteger(value: string) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : undefined;
}

export default async function WesbankSiteVehicleDetailPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    return (
      <FinanceFrame
        title="Wesbank Expenses by Site and Vehicle"
        description="Select a department and date range before viewing the Wesbank detail report."
      >
        <FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." />
      </FinanceFrame>
    );
  if (!hasGeneralFinanceReportsAccess(session.roles))
    return (
      <FinanceFrame
        title="Wesbank Expenses by Site and Vehicle"
        description="Select a department and date range before viewing the Wesbank detail report."
      >
        <FinanceRestricted message="The legacy report requires Reports or Financial Reports permission." />
      </FinanceFrame>
    );

  const query = await searchParams;
  const profileDepartmentCode = positiveInteger(session.departmentCode ?? "");
  const canSelectAllDepartments = canSelectAllFinanceDepartments(
    session.roles,
    session.canAccessAllFinanceDepartments,
    session.accessLevel,
  );
  let departments = [] as ReturnType<typeof departmentOptions>;
  let error: string | null = null;
  try {
    if (!canSelectAllDepartments && !profileDepartmentCode)
      throw new DepartmentApiError(
        "invalid-response",
        "Your Finance profile department is unavailable.",
      );
    const records = await getDepartments();
    departments = departmentOptions(
      canSelectAllDepartments
        ? records
        : records.filter((department) => department.departmentCode === profileDepartmentCode),
    );
  } catch (caught) {
    error =
      caught instanceof DepartmentApiError
        ? caught.message
        : "The department selector could not be loaded.";
  }

  const departmentCode =
    positiveInteger(queryValue(query, "departmentCode")) ??
    (!canSelectAllDepartments ? profileDepartmentCode : undefined);
  const startDate = queryValue(query, "startDate");
  const endDate = queryValue(query, "endDate");
  const submitted = queryValue(query, "run") === "1";
  if (
    submitted &&
    departmentCode &&
    /^\d{4}-\d{2}-\d{2}$/.test(startDate) &&
    /^\d{4}-\d{2}-\d{2}$/.test(endDate)
  ) {
    redirect(
      `/finance/reports/output?${new URLSearchParams({
        kind: "legacy-detail",
        item: "wesbank-site-vehicle-detail",
        departmentCode: String(departmentCode),
        startDate,
        endDate,
        format: "html",
      }).toString()}`,
    );
  }
  if (submitted && !error)
    error = "Select a department, start date and end date before running the report.";

  return (
    <FinanceFrame
      title="Wesbank Expenses by Site and Vehicle"
      description="Select the department and date range used by the legacy Finance report."
    >
      {error ? (
        <div className="notice notice-error" role="alert">
          {error}
        </div>
      ) : null}
      <form className="vehicle-status-maintenance-panel" method="get">
        <input name="run" type="hidden" value="1" />
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="wesbank-detail-department">
              Department
            </label>
            <select
              className="form-select"
              id="wesbank-detail-department"
              name="departmentCode"
              defaultValue={String(departmentCode ?? "")}
              required
            >
              <option value="">Select Department</option>
              {departments.map((department) => (
                <option key={department.value} value={department.value}>
                  {department.label}
                </option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="wesbank-detail-start">
              Start Date
            </label>
            <input
              className="form-input"
              id="wesbank-detail-start"
              name="startDate"
              type="date"
              defaultValue={startDate}
              required
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="wesbank-detail-end">
              End Date
            </label>
            <input
              className="form-input"
              id="wesbank-detail-end"
              name="endDate"
              type="date"
              defaultValue={endDate}
              required
            />
          </div>
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            View Report
          </button>
          <Link className="button button-secondary" href="/finance/wesbank">
            Back
          </Link>
        </div>
      </form>
    </FinanceFrame>
  );
}
