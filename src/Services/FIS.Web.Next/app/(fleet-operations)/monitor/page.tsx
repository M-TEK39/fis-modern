import Link from "next/link";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import {
  MonitorMenu,
  MonitorNotice,
  MonitorShell,
  MonitorTable,
} from "@/app/(fleet-operations)/monitor/_components";
import {
  accessRestricted,
  getMonitorSession,
  hasMonitorAccess,
  parsePositiveInteger,
  queryValue,
  sessionMessage,
} from "@/app/(fleet-operations)/monitor/_page";
import {
  DEFAULT_MONITOR_PAGE_SIZE,
  getMonitorPage,
  MonitorApiError,
} from "@/lib/api/fleet-operations/api-monitor";

type MonitorSearchParams = Record<string, string | string[] | undefined>;

function pageHref(query: MonitorSearchParams, page: number) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (key === "page" || value === undefined) continue;
    for (const item of Array.isArray(value) ? value : [value]) params.append(key, item);
  }
  if (page > 1) params.set("page", String(page));
  const queryString = params.toString();
  return queryString ? `/monitor?${queryString}` : "/monitor";
}

async function MonitorPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<MonitorSearchParams> }>) {
  const session = await getMonitorSession();
  const problem = sessionMessage(session, "/monitor");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasMonitorAccess(session))
    return accessRestricted("Your profile does not include Monitor access.");

  const query = await searchParams;
  const search = queryValue(query.search).trim();
  const requestedPage = parsePositiveInteger(queryValue(query.page)) ?? 1;
  try {
    const monitorPage = await getMonitorPage({
      page: requestedPage,
      pageSize: DEFAULT_MONITOR_PAGE_SIZE,
      search,
    });
    return (
      <MonitorShell
        title="Monitor Maintenance Menu"
        description="Capture and manage Call Centre monitoring inquiries while preserving the legacy workflow."
      >
        <MonitorNotice query={query} />
        <MonitorMenu />
        <section
          className="vehicle-status-maintenance-panel"
          aria-labelledby="monitor-preview-title"
        >
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">{monitorPage.total} active inquiries</p>
              <h2 id="monitor-preview-title">Monitor Inquiry Preview</h2>
            </div>
          </div>
          <form className="vehicle-search-row" method="get">
            <label className="sr-only" htmlFor="monitor-preview-search">
              Search inquiries
            </label>
            <input
              className="vehicle-search"
              id="monitor-preview-search"
              name="search"
              defaultValue={queryValue(query.search)}
              placeholder="Search reference, vehicle, driver, type"
            />
            <input type="hidden" name="page" value="1" />
            <button className="button button-primary" type="submit">
              Filter
            </button>
            <Link className="button button-secondary" href="/monitor">
              Clear
            </Link>
          </form>
          <MonitorTable records={monitorPage.items} />
          {monitorPage.totalPages > 1 ? (
            <nav className="vehicle-pagination" aria-label="Monitor inquiry pages">
              {monitorPage.page > 1 ? (
                <Link
                  className="vehicle-pagination-button"
                  href={pageHref(query, monitorPage.page - 1)}
                >
                  Previous
                </Link>
              ) : (
                <span className="vehicle-pagination-button vehicle-pagination-disabled">
                  Previous
                </span>
              )}
              <span aria-live="polite">
                Page {monitorPage.page} of {monitorPage.totalPages}
              </span>
              {monitorPage.page < monitorPage.totalPages ? (
                <Link
                  className="vehicle-pagination-button"
                  href={pageHref(query, monitorPage.page + 1)}
                >
                  Next
                </Link>
              ) : (
                <span className="vehicle-pagination-button vehicle-pagination-disabled">Next</span>
              )}
            </nav>
          ) : null}
        </section>
      </MonitorShell>
    );
  } catch (error) {
    return (
      <MonitorShell
        title="Monitor Maintenance Menu"
        description="Capture and manage Call Centre monitoring inquiries while preserving the legacy workflow."
      >
        <MonitorMenu />
        <section className="vehicle-status-card" role="alert">
          <h2>
            {error instanceof MonitorApiError && error.reason === "unavailable"
              ? "The Monitor service is temporarily unavailable."
              : "Monitor inquiries could not be loaded."}
          </h2>
          <p className="muted-copy">
            The maintenance menu is available while the service is restored.
          </p>
          <Link className="button button-primary" href="/monitor">
            Try again
          </Link>
        </section>
      </MonitorShell>
    );
  }
}

export default function MonitorPage(props: Parameters<typeof MonitorPageContent>[0]) {
  return (
    <StreamedRoute>
      <MonitorPageContent {...props} />
    </StreamedRoute>
  );
}
