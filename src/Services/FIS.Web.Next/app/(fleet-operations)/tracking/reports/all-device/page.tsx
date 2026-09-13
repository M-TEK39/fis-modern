import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";

import {
  DateRangeFields,
  TrackingNotice,
  TrackingReportPagination,
  TrackingReportTable,
  TrackingReportDateForm,
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
  getTrackingAllDevicesReport,
  TrackingApiError,
} from "@/lib/api/fleet-operations/api-tracking";

async function TrackingAllDeviceReportPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getTrackingSession();
  const problem = sessionMessage(session, "/tracking/reports/all-device");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasTrackingAccess(session))
    return accessRestricted("Your profile does not include Vehicle Management access.");
  const query = await searchParams;
  const range = reportDateRange(queryValue(query.startDate), queryValue(query.endDate));
  const page = parsePositiveInteger(queryValue(query.page)) ?? 1;
  const run = queryValue(query.run) === "1";
  try {
    const report = run
      ? await getTrackingAllDevicesReport(range.startDate, range.endDate, { page })
      : null;
    return (
      <TrackingShell
        title="Tracking Report for ALL Tracking Device"
        description="View tracker activity across all tracking devices."
      >
        <TrackingNotice query={query} />
        <TrackingReportDateForm startDate={range.startDate} endDate={range.endDate} />
        {run ? (
          <>
            <TrackingReportTable
              records={report?.items ?? []}
              title="Tracking report for all devices"
            />
            {report ? (
              <TrackingReportPagination
                page={report}
                query={query}
                routePath="/tracking/reports/all-device"
                label="Tracking report for all devices"
              />
            ) : null}
          </>
        ) : (
          <p className="muted-copy">Choose a date range, then submit the report.</p>
        )}
      </TrackingShell>
    );
  } catch (error) {
    return (
      <TrackingShell
        title="Tracking Report for ALL Tracking Device"
        description="View tracker activity across all tracking devices."
      >
        <section className="vehicle-status-card" role="alert">
          <h2>
            {error instanceof TrackingApiError && error.reason === "unauthorized"
              ? "Your session has expired."
              : "The Tracking report service is temporarily unavailable."}
          </h2>
          <Link className="button button-primary" href="/tracking/reports/all-device">
            Try again
          </Link>
        </section>
      </TrackingShell>
    );
  }
}

export default function TrackingAllDeviceReportPage(
  props: Parameters<typeof TrackingAllDeviceReportPageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <TrackingAllDeviceReportPageContent {...props} />
    </Suspense>
  );
}
