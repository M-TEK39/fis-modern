import Link from "next/link";

import {
  JobCardDetails,
  JobCardTable,
  hasJobCardAccess,
  hasRole,
} from "@/app/job-cards/_components";
import {
  accessRestricted,
  filterByVehicle,
  getJobCardSession,
  queryValue,
  sessionMessage,
} from "@/app/job-cards/_page";
import { getJobCards, JobCardApiError } from "@/lib/api-job-cards";

export default async function ListJobCardsPage({
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
  const mode = queryValue(query.mode) || "GG";
  const selectedId = Number(queryValue(query.id));
  const message =
    queryValue(query.updated) === "1"
      ? "Job card updated successfully."
      : queryValue(query.deleted) === "1"
        ? "Job card deleted successfully."
        : queryValue(query.error);
  try {
    const cards = await getJobCards();
    const filtered = filterByVehicle(cards, search, mode);
    const selected =
      Number.isInteger(selectedId) && selectedId > 0
        ? (cards.find((card) => card.jobCardId === selectedId) ?? null)
        : null;
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
              {filtered.length} record{filtered.length === 1 ? "" : "s"}
            </p>
            <h2 id="job-card-results-title">Job Card Results</h2>
            <JobCardTable cards={filtered} mode="list" returnPath="/job-cards/list" />
          </section>
          {selected ? (
            <JobCardDetails
              card={selected}
              returnPath={`/job-cards/list?id=${selected.jobCardId}`}
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
