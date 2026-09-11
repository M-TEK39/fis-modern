import Link from "next/link";

import {
  JobCardTable,
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

export default function AuthorizerVehicleViewPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <JobCardPageBoundary>
      <AuthorizerVehicleViewContent searchParams={searchParams} />
    </JobCardPageBoundary>
  );
}

async function AuthorizerVehicleViewContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getJobCardSession();
  const problem = sessionMessage(session, "/job-cards/authorizer-vehicles");
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
      "/job-cards/authorizer-vehicles",
      { ...query, id: undefined },
      pageData.page,
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="authorizer-vehicles-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Job Cards</p>
              <h1 id="authorizer-vehicles-title">Job Card Authorizer</h1>
              <p>Filter job cards by GG or GP number before reviewing them.</p>
            </div>
            <Link className="button button-secondary" href="/job-cards/authorizer-dashboard">
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
              <label className="sr-only" htmlFor="authorizer-vehicle-search">
                Vehicle number
              </label>
              <input
                className="vehicle-search"
                id="authorizer-vehicle-search"
                name="search"
                defaultValue={search}
                placeholder={mode === "GP" ? "GP number" : "GG number"}
              />
              <button className="button button-primary" type="submit">
                Search
              </button>
              <Link className="button button-secondary" href="/job-cards/authorizer-vehicles">
                Clear
              </Link>
            </form>
          </section>
          <section className="vehicle-status-maintenance-panel">
            <p className="eyebrow">{pageData.totalRecords} pending</p>
            <h2>Matching Job Cards</h2>
            <JobCardTable
              cards={pageData.items}
              mode="review"
              returnPath={tableReturnPath}
              currentUserCode={Number(session.userAccessCode) || null}
              page={pageData.page}
              totalPages={pageData.totalPages}
              pageHref={(nextPage) =>
                jobCardPageHref("/job-cards/authorizer-vehicles", query, nextPage)
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
        : "Job cards could not be loaded.";
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <h2>{message}</h2>
          <Link className="button button-secondary" href="/job-cards/authorizer-vehicles">
            Try again
          </Link>
        </section>
      </main>
    );
  }
}
