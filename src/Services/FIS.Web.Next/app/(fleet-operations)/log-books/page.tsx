import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";

import {
  LogbookMenu,
  LogbookShell,
  LogbookTable,
} from "@/app/(fleet-operations)/log-books/_components";
import {
  accessRestricted,
  getLogbookSession,
  hasLogbookAccess,
  parsePositiveInteger,
  queryValue,
  sessionMessage,
} from "@/app/(fleet-operations)/log-books/_page";
import {
  DEFAULT_LOGBOOK_PAGE_SIZE,
  getLogbookPage,
  LogbookApiError,
} from "@/lib/api/fleet-operations/api-logbooks";

function pageHref(search: string, page: number) {
  const params = new URLSearchParams({ page: String(page) });
  if (search) params.set("search", search);
  return `/log-books?${params.toString()}`;
}

async function LogBooksPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getLogbookSession();
  const problem = sessionMessage(session, "/log-books");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasLogbookAccess(session))
    return accessRestricted("Your profile does not include Logbooks access.");

  const query = await searchParams;
  const search = queryValue(query.search).trim();
  const requestedPage = parsePositiveInteger(queryValue(query.page)) ?? 1;
  try {
    const logbookPage = await getLogbookPage({
      page: requestedPage,
      pageSize: DEFAULT_LOGBOOK_PAGE_SIZE,
      search,
    });
    const { items, page, pageSize, total, totalPages } = logbookPage;
    return (
      <LogbookShell
        title="Logbook Maintenance Menu"
        description="Manage vehicle logbook handouts while preserving the legacy workflow."
      >
        <LogbookMenu />
        <section
          className="vehicle-status-maintenance-panel"
          aria-labelledby="logbook-preview-title"
        >
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">
                {total} active record{total === 1 ? "" : "s"}
              </p>
              <h2 id="logbook-preview-title">Logbook Handout Preview</h2>
            </div>
          </div>
          <form className="vehicle-search-row" method="get">
            <input name="page" type="hidden" value="1" />
            <label className="sr-only" htmlFor="logbook-preview-search">
              Search logbook handouts
            </label>
            <input
              className="vehicle-search"
              id="logbook-preview-search"
              name="search"
              defaultValue={queryValue(query.search)}
              placeholder="Search GG, logbook range, receiver, comments"
            />
            <button className="button button-primary" type="submit">
              Filter
            </button>
            <Link className="button button-secondary" href="/log-books">
              Clear
            </Link>
          </form>
          <LogbookTable records={items} mode="preview" returnPath="/log-books" />
          {totalPages > 1 ? (
            <nav className="vehicle-pagination" aria-label="Logbook handout pages">
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
      </LogbookShell>
    );
  } catch (error) {
    const message =
      error instanceof LogbookApiError && error.reason === "unavailable"
        ? "The Logbooks service is temporarily unavailable. Please try again."
        : "Logbooks could not be loaded.";
    return (
      <LogbookShell
        title="Logbook Maintenance Menu"
        description="Manage vehicle logbook handouts while preserving the legacy workflow."
      >
        <section className="vehicle-status-card" role="alert">
          <h2>{message}</h2>
          <Link className="button button-secondary" href="/log-books">
            Try again
          </Link>
        </section>
      </LogbookShell>
    );
  }
}

export default function LogBooksPage(props: Parameters<typeof LogBooksPageContent>[0]) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LogBooksPageContent {...props} />
    </Suspense>
  );
}
