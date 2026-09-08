import Link from "next/link";

import { JobCardTable, hasJobCardAccess, hasRole } from "@/app/job-cards/_components";
import { accessRestricted, getJobCardSession, sessionMessage } from "@/app/job-cards/_page";
import { getPriorityUnassignedJobCards, JobCardApiError } from "@/lib/api-job-cards";

export default async function JobCardCapturerPage() {
  const session = await getJobCardSession();
  const problem = sessionMessage(session, "/job-cards/capturer-default");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasRole(session.roles, "capturer") && !hasJobCardAccess(session.accessLevel, session.roles))
    return accessRestricted("Your profile does not include Job Card capturer access.");
  try {
    const cards = await getPriorityUnassignedJobCards();
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
                  {cards.length} record{cards.length === 1 ? "" : "s"}
                </p>
                <h2 id="priority-job-cards-title">Priority Job Cards Ready for Capturing</h2>
              </div>
            </div>
            <JobCardTable cards={cards} mode="priority" returnPath="/job-cards/capturer-default" />
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
