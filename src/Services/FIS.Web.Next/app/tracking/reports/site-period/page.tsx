import Link from "next/link";

import {
  DateRangeFields,
  TrackingNotice,
  TrackingReportTable,
  TrackingShell,
} from "@/app/tracking/_components";
import {
  accessRestricted,
  getTrackingSession,
  hasTrackingAccess,
  parsePositiveInteger,
  queryValue,
  reportDateRange,
  sessionMessage,
} from "@/app/tracking/_page";
import { getTrackingSitePeriodReport, TrackingApiError } from "@/lib/api-tracking";
import { getSites } from "@/lib/api-sites";

export default async function TrackingSitePeriodReportPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getTrackingSession();
  const problem = sessionMessage(session, "/tracking/reports/site-period");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasTrackingAccess(session))
    return accessRestricted("Your profile does not include Vehicle Management access.");
  const query = await searchParams;
  const scope = queryValue(query.scope) || "one";
  const siteCode = parsePositiveInteger(queryValue(query.siteCode));
  const range = reportDateRange(queryValue(query.startDate), queryValue(query.endDate));
  const run = queryValue(query.run) === "1";
  try {
    const [sites, rows] = await Promise.all([
      getSites(),
      run && (scope === "all" || siteCode)
        ? getTrackingSitePeriodReport(
            siteCode ?? 0,
            scope === "all",
            range.startDate,
            range.endDate,
          )
        : Promise.resolve([]),
    ]);
    return (
      <TrackingShell
        title="Tracking Report for a Site / ALL Sites"
        description="View tracking records by site and period."
      >
        <TrackingNotice query={query} />
        <form className="vehicle-status-maintenance-panel" method="get">
          <fieldset className="vehicle-search-options">
            <legend>Scope</legend>
            <label className="vehicle-checkbox-label">
              <input type="radio" name="scope" value="all" defaultChecked={scope === "all"} /> All
              sites
            </label>
            <label className="vehicle-checkbox-label">
              <input type="radio" name="scope" value="one" defaultChecked={scope !== "all"} /> Only
              one site
            </label>
          </fieldset>
          <div className="form-field">
            <label className="form-label" htmlFor="tracking-site">
              Site
            </label>
            <select
              className="form-select"
              id="tracking-site"
              name="siteCode"
              defaultValue={siteCode ?? ""}
            >
              <option value="">Select site</option>
              {sites.map((site) => (
                <option key={site.siteCode} value={site.siteCode}>
                  {site.description || "Unnamed site"} ({site.siteCode})
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
        {run && (scope === "all" || siteCode) ? (
          <TrackingReportTable records={rows} title="Tracking report for site period" />
        ) : (
          <p className="muted-copy">
            Choose a scope, date range, and site when required, then submit the report.
          </p>
        )}
      </TrackingShell>
    );
  } catch (error) {
    return (
      <TrackingShell
        title="Tracking Report for a Site / ALL Sites"
        description="View tracking records by site and period."
      >
        <section className="vehicle-status-card" role="alert">
          <h2>
            {error instanceof TrackingApiError && error.reason === "unauthorized"
              ? "Your session has expired."
              : "The Tracking report service is temporarily unavailable."}
          </h2>
          <Link className="button button-primary" href="/tracking/reports/site-period">
            Try again
          </Link>
        </section>
      </TrackingShell>
    );
  }
}
