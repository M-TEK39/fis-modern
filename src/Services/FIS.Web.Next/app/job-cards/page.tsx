import Link from "next/link";

import { JobCardTable, JobCardMenu, hasJobCardAccess, hasRole } from "@/app/job-cards/_components";
import {
  accessRestricted,
  getJobCardSession,
  queryValue,
  sessionMessage,
} from "@/app/job-cards/_page";
import { getJobCards, JobCardApiError } from "@/lib/api-job-cards";

export default async function JobCardsPage({
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
  const status = queryValue(query.status);
  try {
    const cards = await getJobCards();
    const normalized = search.toLocaleLowerCase();
    const filtered = cards.filter((card) => {
      const matchesSearch =
        !normalized ||
        [
          card.jobCardId,
          card.ggNumber,
          card.registrationNumber,
          card.extraDescription,
          statusLabel(card.statusCode),
        ].some((value) =>
          String(value ?? "")
            .toLocaleLowerCase()
            .includes(normalized),
        );
      const matchesStatus =
        !status ||
        status === "all" ||
        (status === "open"
          ? card.statusCode !== 5 && card.statusCode !== 7
          : status === "closed"
            ? card.statusCode === 5
            : true);
      return matchesSearch && matchesStatus;
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
                  {filtered.length} of {cards.length} record{cards.length === 1 ? "" : "s"}
                </p>
                <h2 id="job-card-snapshot-title">All Job Cards Snapshot</h2>
              </div>
            </div>
            <form className="vehicle-search-row" method="get">
              <label className="sr-only" htmlFor="job-card-search">
                Search job cards
              </label>
              <input
                className="vehicle-search"
                id="job-card-search"
                name="search"
                defaultValue={search}
                placeholder="Search job card, GG, registration, description"
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
              cards={filtered}
              mode="list"
              returnPath="/job-cards/list"
              currentUserCode={Number(session.userAccessCode) || null}
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

function statusLabel(statusCode: number) {
  return (
    {
      1: "pending",
      2: "awaiting authorization",
      3: "authorized",
      4: "in progress",
      5: "complete",
      6: "failed",
      7: "canceled",
    }[statusCode] ?? "unknown"
  );
}
