import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import {
  FinanceFrame,
  FinanceRestricted,
  FinanceUnavailable,
} from "@/app/(fleet-operations)/finance/_components";
import {
  hasAdministratorRole,
  hasRole,
} from "@/app/(fleet-operations)/finance/_utils";
import { departmentOptions, siteOptions } from "@/app/(fleet-operations)/finance/_location-options";
import { DepartmentApiError, getDepartments } from "@/lib/api/reference-data/api-departments";
import { SiteApiError, getSites } from "@/lib/api/reference-data/api-sites";
import { getSession } from "@/lib/auth/session";

type Query = Record<string, string | string[] | undefined>;

const REPORTS = {
  AllRoutesOver25000KM: {
    item: "trip-routes-over-25000",
    title: "Trip Authorities Exceeding 25 000 km per Trip",
  },
  AllDayTripsOver3500KM: {
    item: "trip-day-routes-over-3500",
    title: "Trip Authorities Exceeding 3 500 km per Day per Route",
  },
} as const;

function queryValue(query: Query, name: string) {
  const value = query[name];
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

function positiveInteger(value: string) {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : undefined;
}

function outputHref(item: string, departmentCode: number, siteCode: number, query: Query) {
  const startDate = queryValue(query, "startDate");
  const endDate = queryValue(query, "endDate");
  const params = new URLSearchParams({
    kind: "legacy-detail",
    item,
    departmentCode: String(departmentCode),
    siteCode: String(siteCode),
    startDate,
    endDate,
    format: "html",
  });
  return `/finance/reports/output?${params.toString()}`;
}

export default async function TripKilometresPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Query> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    return (
      <FinanceFrame
        title="Maximum Kilometre Trip Reports"
        description="Trip authority report selection."
      >
        <FinanceUnavailable message="The sign-in service is temporarily unavailable. Please try again." />
      </FinanceFrame>
    );
  if (!hasRole(session.roles, "Reports"))
    return (
      <FinanceFrame
        title="Maximum Kilometre Trip Reports"
        description="Trip authority report selection."
      >
        <FinanceRestricted message="The legacy Trip Reports menu requires the Reports permission." />
      </FinanceFrame>
    );

  const query = await searchParams;
  const selectedReport = REPORTS[queryValue(query, "report") as keyof typeof REPORTS];
  if (!selectedReport)
    return (
      <FinanceFrame
        title="Maximum Kilometre Trip Reports"
        description="Trip authority report selection."
      >
        <FinanceRestricted message="The requested legacy trip report is not available." />
      </FinanceFrame>
    );

  const profileDepartmentCode = positiveInteger(session.departmentCode ?? "");
  const profileSiteCode = positiveInteger(session.siteCode ?? "");
  const canSelectProvinceDepartments =
    hasAdministratorRole(session.roles) ||
    hasRole(session.roles, "Vehicle List for All Departments in Province");
  let departments = [] as ReturnType<typeof departmentOptions>;
  let sites = [] as ReturnType<typeof siteOptions>;
  let error: string | null = null;
  const selectedDepartmentCode =
    positiveInteger(queryValue(query, "departmentCode")) ?? profileDepartmentCode;
  try {
    if (!profileDepartmentCode || !profileSiteCode)
      throw new DepartmentApiError(
        "invalid-response",
        "Your legacy profile location is unavailable. Sign out and sign back in before running this report.",
      );
    const [departmentRecords, siteRecords] = await Promise.all([getDepartments(), getSites()]);
    const profileSite = siteRecords.find((site) => site.siteCode === profileSiteCode);
    if (!profileSite?.provinceCode)
      throw new SiteApiError("invalid-response", "Your legacy profile province is unavailable.");
    const provinceDepartmentCodes = new Set(
      siteRecords
        .filter((site) => site.provinceCode === profileSite.provinceCode)
        .map((site) => site.departmentCode)
        .filter((code): code is number => code !== null),
    );
    departments = departmentOptions(
      canSelectProvinceDepartments
        ? departmentRecords.filter((department) =>
            provinceDepartmentCodes.has(department.departmentCode),
          )
        : departmentRecords.filter(
            (department) => department.departmentCode === profileDepartmentCode,
          ),
    );
    sites = siteOptions(
      siteRecords.filter((site) => site.departmentCode === selectedDepartmentCode),
    );
  } catch (caught) {
    error =
      caught instanceof DepartmentApiError || caught instanceof SiteApiError
        ? caught.message
        : "The report location options could not be loaded.";
  }

  const submitted = queryValue(query, "run") === "1";
  const departmentCode = positiveInteger(queryValue(query, "departmentCode"));
  const siteCode = positiveInteger(queryValue(query, "siteCode"));
  const startDate = queryValue(query, "startDate");
  const endDate = queryValue(query, "endDate");
  const output =
    submitted &&
    departmentCode &&
    siteCode &&
    /^\d{4}-\d{2}-\d{2}$/.test(startDate) &&
    /^\d{4}-\d{2}-\d{2}$/.test(endDate)
      ? outputHref(selectedReport.item, departmentCode, siteCode, query)
      : null;
  if (submitted && !error && !output)
    error = "Select a department, site, start date and end date before running the report.";
  if (output) redirect(output);

  return (
    <FinanceFrame
      title={selectedReport.title}
      description="Select the department, site and date range used by the legacy Trip Reports screen."
    >
      {error ? (
        <div className="notice notice-error" role="alert">
          {error}
        </div>
      ) : null}
      <form className="vehicle-status-maintenance-panel" method="get">
        <input name="report" type="hidden" value={queryValue(query, "report")} />
        <input name="run" type="hidden" value="1" />
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="trip-kilometres-department">
              Department
            </label>
            {canSelectProvinceDepartments ? (
              <select
                className="form-select"
                id="trip-kilometres-department"
                name="departmentCode"
                defaultValue={String(selectedDepartmentCode ?? "")}
                required
              >
                <option value="">Select Department</option>
                {departments.map((department) => (
                  <option key={department.value} value={department.value}>
                    {department.label}
                  </option>
                ))}
              </select>
            ) : (
              <>
                <input
                  name="departmentCode"
                  type="hidden"
                  value={String(profileDepartmentCode ?? "")}
                />
                <input
                  className="form-input"
                  id="trip-kilometres-department"
                  readOnly
                  value={departments[0]?.label ?? "Your department"}
                />
              </>
            )}
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="trip-kilometres-site">
              Site
            </label>
            <select
              className="form-select"
              id="trip-kilometres-site"
              name="siteCode"
              defaultValue={queryValue(query, "siteCode") || String(profileSiteCode ?? "")}
              required
            >
              <option value="">Select Site</option>
              {sites.map((site) => (
                <option key={site.value} value={site.value}>
                  {site.label}
                </option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="trip-kilometres-start">
              Start Date
            </label>
            <input
              className="form-input"
              id="trip-kilometres-start"
              name="startDate"
              type="date"
              defaultValue={startDate}
              required
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="trip-kilometres-end">
              End Date
            </label>
            <input
              className="form-input"
              id="trip-kilometres-end"
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
          <Link className="button button-secondary" href="/reports/trip-authority">
            Back
          </Link>
        </div>
      </form>
    </FinanceFrame>
  );
}
