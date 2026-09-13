import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";

import {
  DateRangeFields,
  TrackingNotice,
  TrackingReportPagination,
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
  getTrackingSitePeriodReport,
  TrackingApiError,
} from "@/lib/api/fleet-operations/api-tracking";
import TrackingScopeFieldset from "@/components/ui/tracking-scope-fieldset";
import { getSites } from "@/lib/api/reference-data/api-sites";

const TrackingSitePeriodReportPageContent = renderTrackingSitePeriodReportPageContent;

async function renderTrackingSitePeriodReportPageContent({
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
  const page = parsePositiveInteger(queryValue(query.page)) ?? 1;
  const run = queryValue(query.run) === "1";
  try {
    const [sites, report] = await Promise.all([
      getSites(),
      run && (scope === "all" || siteCode)
        ? getTrackingSitePeriodReport(
            siteCode ?? 0,
            scope === "all",
            range.startDate,
            range.endDate,
            { page },
          )
        : Promise.resolve(null),
    ]);
    return (
      <TrackingShell
        title="Tracking Report for a Site / ALL Sites"
        description="View tracking records by site and period."
      >
        <TrackingNotice query={query} />
        <form className="vehicle-status-maintenance-panel" method="get">
          <TrackingScopeFieldset
            selectedScope={scope}
            allLabel="All sites"
            oneLabel="Only one site"
          />
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
        {report ? (
          <>
            <TrackingReportTable records={report.items} title="Tracking report for site period" />
            <TrackingReportPagination
              page={report}
              query={query}
              routePath="/tracking/reports/site-period"
              label="Tracking report for site period"
            />
          </>
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

export default function TrackingSitePeriodReportPage(
  props: Parameters<typeof TrackingSitePeriodReportPageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <TrackingSitePeriodReportPageContent {...props} />
    </Suspense>
  );
}
