import Link from "next/link";

import {
  TrackingForm,
  TrackingNotice,
  TrackingShell,
  TrackingTable,
  VehicleLookup,
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
  getTracking,
  getTrackings,
  TrackingApiError,
  type TrackingRecord,
} from "@/lib/api/fleet-operations/api-tracking";
import { getVehicleOptions } from "@/lib/api/vehicles/api-vehicles";

export default async function TrackingMaintenancePage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getTrackingSession();
  const problem = sessionMessage(session, "/tracking/maintenance");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasTrackingAccess(session))
    return accessRestricted("Your profile does not include Vehicle Management access.");
  const query = await searchParams;
  const search = queryValue(query.search);
  const mode = queryValue(query.mode) || "GG";
  const requestedVmf = parsePositiveInteger(queryValue(query.vmfCode));
  const trackCode = parsePositiveInteger(queryValue(query.trackCode));
  const add = queryValue(query.add) === "1";
  try {
    const [vehicles, records, record] = await Promise.all([
      getVehicleOptions(),
      getTrackings(),
      trackCode ? getTracking(trackCode) : Promise.resolve(null),
    ]);
    const selectedRecord = record && !record.isDeleted ? record : null;
    const selectedVmfCode = selectedRecord?.vmfCode ?? requestedVmf;
    const visibleRecords = selectedVmfCode
      ? records.filter((item) => !item.isDeleted && item.vmfCode === selectedVmfCode)
      : [];
    const returnPath = `/tracking/maintenance?mode=${encodeURIComponent(mode)}&search=${encodeURIComponent(search)}${selectedVmfCode ? `&vmfCode=${selectedVmfCode}` : ""}`;
    return (
      <TrackingShell
        title="Tracking Maintenance"
        description="Capture and maintain tracking records for a selected vehicle."
      >
        <TrackingNotice query={query} />
        <section className="vehicle-status-maintenance-panel">
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Vehicle lookup</p>
              <h2>Find a vehicle before viewing tracking records</h2>
            </div>
          </div>
          <form method="get">
            <VehicleLookup
              options={vehicles}
              mode={mode}
              search={search}
              selectedVmfCode={selectedVmfCode}
            />
          </form>
          <div className="button-row">
            <Link className="button button-primary" href={`${returnPath}&add=1`}>
              Add tracking record
            </Link>
            <Link className="button button-secondary" href="/tracking">
              Main menu
            </Link>
          </div>
        </section>
        {selectedVmfCode ? (
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="tracking-history-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">
                  {visibleRecords.length} record{visibleRecords.length === 1 ? "" : "s"}
                </p>
                <h2 id="tracking-history-title">Tracking history</h2>
              </div>
            </div>
            <TrackingTable records={visibleRecords} />
          </section>
        ) : (
          <section className="vehicle-status-card">
            <p className="muted-copy">
              Search by GG or registration number, select a vehicle match, then view or add tracking
              records.
            </p>
          </section>
        )}
        {add || selectedRecord ? (
          <TrackingForm
            record={selectedRecord}
            vmfCode={selectedVmfCode ?? null}
            returnPath={returnPath}
          />
        ) : null}
      </TrackingShell>
    );
  } catch (error) {
    const message =
      error instanceof TrackingApiError && error.reason === "unauthorized"
        ? "Your session has expired."
        : "The Tracking service is temporarily unavailable.";
    return (
      <TrackingShell
        title="Tracking Maintenance"
        description="Capture and maintain tracking records for a selected vehicle."
      >
        <section className="vehicle-status-card" role="alert">
          <h2>{message}</h2>
          <Link className="button button-primary" href="/tracking/maintenance">
            Try again
          </Link>
        </section>
      </TrackingShell>
    );
  }
}
