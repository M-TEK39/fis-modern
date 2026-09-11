import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";

import {
  TrackingMenu,
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
  sessionMessage,
} from "@/app/(fleet-operations)/tracking/_page";
import {
  DEFAULT_TRACKING_PAGE_SIZE,
  getTrackingPage,
  TrackingApiError,
} from "@/lib/api/fleet-operations/api-tracking";

function pageHref(search: string, page: number) {
  const params = new URLSearchParams({ page: String(page) });
  if (search) params.set("search", search);
  return `/tracking?${params.toString()}`;
}

async function TrackingPageContent({
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
  const search = queryValue(query.search).trim();
  const requestedPage = parsePositiveInteger(queryValue(query.page)) ?? 1;
  try {
    const trackingPage = await getTrackingPage({
      page: requestedPage,
      pageSize: DEFAULT_TRACKING_PAGE_SIZE,
      search,
    });
    const { items, page, pageSize, total, totalPages } = trackingPage;
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
              <p className="eyebrow">{total} active records</p>
              <h2 id="tracking-preview-title">Tracking Preview</h2>
            </div>
          </div>
          <form className="vehicle-search-row" method="get">
            <input name="page" type="hidden" value="1" />
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
          <TrackingReportTable records={items} />
          {totalPages > 1 ? (
            <nav className="vehicle-pagination" aria-label="Tracking preview pages">
              {page > 1 ? (
                <Link className="vehicle-pagination-button" href={pageHref(search, page - 1)}>
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
                <Link className="vehicle-pagination-button" href={pageHref(search, page + 1)}>
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
          ) : null}
          <div className="pagination-meta">
            Total records: {total} | Page size: {pageSize}
          </div>
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

export default function TrackingPage(props: Parameters<typeof TrackingPageContent>[0]) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <TrackingPageContent {...props} />
    </Suspense>
  );
}
