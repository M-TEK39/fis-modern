import Link from "next/link";

import {
  JobCardTable,
  JobCardMenu,
  hasJobCardAccess,
  hasRole,
} from "@/app/(fleet-operations)/job-cards/_components";
import {
  accessRestricted,
  getJobCardSession,
  JobCardPageBoundary,
  jobCardPageHref,
  queryPage,
  querySearchType,
  queryValue,
  sessionMessage,
} from "@/app/(fleet-operations)/job-cards/_page";
import {
  DEFAULT_JOB_CARD_PAGE_SIZE,
  getJobCardsPage,
  JobCardApiError,
} from "@/lib/api/fleet-operations/api-job-cards";

export default function JobCardsPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <JobCardPageBoundary>
      <JobCardsContent searchParams={searchParams} />
    </JobCardPageBoundary>
  );
}

async function JobCardsContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getJobCardSession();
  const problem = sessionMessage(session, "/job-cards");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasJobCardAccess(session.accessLevel, session.roles))
    return accessRestricted("Your profile does not include Job Card access.");

  const query = await searchParams;
  const search = queryValue(query.search);
  const requestedStatus = queryValue(query.status);
  const status = ["all", "open", "closed"].includes(requestedStatus) ? requestedStatus : "all";
  const searchType = querySearchType(query.mode);
  const page = queryPage(query.page);
  const statusCodes =
    status === "open" ? [0, 1, 2, 3, 4, 6] : status === "closed" ? [5] : undefined;
  try {
    const pageData = await getJobCardsPage({
      page,
      pageSize: DEFAULT_JOB_CARD_PAGE_SIZE,
      search,
      searchType,
      statusCodes,
    });
    const canCapturer =
      hasRole(session.roles, "capturer") || hasJobCardAccess(session.accessLevel, session.roles);
    const canAuthorizer =
      hasRole(session.roles, "authorizer") || hasJobCardAccess(session.accessLevel, session.roles);
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="job-cards-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Fleet maintenance</p>
              <h1 id="job-cards-title">Job Cards</h1>
              <p>Capture, authorize, close, cancel, print, and review vehicle job cards.</p>
            </div>
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
          </header>
          <JobCardMenu canCapturer={canCapturer} canAuthorizer={canAuthorizer} />
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="job-card-snapshot-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">
                  {pageData.totalRecords} record{pageData.totalRecords === 1 ? "" : "s"}
                </p>
                <h2 id="job-card-snapshot-title">All Job Cards Snapshot</h2>
              </div>
            </div>
            <form className="vehicle-search-row" method="get">
              <input type="hidden" name="page" value="1" />
              <fieldset className="vehicle-search-options">
                <legend>Find by</legend>
                <label className="vehicle-checkbox-label">
                  <input type="radio" name="mode" value="GG" defaultChecked={searchType === "GG"} />{" "}
                  GG
                </label>
                <label className="vehicle-checkbox-label">
                  <input type="radio" name="mode" value="GP" defaultChecked={searchType === "GP"} />{" "}
                  GP
                </label>
              </fieldset>
              <label className="sr-only" htmlFor="job-card-search">
                Search job cards by GG, GP, or job card number
              </label>
              <input
                className="vehicle-search"
                id="job-card-search"
                name="search"
                defaultValue={search}
                placeholder="GG, GP, or job card number"
              />
              <select
                className="form-select"
                name="status"
                defaultValue={status || "all"}
                aria-label="Filter job cards by status"
              >
                <option value="all">All job cards</option>
                <option value="open">Open job cards</option>
                <option value="closed">Closed job cards</option>
              </select>
              <button className="button button-primary" type="submit">
                Filter
              </button>
              <Link className="button button-secondary" href="/job-cards">
                Clear
              </Link>
            </form>
            <JobCardTable
              cards={pageData.items}
              mode="list"
              returnPath="/job-cards/list"
              currentUserCode={Number(session.userAccessCode) || null}
              page={pageData.page}
              totalPages={pageData.totalPages}
              pageHref={(nextPage) => jobCardPageHref("/job-cards", query, nextPage)}
            />
          </section>
        </section>
      </main>
    );
  } catch (error) {
    const message =
      error instanceof JobCardApiError && error.reason === "unavailable"
        ? "The Job Cards service is temporarily unavailable. Please try again."
        : "Job cards could not be loaded.";
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <h2>{message}</h2>
          <Link className="button button-secondary" href="/job-cards">
            Try again
          </Link>
        </section>
      </main>
    );
  }
}
