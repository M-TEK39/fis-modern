import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";

import {
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
  sessionMessage,
} from "@/app/(fleet-operations)/tracking/_page";
import {
  getTrackingOneDeviceReport,
  TrackingApiError,
} from "@/lib/api/fleet-operations/api-tracking";

async function TrackingOneDeviceReportPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getTrackingSession();
  const problem = sessionMessage(session, "/tracking/reports/one-device");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasTrackingAccess(session))
    return accessRestricted("Your profile does not include Vehicle Management access.");
  const query = await searchParams;
  const device = queryValue(query.device);
  const page = parsePositiveInteger(queryValue(query.page)) ?? 1;
  try {
    const report = device.trim() ? await getTrackingOneDeviceReport(device.trim(), { page }) : null;
    return (
      <TrackingShell
        title="Tracking Report for ONE Tracking Device"
        description="View tracking records for a single tracking device."
      >
        <TrackingNotice query={query} />
        <form className="vehicle-status-maintenance-panel" method="get">
          <div className="form-field">
            <label className="form-label" htmlFor="tracking-device">
              Tracker number
            </label>
            <input
              className="form-input"
              id="tracking-device"
              name="device"
              defaultValue={device}
              required
            />
          </div>
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
            <TrackingReportTable records={report.items} title="Tracking report for one device" />
            <TrackingReportPagination
              page={report}
              query={query}
              routePath="/tracking/reports/one-device"
              label="Tracking report for one device"
            />
          </>
        ) : (
          <p className="muted-copy">Enter a tracker number to view its history.</p>
        )}
      </TrackingShell>
    );
  } catch (error) {
    return (
      <TrackingShell
        title="Tracking Report for ONE Tracking Device"
        description="View tracking records for a single tracking device."
      >
        <section className="vehicle-status-card" role="alert">
          <h2>
            {error instanceof TrackingApiError && error.reason === "unauthorized"
              ? "Your session has expired."
              : "The Tracking report service is temporarily unavailable."}
          </h2>
          <Link className="button button-primary" href="/tracking/reports/one-device">
            Try again
          </Link>
        </section>
      </TrackingShell>
    );
  }
}

export default function TrackingOneDeviceReportPage(
  props: Parameters<typeof TrackingOneDeviceReportPageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <TrackingOneDeviceReportPageContent {...props} />
    </Suspense>
  );
}
