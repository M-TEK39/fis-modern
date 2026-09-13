import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";

import {
  DateRangeFields,
  TrackingNotice,
  TrackingReportPagination,
  TrackingReportTable,
  TrackingShell,
  VehicleLookup,
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
  getTrackingOneVehicleReport,
  TrackingApiError,
} from "@/lib/api/fleet-operations/api-tracking";
import { getVehicleOptions } from "@/lib/api/vehicles/api-vehicles";

async function TrackingOneVehicleReportPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getTrackingSession();
  const problem = sessionMessage(session, "/tracking/reports/one-vehicle");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasTrackingAccess(session))
    return accessRestricted("Your profile does not include Vehicle Management access.");
  const query = await searchParams;
  const search = queryValue(query.search);
  const mode = queryValue(query.mode) || "GG";
  const vmfCode = parsePositiveInteger(queryValue(query.vmfCode));
  const range = reportDateRange(queryValue(query.startDate), queryValue(query.endDate));
  const page = parsePositiveInteger(queryValue(query.page)) ?? 1;
  try {
    const [vehicles, report] = await Promise.all([
      getVehicleOptions(),
      vmfCode
        ? getTrackingOneVehicleReport(vmfCode, range.startDate, range.endDate, { page })
        : Promise.resolve(null),
    ]);
    return (
      <TrackingShell
        title="Tracking Report for ONE Vehicle"
        description="View tracking history for a single vehicle."
      >
        <TrackingNotice query={query} />
        <form className="vehicle-status-maintenance-panel" method="get">
          <VehicleLookup options={vehicles} mode={mode} search={search} selectedVmfCode={vmfCode} />
          <DateRangeFields startDate={range.startDate} endDate={range.endDate} />
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
            <TrackingReportTable records={report.items} title="Tracking report for one vehicle" />
            <TrackingReportPagination
              page={report}
              query={query}
              routePath="/tracking/reports/one-vehicle"
              label="Tracking report for one vehicle"
            />
          </>
        ) : (
          <p className="muted-copy">
            Search by GG or registration number, select a vehicle match, and submit.
          </p>
        )}
      </TrackingShell>
    );
  } catch (error) {
    return (
      <TrackingShell
        title="Tracking Report for ONE Vehicle"
        description="View tracking history for a single vehicle."
      >
        <section className="vehicle-status-card" role="alert">
          <h2>
            {error instanceof TrackingApiError && error.reason === "unauthorized"
              ? "Your session has expired."
              : "The Tracking report service is temporarily unavailable."}
          </h2>
          <Link className="button button-primary" href="/tracking/reports/one-vehicle">
            Try again
          </Link>
        </section>
      </TrackingShell>
    );
  }
}

export default function TrackingOneVehicleReportPage(
  props: Parameters<typeof TrackingOneVehicleReportPageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <TrackingOneVehicleReportPageContent {...props} />
    </Suspense>
  );
}
