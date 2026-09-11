import Link from "next/link";

import {
  JobCardDetails,
  JobCardSearchForm,
  JobCardTable,
} from "@/app/(fleet-operations)/job-cards/_components";
import {
  getJobCardForSelection,
  jobCardPageHref,
} from "@/app/(fleet-operations)/job-cards/_page-utils";
import JobCardsListHeader from "@/components/ui/job-cards-list-header";
import type { getJobCardsPage } from "@/lib/api/fleet-operations/api-job-cards";

type ListJobCardsViewProps = Readonly<{
  message: string;
  mode: "GG" | "GP";
  pageData: Awaited<ReturnType<typeof getJobCardsPage>>;
  query: Record<string, string | string[] | undefined>;
  search: string;
  selected: Awaited<ReturnType<typeof getJobCardForSelection>>;
  tableReturnPath: string;
}>;

export function ListJobCardsView({
  message,
  mode,
  pageData,
  query,
  search,
  selected,
  tableReturnPath,
}: ListJobCardsViewProps) {
  const messageIsSuccess = query.updated === "1" || query.deleted === "1";

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="list-job-cards-title">
        <JobCardsListHeader />
        {message ? (
          <div
            className={messageIsSuccess ? "notice notice-success" : "notice notice-error"}
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
}
