import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

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
  DEFAULT_TRACKING_PAGE_SIZE,
  getTracking,
  getTrackingPage,
  TrackingApiError,
  type TrackingRecord,
} from "@/lib/api/fleet-operations/api-tracking";
import { getVehicleOptions } from "@/lib/api/vehicles/api-vehicles";

const routePath = "/tracking/maintenance";

function historyPath(search: string, mode: string, vmfCode: number | null, page: number) {
  const params = new URLSearchParams({ mode });
  if (search) params.set("search", search);
  if (vmfCode) params.set("vmfCode", String(vmfCode));
  if (page > 1) params.set("page", String(page));
  return `${routePath}?${params.toString()}`;
}

function TrackingHistoryPagination({
  search,
  mode,
  vmfCode,
  page,
  totalPages,
}: Readonly<{
  search: string;
  mode: string;
  vmfCode: number;
  page: number;
  totalPages: number;
}>) {
  if (totalPages <= 1) return null;

  return (
    <nav className="vehicle-pagination" aria-label="Tracking history pages">
      {page > 1 ? (
        <Link
          className="vehicle-pagination-button"
          href={historyPath(search, mode, vmfCode, page - 1)}
          aria-label={`Go to tracking history page ${page - 1}`}
        >
          Previous
        </Link>
      ) : (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Previous
        </span>
      )}
      <span className="vehicle-pagination-meta" aria-live="polite">
        Page {page} of {totalPages}
      </span>
      {page < totalPages ? (
        <Link
          className="vehicle-pagination-button"
          href={historyPath(search, mode, vmfCode, page + 1)}
          aria-label={`Go to tracking history page ${page + 1}`}
        >
          Next
        </Link>
      ) : (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Next
        </span>
      )}
    </nav>
  );
}

const TrackingMaintenancePageContent = renderTrackingMaintenancePageContent;

async function renderTrackingMaintenancePageContent({
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
  const requestedPage = parsePositiveInteger(queryValue(query.page)) ?? 1;
  const add = queryValue(query.add) === "1";
  const retryPath = historyPath(search, mode, requestedVmf, requestedPage);
  try {
    const [vehicles, record] = await Promise.all([
      getVehicleOptions(),
      trackCode ? getTracking(trackCode) : Promise.resolve(null),
    ]);
    const selectedRecord = record && !record.isDeleted ? record : null;
    const selectedVmfCode = selectedRecord?.vmfCode ?? requestedVmf;
    const trackingPage = selectedVmfCode
      ? await getTrackingPage({
          page: requestedPage,
          pageSize: DEFAULT_TRACKING_PAGE_SIZE,
          vmfCode: selectedVmfCode,
        })
      : null;
    const currentPage = trackingPage?.page ?? requestedPage;
    const returnPath = historyPath(search, mode, selectedVmfCode, currentPage);
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
                  {trackingPage?.total ?? 0} record{trackingPage?.total === 1 ? "" : "s"}
                </p>
                <h2 id="tracking-history-title">Tracking history</h2>
              </div>
            </div>
            <TrackingTable records={trackingPage?.items ?? []} returnPath={returnPath} />
            {trackingPage ? (
              <>
                <TrackingHistoryPagination
                  search={search}
                  mode={mode}
                  vmfCode={selectedVmfCode}
                  page={trackingPage.page}
                  totalPages={trackingPage.totalPages}
                />
                <div className="pagination-meta">
                  Total records: {trackingPage.total} | Page size: {trackingPage.pageSize}
                </div>
              </>
            ) : null}
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
          <Link className="button button-primary" href={retryPath}>
            Try again
          </Link>
        </section>
      </TrackingShell>
    );
  }
}

export default function TrackingMaintenancePage(
  props: Parameters<typeof TrackingMaintenancePageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <TrackingMaintenancePageContent {...props} />
    </Suspense>
  );
}
