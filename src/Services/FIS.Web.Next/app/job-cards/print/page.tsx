import Link from "next/link";

import {
  JobCardPrintPreview,
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

export default async function PrintJobCardsPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getJobCardSession();
  const problem = sessionMessage(session, "/job-cards/print");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasRole(session.roles, "capturer") && !hasJobCardAccess(session.accessLevel, session.roles))
    return accessRestricted("Your profile does not include Job Card capturer access.");
  const query = await searchParams;
  const search = queryValue(query.search);
  const mode = queryValue(query.mode) || "GG";
  const selectedId = Number(queryValue(query.id));
  try {
    const cards = (await getJobCards()).filter((card) => card.statusCode === 3);
    const filtered = filterByVehicle(cards, search, mode);
    const selected =
      Number.isInteger(selectedId) && selectedId > 0
        ? (cards.find((card) => card.jobCardId === selectedId) ?? null)
        : null;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="print-job-cards-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Job Cards</p>
              <h1 id="print-job-cards-title">Print Job Cards</h1>
              <p>Search by GG or GP number to preview authorized job cards.</p>
            </div>
            <Link className="button button-secondary" href="/job-cards/capturer-default">
              Main menu
            </Link>
          </header>
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
            <label className="sr-only" htmlFor="print-job-card-search">
              Vehicle or job card number
            </label>
            <input
              className="vehicle-search"
              id="print-job-card-search"
              name="search"
              defaultValue={search}
              placeholder="GG, GP, or job card number"
            />
            <button className="button button-primary" type="submit">
              Search
            </button>
            <Link className="button button-secondary" href="/job-cards/print">
              Clear
            </Link>
          </form>
          <section className="vehicle-status-maintenance-panel">
            <p className="eyebrow">{filtered.length} authorized</p>
            <h2>Authorized Job Cards</h2>
            <JobCardTable cards={filtered} mode="print" returnPath="/job-cards/print" />
          </section>
          {selected ? <JobCardPrintPreview card={selected} /> : null}
        </section>
      </main>
    );
  } catch (error) {
    const message =
      error instanceof JobCardApiError && error.reason === "unavailable"
        ? "The Job Cards service is temporarily unavailable. Please try again."
        : "Authorized job cards could not be loaded.";
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <h2>{message}</h2>
          <Link className="button button-secondary" href="/job-cards/print">
            Try again
          </Link>
        </section>
      </main>
    );
  }
}
