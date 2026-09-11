import Link from "next/link";

import {
  JobCardTable,
  hasJobCardAccess,
  hasRole,
} from "@/app/(fleet-operations)/job-cards/_components";
import {
  accessRestricted,
  JobCardPageBoundary,
  getJobCardSession,
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

export default function AuthorizerDashboardPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <JobCardPageBoundary>
      <AuthorizerDashboardContent searchParams={searchParams} />
    </JobCardPageBoundary>
  );
}

async function AuthorizerDashboardContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getJobCardSession();
  const problem = sessionMessage(session, "/job-cards/authorizer-dashboard");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (
    !hasRole(session.roles, "authorizer") &&
    !hasJobCardAccess(session.accessLevel, session.roles)
  )
    return accessRestricted("Your profile does not include Job Card authorizer access.");
  const query = await searchParams;
  const search = queryValue(query.search);
  const mode = querySearchType(query.mode);
  const page = queryPage(query.page);
  try {
    const pageData = await getJobCardsPage({
      page,
      pageSize: DEFAULT_JOB_CARD_PAGE_SIZE,
      search,
      searchType: mode,
      statusCodes: [1, 2],
    });
    const tableReturnPath = jobCardPageHref(
      "/job-cards/authorizer-dashboard",
      { ...query, id: undefined },
      pageData.page,
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="authorizer-dashboard-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Job Cards</p>
              <h1 id="authorizer-dashboard-title">Authorizer Dashboard</h1>
              <p>Review current job cards assigned to authorizers.</p>
            </div>
            <Link className="button button-secondary" href="/job-cards">
              Back
            </Link>
          </header>
          <section className="vehicle-status-maintenance-panel">
            <form className="vehicle-search-row" method="get">
              <input type="hidden" name="page" value="1" />
              <fieldset className="vehicle-search-options">
                <legend>Find by</legend>
                <label className="vehicle-checkbox-label">
                  <input type="radio" name="mode" value="GG" defaultChecked={mode !== "GP"} /> GG
                </label>
                <label className="vehicle-checkbox-label">
                  <input type="radio" name="mode" value="GP" defaultChecked={mode === "GP"} /> GP
                </label>
              </fieldset>
              <label className="sr-only" htmlFor="authorizer-search">
                Vehicle or job card
              </label>
              <input
                className="vehicle-search"
                id="authorizer-search"
                name="search"
                defaultValue={search}
                placeholder="GG, GP, or job card number"
              />
              <button className="button button-primary" type="submit">
                Search
              </button>
              <Link className="button button-secondary" href="/job-cards/authorizer-dashboard">
                Clear
              </Link>
            </form>
          </section>
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="authorizer-review-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">{pageData.totalRecords} pending</p>
                <h2 id="authorizer-review-title">Job Cards for Review</h2>
              </div>
              <Link
                className="button button-secondary button-small"
                href="/job-cards/authorizer-vehicles"
              >
                Search by vehicle
              </Link>
            </div>
            <JobCardTable
              cards={pageData.items}
              mode="review"
              returnPath={tableReturnPath}
              currentUserCode={Number(session.userAccessCode) || null}
              page={pageData.page}
              totalPages={pageData.totalPages}
              pageHref={(nextPage) =>
                jobCardPageHref("/job-cards/authorizer-dashboard", query, nextPage)
              }
            />
          </section>
        </section>
      </main>
    );
  } catch (error) {
    const message =
      error instanceof JobCardApiError && error.reason === "unavailable"
        ? "The Job Cards service is temporarily unavailable. Please try again."
        : "The authorizer dashboard could not be loaded.";
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <h2>{message}</h2>
          <Link className="button button-secondary" href="/job-cards/authorizer-dashboard">
            Try again
          </Link>
        </section>
      </main>
    );
  }
}
