import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";

import {
  LogsheetMenu,
  LogsheetShell,
  LogsheetTable,
} from "@/app/(fleet-operations)/log-sheets/_components";
import {
  accessRestricted,
  getLogsheetSession,
  hasLogsheetAccess,
  hasLogsheetManagerAccess,
  sessionMessage,
} from "@/app/(fleet-operations)/log-sheets/_page";
import {
  DEFAULT_LOGSHEET_PAGE_SIZE,
  getLogsheetsPage,
  LogsheetApiError,
} from "@/lib/api/fleet-operations/api-logsheets";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function pageValue(value: string | string[] | undefined) {
  const candidate = Number(Array.isArray(value) ? value[0] : value);
  return Number.isInteger(candidate) && candidate > 0 ? candidate : 1;
}

function pageHref(page: number) {
  return page > 1 ? `/log-sheets?page=${page}` : "/log-sheets";
}

async function LogsheetMenuPageContent({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  const session = await getLogsheetSession();
  const problem = sessionMessage(session, "/log-sheets");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasLogsheetAccess(session))
    return accessRestricted("Your profile does not include Log Sheets access.");

  try {
    const query = await searchParams;
    const recordPage = await getLogsheetsPage({
      page: pageValue(query.page),
      pageSize: DEFAULT_LOGSHEET_PAGE_SIZE,
    });
    return (
      <LogsheetShell
        title="Logsheet Maintenance"
        description="Capture and manage monthly vehicle usage logs."
      >
        <LogsheetMenu canManage={hasLogsheetManagerAccess(session)} />
        <div className="button-row">
          <Link className="button button-secondary" href="/manuals">
            User manuals
          </Link>
        </div>
        <section
          className="vehicle-status-maintenance-panel"
          aria-labelledby="recent-logsheets-title"
        >
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Recent activity</p>
              <h2 id="recent-logsheets-title">Latest logsheets</h2>
            </div>
            <span className="muted-copy">
              {recordPage.total} record{recordPage.total === 1 ? "" : "s"}
            </span>
          </div>
          <LogsheetTable
            records={recordPage.items}
            mode="preview"
            returnPath={pageHref(recordPage.page)}
          />
          {recordPage.totalPages > 1 ? (
            <nav className="vehicle-pagination" aria-label="Logsheet pages">
              {recordPage.page <= 1 ? (
                <span
                  className="vehicle-pagination-button vehicle-pagination-disabled"
                  aria-disabled="true"
                >
                  Previous
                </span>
              ) : (
                <Link className="vehicle-pagination-button" href={pageHref(recordPage.page - 1)}>
                  Previous
                </Link>
              )}
              <span className="vehicle-pagination-meta" aria-live="polite">
                Page {recordPage.page} of {recordPage.totalPages}
              </span>
              {recordPage.page >= recordPage.totalPages ? (
                <span
                  className="vehicle-pagination-button vehicle-pagination-disabled"
                  aria-disabled="true"
                >
                  Next
                </span>
              ) : (
                <Link className="vehicle-pagination-button" href={pageHref(recordPage.page + 1)}>
                  Next
                </Link>
              )}
            </nav>
          ) : null}
        </section>
      </LogsheetShell>
    );
  } catch (error) {
    return (
      <LogsheetShell
        title="Logsheet Maintenance"
        description="Capture and manage monthly vehicle usage logs."
      >
        <LogsheetMenu canManage={hasLogsheetManagerAccess(session)} />
        <section className="vehicle-status-card" role="alert">
          <h2>
            {error instanceof LogsheetApiError
              ? "The Logsheet service is temporarily unavailable."
              : "Logsheets could not be loaded."}
          </h2>
          <p className="muted-copy">
            The maintenance menu is available while the service is restored.
          </p>
        </section>
      </LogsheetShell>
    );
  }
}

export default function LogsheetMenuPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LogsheetMenuPageContent searchParams={searchParams} />
    </Suspense>
  );
}
