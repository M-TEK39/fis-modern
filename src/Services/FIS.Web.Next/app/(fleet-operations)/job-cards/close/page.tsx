import Link from "next/link";

import { CloseJobCardDetails, JobCardSearchForm, JobCardTable, LeftoverCloseJobCardForm } from "@/app/(fleet-operations)/job-cards/_components";
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
  getCloseJobCardDetails,
  getJobCardsPage,
  JobCardApiError,
} from "@/lib/api/fleet-operations/api-job-cards";

export default function CloseJobCardsPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <JobCardPageBoundary>
      <CloseJobCardsContent searchParams={searchParams} />
    </JobCardPageBoundary>
  );
}

async function CloseJobCardsContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getJobCardSession();
  if (session.status === "expired" || session.status === "unavailable")
    return <SessionProblem returnPath="/job-cards/close" />;
  if (session.status !== "authenticated")
    return <AccessRestricted message="Your session could not be loaded." />;
  if (!hasRole(session.roles, "capturer") && !hasJobCardAccess(session.accessLevel, session.roles))
    return <AccessRestricted message="Your profile does not include Job Card capturer access." />;
  const query = await searchParams;
  const search = queryValue(query.search);
  const mode = querySearchType(query.mode);
  const page = queryPage(query.page);
    const selectedId = Number(queryValue(query.id));
  try {
    const pageData = await getJobCardsPage({
      page,
      pageSize: DEFAULT_JOB_CARD_PAGE_SIZE,
      search,
      searchType: mode,
      list: "close",
    });
    const tableReturnPath = jobCardPageHref(
      "/job-cards/close",
      { ...query, id: undefined },
      pageData.page,
    );
    const selectedCard =
      Number.isInteger(selectedId) && selectedId > 0
        ? (pageData.items.find((card) => card.jobCardId === selectedId) ?? null)
        : null;
    let closeDetails: Awaited<ReturnType<typeof getCloseJobCardDetails>> | null = null;
    if (Number.isInteger(selectedId) && selectedId > 0) {
      try {
        closeDetails = await getCloseJobCardDetails(selectedId);
      } catch (error) {
        if (!(error instanceof JobCardApiError && error.reason === "not-found")) {
          throw error;
        }
      }
    }
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="close-job-cards-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Job Cards</p>
              <h1 id="close-job-cards-title">Close Job Cards</h1>
              <p>Capture final repair costs when closing each job card.</p>
            </div>
            <Link className="button button-secondary" href="/job-cards/capturer-default">
              Main menu
            </Link>
          </header>
          <section className="vehicle-status-maintenance-panel">
            <JobCardSearchForm
              action="/job-cards/close"
              inputId="close-job-card-search"
              inputLabel="Job card number"
              mode={mode}
              placeholder="Job card number"
              search={search}
            />
          </section>
          <section className="vehicle-status-maintenance-panel">
            <p className="eyebrow">{pageData.totalRecords} ready</p>
            <h2>Job Cards Ready for Closing</h2>
            <JobCardTable
              cards={pageData.items}
              mode="close"
              returnPath={tableReturnPath}
              page={pageData.page}
              totalPages={pageData.totalPages}
              pageHref={(nextPage) => jobCardPageHref("/job-cards/close", query, nextPage)}
            />
          </section>
          {closeDetails?.overlay && closeDetails.item ? (
            <CloseJobCardDetails details={closeDetails.item} returnPath={tableReturnPath} />
          ) : closeDetails?.overlay ? (
            <p className="muted-copy">No job card details found.</p>
          ) : selectedCard ? (
            <section className="vehicle-status-maintenance-panel" aria-labelledby="leftover-close-title">
              <h2 id="leftover-close-title">Job Card Details</h2>
              <LeftoverCloseJobCardForm card={selectedCard} returnPath={tableReturnPath} />
            </section>
          ) : null}
        </section>
      </main>
    );
  } catch (error) {
    const message =
      error instanceof JobCardApiError && error.reason === "unavailable"
        ? "The Job Cards service is temporarily unavailable. Please try again."
        : "Job cards ready for closing could not be loaded.";
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <h2>{message}</h2>
          <Link className="button button-secondary" href="/job-cards/close">
            Try again
          </Link>
        </section>
      </main>
    );
  }
}
