import Link from "next/link";

import {
  JobCardDetails,
  JobCardSearchForm,
  JobCardTable,
} from "@/app/(fleet-operations)/job-cards/_components";
import { hasJobCardAccess, hasRole } from "@/app/(fleet-operations)/job-cards/_utils";
import {
  AccessRestricted,
  JobCardPageBoundary,
  SessionProblem,
} from "@/app/(fleet-operations)/job-cards/_page";
import {
  getJobCardForSelection,
  getJobCardSession,
  jobCardPageHref,
  queryPage,
  querySearchType,
  queryValue,
} from "@/app/(fleet-operations)/job-cards/_page-utils";
import JobCardsListHeader from "@/components/ui/job-cards-list-header";
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

const ListJobCardsContent = renderListJobCardsContent;

async function renderListJobCardsContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getJobCardSession();
  if (session.status === "expired" || session.status === "unavailable")
    return <SessionProblem returnPath="/job-cards/list" />;
  if (session.status !== "authenticated")
    return <AccessRestricted message="Your session could not be loaded." />;
  if (!hasRole(session.roles, "capturer") && !hasJobCardAccess(session.accessLevel, session.roles))
    return <AccessRestricted message="Your profile does not include Job Card capturer access." />;
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
          <JobCardsListHeader />
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
          <JobCardSearchForm
            action="/job-cards/list"
            inputId="list-job-card-search"
            inputLabel="Vehicle or job card number"
            mode={mode}
            placeholder="GG, GP, or job card number"
            search={search}
          />
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
