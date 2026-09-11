import Link from "next/link";

import {
  JobCardDetails,
  JobCardTable,
  hasJobCardAccess,
  hasRole,
} from "@/app/(fleet-operations)/job-cards/_components";
import {
  accessRestricted,
  getJobCardForSelection,
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

export default function ListJobCardsPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <JobCardPageBoundary>
      <ListJobCardsContent searchParams={searchParams} />
    </JobCardPageBoundary>
  );
}

async function ListJobCardsContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getJobCardSession();
  const problem = sessionMessage(session, "/job-cards/list");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasRole(session.roles, "capturer") && !hasJobCardAccess(session.accessLevel, session.roles))
    return accessRestricted("Your profile does not include Job Card capturer access.");
  const query = await searchParams;
  const search = queryValue(query.search || query.gg);
  const mode = querySearchType(query.mode);
  const page = queryPage(query.page);
  const selectedId = Number(queryValue(query.id));
  const message =
    queryValue(query.updated) === "1"
      ? "Job card updated successfully."
      : queryValue(query.deleted) === "1"
        ? "Job card deleted successfully."
        : queryValue(query.error);
  try {
    const pageData = await getJobCardsPage({
      page,
      pageSize: DEFAULT_JOB_CARD_PAGE_SIZE,
      search,
      searchType: mode,
    });
    const selected =
      Number.isInteger(selectedId) && selectedId > 0
        ? await getJobCardForSelection(selectedId)
        : null;
    const tableReturnPath = jobCardPageHref(
      "/job-cards/list",
      { ...query, id: undefined },
      pageData.page,
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="list-job-cards-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Job Cards</p>
              <h1 id="list-job-cards-title">List / Edit Job Cards</h1>
              <p>Search by GG or GP number, select a job card, then review its details.</p>
            </div>
            <div className="button-row">
              <Link className="button button-primary" href="/job-cards/select-vehicle">
                Add new job card
              </Link>
              <Link className="button button-secondary" href="/job-cards/capturer-default">
                Main menu
              </Link>
            </div>
          </header>
          {message ? (
            <div
              className={
                query.updated === "1" || query.deleted === "1"
                  ? "notice notice-success"
                  : "notice notice-error"
              }
              role={query.error ? "alert" : "status"}
            >
              {message}
            </div>
          ) : null}
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
            <label className="sr-only" htmlFor="list-job-card-search">
              Vehicle or job card number
            </label>
            <input
              className="vehicle-search"
              id="list-job-card-search"
              name="search"
              defaultValue={search}
              placeholder="GG, GP, or job card number"
            />
            <button className="button button-primary" type="submit">
              Search
            </button>
            <Link className="button button-secondary" href="/job-cards/list">
              Clear
            </Link>
          </form>
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="job-card-results-title"
          >
            <p className="eyebrow">
              {pageData.totalRecords} record{pageData.totalRecords === 1 ? "" : "s"}
            </p>
            <h2 id="job-card-results-title">Job Card Results</h2>
            <JobCardTable
              cards={pageData.items}
              mode="list"
              returnPath={tableReturnPath}
              page={pageData.page}
              totalPages={pageData.totalPages}
              pageHref={(nextPage) => jobCardPageHref("/job-cards/list", query, nextPage)}
            />
          </section>
          {selected ? (
            <JobCardDetails
              card={selected}
              returnPath={jobCardPageHref("/job-cards/list", query, pageData.page)}
            />
          ) : (
            <p className="muted-copy">Select Review to inspect or edit a job card.</p>
          )}
        </section>
      </main>
    );
  } catch (error) {
    const messageText =
      error instanceof JobCardApiError && error.reason === "unavailable"
        ? "The Job Cards service is temporarily unavailable. Please try again."
        : "Job cards could not be loaded.";
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <h2>{messageText}</h2>
          <Link className="button button-secondary" href="/job-cards/list">
            Try again
          </Link>
        </section>
      </main>
    );
  }
}
