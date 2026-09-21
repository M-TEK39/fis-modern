import Link from "next/link";

import { JobCardTable } from "@/app/(fleet-operations)/job-cards/_components";
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
} from "@/app/(fleet-operations)/job-cards/_page-utils";
import {
  DEFAULT_JOB_CARD_PAGE_SIZE,
  getAssignedPriorityJobCardsPage,
  getPriorityUnassignedJobCardsPage,
  JobCardApiError,
} from "@/lib/api/fleet-operations/api-job-cards";

export default function JobCardCapturerPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <JobCardPageBoundary>
      <JobCardCapturerContent searchParams={searchParams} />
    </JobCardPageBoundary>
  );
}

async function JobCardCapturerContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getJobCardSession();
  if (session.status === "expired" || session.status === "unavailable")
    return <SessionProblem returnPath="/job-cards/capturer-default" />;
  if (session.status !== "authenticated")
    return <AccessRestricted message="Your session could not be loaded." />;
  if (!hasRole(session.roles, "capturer") && !hasJobCardAccess(session.accessLevel, session.roles))
    return <AccessRestricted message="Your profile does not include Job Card capturer access." />;
  const query = await searchParams;
  const page = queryPage(query.page);
  const assignedPage = queryPage(query.assignedPage);
  try {
    const [pageData, assignedPageData] = await Promise.all([
      getPriorityUnassignedJobCardsPage({
        page,
        pageSize: DEFAULT_JOB_CARD_PAGE_SIZE,
      }),
      getAssignedPriorityJobCardsPage({
        page: assignedPage,
        pageSize: DEFAULT_JOB_CARD_PAGE_SIZE,
      }),
    ]);
    const tableReturnPath = jobCardPageHref("/job-cards/capturer-default", query, pageData.page);
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="job-card-capturer-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Job Cards</p>
              <h1 id="job-card-capturer-title">Capturer Menu</h1>
              <p>Capture, edit, print, cancel, and close job cards.</p>
            </div>
            <Link className="button button-secondary" href="/job-cards">
              Main menu
            </Link>
          </header>
          <div className="button-row">
            <Link className="button button-primary" href="/job-cards/select-vehicle">
              Create new job card
            </Link>
            <Link className="button button-secondary" href="/job-cards/list">
              List / edit job cards
            </Link>
            <Link className="button button-secondary" href="/job-cards/print">
              Print job cards
            </Link>
            <Link className="button button-secondary" href="/job-cards/cancel">
              Cancel job cards
            </Link>
            <Link className="button button-secondary" href="/job-cards/close">
              Close job cards
            </Link>
          </div>
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="priority-job-cards-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">
                  {pageData.totalRecords} record{pageData.totalRecords === 1 ? "" : "s"}
                </p>
                <h2 id="priority-job-cards-title">Priority Job Cards Ready for Capturing</h2>
              </div>
            </div>
            <JobCardTable
              cards={pageData.items}
              mode="priority"
              returnPath={tableReturnPath}
              page={pageData.page}
              totalPages={pageData.totalPages}
              pageHref={(nextPage) =>
                jobCardPageHref("/job-cards/capturer-default", query, nextPage)
              }
            />
          </section>
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="assigned-priority-job-cards-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">
                  {assignedPageData.totalRecords} record
                  {assignedPageData.totalRecords === 1 ? "" : "s"}
                </p>
                <h2 id="assigned-priority-job-cards-title">
                  Assigned job cards that require immediate attention
                </h2>
              </div>
            </div>
            <JobCardTable
              cards={assignedPageData.items}
              mode="print"
              returnPath={jobCardPageHref(
                "/job-cards/capturer-default",
                query,
                assignedPageData.page,
                "assignedPage",
              )}
              page={assignedPageData.page}
              totalPages={assignedPageData.totalPages}
              pageHref={(nextPage) =>
                jobCardPageHref("/job-cards/capturer-default", query, nextPage, "assignedPage")
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
        : "Priority job cards could not be loaded.";
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <h2>{message}</h2>
          <Link className="button button-secondary" href="/job-cards/capturer-default">
            Try again
          </Link>
        </section>
      </main>
    );
  }
}
