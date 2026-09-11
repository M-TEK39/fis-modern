import Link from "next/link";

import { JobCardSearchForm, JobCardTable } from "@/app/(fleet-operations)/job-cards/_components";
import { hasJobCardAccess, hasRole } from "@/app/(fleet-operations)/job-cards/_utils";
import {
  AccessRestricted,
  JobCardPageBoundary,
  SessionProblem,
} from "@/app/(fleet-operations)/job-cards/_page";
import {
  getJobCardSession,
  jobCardPageHref,
  queryPage,
  querySearchType,
  queryValue,
} from "@/app/(fleet-operations)/job-cards/_page-utils";
import {
  DEFAULT_JOB_CARD_PAGE_SIZE,
  getJobCardsPage,
  JobCardApiError,
} from "@/lib/api/fleet-operations/api-job-cards";

export default function CancelJobCardsPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <JobCardPageBoundary>
      <CancelJobCardsContent searchParams={searchParams} />
    </JobCardPageBoundary>
  );
}

async function CancelJobCardsContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getJobCardSession();
  if (session.status === "expired" || session.status === "unavailable")
    return <SessionProblem returnPath="/job-cards/cancel" />;
  if (session.status !== "authenticated")
    return <AccessRestricted message="Your session could not be loaded." />;
  if (!hasRole(session.roles, "capturer") && !hasJobCardAccess(session.accessLevel, session.roles))
    return <AccessRestricted message="Your profile does not include Job Card capturer access." />;
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
      statusCodes: [3, 4, 6, 7],
    });
    const tableReturnPath = jobCardPageHref(
      "/job-cards/cancel",
      { ...query, id: undefined },
      pageData.page,
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="cancel-job-cards-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Job Cards</p>
              <h1 id="cancel-job-cards-title">Cancel Job Cards</h1>
              <p>Search authorized or in-progress job cards and cancel them when required.</p>
            </div>
            <Link className="button button-secondary" href="/job-cards/capturer-default">
              Main menu
            </Link>
          </header>
          <section className="vehicle-status-maintenance-panel">
            <JobCardSearchForm
              action="/job-cards/cancel"
              inputId="cancel-job-card-search"
              inputLabel="Vehicle or job card"
              mode={mode}
              placeholder="GG, GP, or job card number"
              search={search}
            />
          </section>
          <section className="vehicle-status-maintenance-panel">
            <p className="eyebrow">
              {pageData.totalRecords} record{pageData.totalRecords === 1 ? "" : "s"}
            </p>
            <h2>Cancelable Job Cards</h2>
            <JobCardTable
              cards={pageData.items}
              mode="cancel"
              returnPath={tableReturnPath}
              page={pageData.page}
              totalPages={pageData.totalPages}
              pageHref={(nextPage) => jobCardPageHref("/job-cards/cancel", query, nextPage)}
            />
          </section>
        </section>
      </main>
    );
  } catch (error) {
    const message =
      error instanceof JobCardApiError && error.reason === "unavailable"
        ? "The Job Cards service is temporarily unavailable. Please try again."
        : "Cancelable job cards could not be loaded.";
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <h2>{message}</h2>
          <Link className="button button-secondary" href="/job-cards/cancel">
            Try again
          </Link>
        </section>
      </main>
    );
  }
}
