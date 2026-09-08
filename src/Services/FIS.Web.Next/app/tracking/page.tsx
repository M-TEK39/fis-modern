import Link from "next/link";

import {
  TrackingMenu,
  TrackingNotice,
  TrackingReportTable,
  TrackingShell,
} from "@/app/tracking/_components";
import {
  accessRestricted,
  getTrackingSession,
  hasTrackingAccess,
  queryValue,
  sessionMessage,
} from "@/app/tracking/_page";
import { getTrackings, TrackingApiError } from "@/lib/api-tracking";

export default async function TrackingPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getTrackingSession();
  const problem = sessionMessage(session, "/tracking");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasTrackingAccess(session))
    return accessRestricted("Your profile does not include Vehicle Management access.");
  const query = await searchParams;
  const search = queryValue(query.search).toLocaleLowerCase();
  try {
    const records = (await getTrackings()).filter((record) => !record.isDeleted);
    const filtered = records.filter(
      (record) =>
        !search ||
        [
          record.trackCode,
          record.vmfCode,
          record.trackNumber,
          record.fleetNumber,
          record.registrationNumber,
          record.status,
          record.type,
        ].some((value) =>
          String(value ?? "")
            .toLocaleLowerCase()
            .includes(search),
        ),
    );
    return (
      <TrackingShell
        title="Tracking Maintenance Menu"
        description="Capture and maintain tracking records while preserving the legacy workflow."
      >
        <TrackingNotice query={query} />
        <TrackingMenu />
        <section
          className="vehicle-status-maintenance-panel"
          aria-labelledby="tracking-preview-title"
        >
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">
                {filtered.length} of {records.length} active records
              </p>
              <h2 id="tracking-preview-title">Tracking Preview</h2>
            </div>
          </div>
          <form className="vehicle-search-row" method="get">
            <label className="sr-only" htmlFor="tracking-preview-search">
              Search tracking records
            </label>
            <input
              className="vehicle-search"
              id="tracking-preview-search"
              name="search"
              defaultValue={queryValue(query.search)}
              placeholder="Search tracker, vehicle, status, type"
            />
            <button className="button button-primary" type="submit">
              Filter
            </button>
            <Link className="button button-secondary" href="/tracking">
              Clear
            </Link>
          </form>
          <TrackingReportTable records={filtered.slice(0, 100)} />
        </section>
      </TrackingShell>
    );
  } catch (error) {
    return (
      <TrackingShell
        title="Tracking Maintenance Menu"
        description="Capture and maintain tracking records while preserving the legacy workflow."
      >
        <TrackingMenu />
        <section className="vehicle-status-card" role="alert">
          <h2>
            {error instanceof TrackingApiError && error.reason === "unavailable"
              ? "The Tracking service is temporarily unavailable."
              : "Tracking records could not be loaded."}
          </h2>
          <Link className="button button-primary" href="/tracking">
            Try again
          </Link>
        </section>
      </TrackingShell>
    );
  }
}
