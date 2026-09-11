import Link from "next/link";
import SearchTypeFieldset from "@/components/ui/search-type-fieldset";
import JobCardsSnapshotHeader from "@/components/ui/job-cards-snapshot-header";

import { JobCardMenu, JobCardTable } from "@/app/(fleet-operations)/job-cards/_components";
import { jobCardPageHref } from "@/app/(fleet-operations)/job-cards/_page-utils";
import type { getJobCardsPage } from "@/lib/api/fleet-operations/api-job-cards";

type JobCardsSnapshotViewProps = Readonly<{
  canAuthorizer: boolean;
  canCapturer: boolean;
  pageData: Awaited<ReturnType<typeof getJobCardsPage>>;
  query: Record<string, string | string[] | undefined>;
  search: string;
  searchType: "GG" | "GP";
  sessionUserCode: number | null;
  status: "all" | "open" | "closed";
}>;

export function JobCardsSnapshotView({
  canAuthorizer,
  canCapturer,
  pageData,
  query,
  search,
  searchType,
  sessionUserCode,
  status,
}: JobCardsSnapshotViewProps) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="job-cards-title">
        <JobCardsSnapshotHeader />
        <JobCardMenu canCapturer={canCapturer} canAuthorizer={canAuthorizer} />
        <section
          className="vehicle-status-maintenance-panel"
          aria-labelledby="job-card-snapshot-title"
        >
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">
                {pageData.totalRecords} record{pageData.totalRecords === 1 ? "" : "s"}
              </p>
              <h2 id="job-card-snapshot-title">All Job Cards Snapshot</h2>
            </div>
          </div>
          <form className="vehicle-search-row" method="get">
            <input type="hidden" name="page" value="1" />
            <SearchTypeFieldset selectedType={searchType} legend="Find by" name="mode" />
            <label className="sr-only" htmlFor="job-card-search">
              Search job cards by GG, GP, or job card number
            </label>
            <input
              className="vehicle-search"
              id="job-card-search"
              name="search"
              defaultValue={search}
              placeholder="GG, GP, or job card number"
            />
            <select
              className="form-select"
              name="status"
              defaultValue={status}
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
            cards={pageData.items}
            mode="list"
            returnPath="/job-cards/list"
            currentUserCode={sessionUserCode}
            page={pageData.page}
            totalPages={pageData.totalPages}
            pageHref={(nextPage) => jobCardPageHref("/job-cards", query, nextPage)}
          />
        </section>
      </section>
    </main>
  );
}
