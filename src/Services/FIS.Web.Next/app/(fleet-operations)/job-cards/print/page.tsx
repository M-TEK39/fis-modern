import Link from "next/link";

import {
  JobCardPrintPreview,
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
import {
  DEFAULT_JOB_CARD_PAGE_SIZE,
  getJobCardsPage,
  JobCardApiError,
} from "@/lib/api/fleet-operations/api-job-cards";

export default function PrintJobCardsPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <JobCardPageBoundary>
      <PrintJobCardsContent searchParams={searchParams} />
    </JobCardPageBoundary>
  );
}

async function PrintJobCardsContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getJobCardSession();
  if (session.status === "expired" || session.status === "unavailable")
    return <SessionProblem returnPath="/job-cards/print" />;
  if (session.status !== "authenticated")
    return <AccessRestricted message="Your session could not be loaded." />;
  if (!hasRole(session.roles, "capturer") && !hasJobCardAccess(session.accessLevel, session.roles))
    return <AccessRestricted message="Your profile does not include Job Card capturer access." />;
  const query = await searchParams;
  const search = queryValue(query.search);
  const mode = querySearchType(query.mode);
  const selectedId = Number(queryValue(query.id));
  const page = queryPage(query.page);
  try {
    const pageData = await getJobCardsPage({
      page,
      pageSize: DEFAULT_JOB_CARD_PAGE_SIZE,
      search,
      searchType: mode,
      statusCodes: [3],
    });
    const selectedCandidate =
      Number.isInteger(selectedId) && selectedId > 0
        ? await getJobCardForSelection(selectedId)
        : null;
    const selected = selectedCandidate?.statusCode === 3 ? selectedCandidate : null;
    const tableReturnPath = jobCardPageHref(
      "/job-cards/print",
      { ...query, id: undefined },
      pageData.page,
    );
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
          <JobCardSearchForm
            action="/job-cards/print"
            inputId="print-job-card-search"
            inputLabel="Vehicle or job card number"
            mode={mode}
            placeholder="GG, GP, or job card number"
            search={search}
          />
          <section className="vehicle-status-maintenance-panel">
            <p className="eyebrow">{pageData.totalRecords} authorized</p>
            <h2>Authorized Job Cards</h2>
            <JobCardTable
              cards={pageData.items}
              mode="print"
              returnPath={tableReturnPath}
              page={pageData.page}
              totalPages={pageData.totalPages}
              pageHref={(nextPage) => jobCardPageHref("/job-cards/print", query, nextPage)}
            />
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
