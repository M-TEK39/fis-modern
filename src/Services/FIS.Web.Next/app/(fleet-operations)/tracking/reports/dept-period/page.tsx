import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";

import {
  DateRangeFields,
  TrackingNotice,
  TrackingReportTable,
  TrackingShell,
} from "@/app/(fleet-operations)/tracking/_components";
import {
  accessRestricted,
  getTrackingSession,
  hasTrackingAccess,
  parsePositiveInteger,
  queryValue,
  reportDateRange,
  sessionMessage,
} from "@/app/(fleet-operations)/tracking/_page";
import {
  getTrackingDeptPeriodReport,
  TrackingApiError,
} from "@/lib/api/fleet-operations/api-tracking";
import { getDepartments } from "@/lib/api/reference-data/api-departments";

async function TrackingDeptPeriodReportPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getTrackingSession();
  const problem = sessionMessage(session, "/tracking/reports/dept-period");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasTrackingAccess(session))
    return accessRestricted("Your profile does not include Vehicle Management access.");
  const query = await searchParams;
  const scope = queryValue(query.scope) || "one";
  const departmentCode = parsePositiveInteger(queryValue(query.departmentCode));
  const range = reportDateRange(queryValue(query.startDate), queryValue(query.endDate));
  const run = queryValue(query.run) === "1";
  try {
    const [departments, rows] = await Promise.all([
      getDepartments(),
      run && (scope === "all" || departmentCode)
        ? getTrackingDeptPeriodReport(
            departmentCode ?? 0,
            scope === "all",
            range.startDate,
            range.endDate,
          )
        : Promise.resolve([]),
    ]);
    return (
      <TrackingShell
        title="Tracking Report for a Dept / ALL Dept's"
        description="View tracking records by department and period."
      >
        <TrackingNotice query={query} />
        <form className="vehicle-status-maintenance-panel" method="get">
          <fieldset className="vehicle-search-options">
            <legend>Scope</legend>
            <label className="vehicle-checkbox-label">
              <input type="radio" name="scope" value="all" defaultChecked={scope === "all"} /> All
              departments
            </label>
            <label className="vehicle-checkbox-label">
              <input type="radio" name="scope" value="one" defaultChecked={scope !== "all"} /> Only
              one department
            </label>
          </fieldset>
          <div className="form-field">
            <label className="form-label" htmlFor="tracking-department">
              Department
            </label>
            <select
              className="form-select"
              id="tracking-department"
              name="departmentCode"
              defaultValue={departmentCode ?? ""}
            >
              <option value="">Select department</option>
              {departments.map((department) => (
                <option key={department.departmentCode} value={department.departmentCode}>
                  {department.description || "Unnamed department"} ({department.departmentCode})
                </option>
              ))}
            </select>
          </div>
          <DateRangeFields startDate={range.startDate} endDate={range.endDate} />
          <input name="run" type="hidden" value="1" />
          <div className="button-row">
            <button className="button button-primary" type="submit">
              Submit
            </button>
            <Link className="button button-secondary" href="/tracking/reports">
              Report menu
            </Link>
          </div>
        </form>
        {run && (scope === "all" || departmentCode) ? (
          <TrackingReportTable records={rows} title="Tracking report for department period" />
        ) : (
          <p className="muted-copy">
            Choose a scope, date range, and department when required, then submit the report.
          </p>
        )}
      </TrackingShell>
    );
  } catch (error) {
    return (
      <TrackingShell
        title="Tracking Report for a Dept / ALL Dept's"
        description="View tracking records by department and period."
      >
        <section className="vehicle-status-card" role="alert">
          <h2>
            {error instanceof TrackingApiError && error.reason === "unauthorized"
              ? "Your session has expired."
              : "The Tracking report service is temporarily unavailable."}
          </h2>
          <Link className="button button-primary" href="/tracking/reports/dept-period">
            Try again
          </Link>
        </section>
      </TrackingShell>
    );
  }
}

export default function TrackingDeptPeriodReportPage(
  props: Parameters<typeof TrackingDeptPeriodReportPageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <TrackingDeptPeriodReportPageContent {...props} />
    </Suspense>
  );
}
