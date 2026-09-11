import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";

import {
  DateRangeFields,
  TrackingNotice,
  TrackingReportTable,
  TrackingReportDateForm,
  TrackingShell,
} from "@/app/(fleet-operations)/tracking/_components";
import {
  accessRestricted,
  getTrackingSession,
  hasTrackingAccess,
  queryValue,
  reportDateRange,
  sessionMessage,
} from "@/app/(fleet-operations)/tracking/_page";
import {
  getTrackingInstallPeriodReport,
  TrackingApiError,
} from "@/lib/api/fleet-operations/api-tracking";

async function TrackingInstallPeriodReportPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getTrackingSession();
  const problem = sessionMessage(session, "/tracking/reports/install-period");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasTrackingAccess(session))
    return accessRestricted("Your profile does not include Vehicle Management access.");
  const query = await searchParams;
  const range = reportDateRange(queryValue(query.startDate), queryValue(query.endDate));
  const run = queryValue(query.run) === "1";
  try {
    const rows = run ? await getTrackingInstallPeriodReport(range.startDate, range.endDate) : [];
    return (
      <TrackingShell
        title="Tracking Report for an Install Period"
        description="View tracker installations captured during a selected period."
      >
        <TrackingNotice query={query} />
        <TrackingReportDateForm startDate={range.startDate} endDate={range.endDate} />
        {run ? (
          <TrackingReportTable records={rows} title="Tracking report for install period" />
        ) : (
          <p className="muted-copy">Choose a date range, then submit the report.</p>
        )}
      </TrackingShell>
    );
  } catch (error) {
    return (
      <TrackingShell
        title="Tracking Report for an Install Period"
        description="View tracker installations captured during a selected period."
      >
        <section className="vehicle-status-card" role="alert">
          <h2>
            {error instanceof TrackingApiError && error.reason === "unauthorized"
              ? "Your session has expired."
              : "The Tracking report service is temporarily unavailable."}
          </h2>
          <Link className="button button-primary" href="/tracking/reports/install-period">
            Try again
          </Link>
        </section>
      </TrackingShell>
    );
  }
}

export default function TrackingInstallPeriodReportPage(
  props: Parameters<typeof TrackingInstallPeriodReportPageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <TrackingInstallPeriodReportPageContent {...props} />
    </Suspense>
  );
}
