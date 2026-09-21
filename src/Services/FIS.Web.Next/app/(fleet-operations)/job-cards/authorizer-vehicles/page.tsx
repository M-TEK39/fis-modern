import Link from "next/link";

import { JobCardSearchForm, JobCardTable, JobCardAuthorizerGgStatsTable, AuthorizerJobCardDetails } from "@/app/(fleet-operations)/job-cards/_components";
import { hasJobCardAccess, hasRole } from "@/app/(fleet-operations)/job-cards/_utils";
import {
  AccessRestricted,
  JobCardPageBoundary,
  SessionProblem,
} from "@/app/(fleet-operations)/job-cards/_page";
import {
  getJobCardSession,
  getAuthorizerDetailsForSelection,
  jobCardPageHref,
  queryPage,
  querySearchType,
  queryValue,
} from "@/app/(fleet-operations)/job-cards/_page-utils";
import {
  DEFAULT_JOB_CARD_PAGE_SIZE,
  getAuthorizerGgStatsPage,
  getAuthorizerJobCardsPage,
  JobCardApiError,
} from "@/lib/api/fleet-operations/api-job-cards";

export default function AuthorizerVehicleViewPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <JobCardPageBoundary>
      <AuthorizerVehicleViewContent searchParams={searchParams} />
    </JobCardPageBoundary>
  );
}

async function AuthorizerVehicleViewContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getJobCardSession();
  if (session.status === "expired" || session.status === "unavailable")
    return <SessionProblem returnPath="/job-cards/authorizer-vehicles" />;
  if (session.status !== "authenticated")
    return <AccessRestricted message="Your session could not be loaded." />;
  if (
    !hasRole(session.roles, "authorizer") &&
    !hasJobCardAccess(session.accessLevel, session.roles)
  )
    return <AccessRestricted message="Your profile does not include Job Card authorizer access." />;
  const query = await searchParams;
  const search = queryValue(query.search);
  const mode = querySearchType(query.mode);
  const page = queryPage(query.page);
  const selectedId = Number(queryValue(query.id));
  try {
    const statsPage =
      mode === "GP"
        ? null
        : await getAuthorizerGgStatsPage({
            page: search.length > 0 ? 1 : page,
            pageSize: DEFAULT_JOB_CARD_PAGE_SIZE,
            ggNumber: search,
          });
    const pageData =
      statsPage === null || search.length > 0
        ? await getAuthorizerJobCardsPage({
            page,
            pageSize: DEFAULT_JOB_CARD_PAGE_SIZE,
            search,
            searchType: mode,
            statusCodes: [1, 2],
          })
        : null;
    const tableReturnPath = jobCardPageHref(
      "/job-cards/authorizer-vehicles",
      { ...query, id: undefined },
      statsPage === null ? (pageData?.page ?? page) : page,
    );
    const selectedDetails =
      Number.isInteger(selectedId) && selectedId > 0
        ? await getAuthorizerDetailsForSelection(selectedId)
        : null;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="authorizer-vehicles-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Job Cards</p>
              <h1 id="authorizer-vehicles-title">Job Card Authorizer</h1>
              <p>Review job-card stats per GG number, then open the cards for a selected vehicle.</p>
            </div>
            <Link className="button button-secondary" href="/job-cards/authorizer-dashboard">
              Back
            </Link>
          </header>
          <section className="vehicle-status-maintenance-panel">
            <JobCardSearchForm
              action="/job-cards/authorizer-vehicles"
              inputId="authorizer-vehicle-search"
              inputLabel="Vehicle number"
              mode={mode}
              placeholder={mode === "GP" ? "GP number" : "GG number"}
              search={search}
            />
          </section>
          {statsPage ? (
            <section className="vehicle-status-maintenance-panel">
              <p className="eyebrow">{statsPage.totalRecords} GG number{statsPage.totalRecords === 1 ? "" : "s"}</p>
              <h2>Job Card Stats per GG Number</h2>
              <JobCardAuthorizerGgStatsTable
                rows={statsPage.items}
                page={search.length > 0 ? undefined : statsPage.page}
                totalPages={search.length > 0 ? undefined : statsPage.totalPages}
                pageHref={
                  search.length > 0
                    ? undefined
                    : (nextPage) =>
                        jobCardPageHref("/job-cards/authorizer-vehicles", query, nextPage)
                }
                ggHref={(ggNumber) =>
                  jobCardPageHref(
                    "/job-cards/authorizer-vehicles",
                    { search: ggNumber, mode: "GG" },
                    1,
                  )
                }
              />
            </section>
          ) : null}
          {pageData ? (
            <section className="vehicle-status-maintenance-panel">
              <p className="eyebrow">{pageData.totalRecords} pending</p>
              <h2>
                {statsPage ? "Job Card for specific GG Number" : "Matching Job Cards"}
              </h2>
              <JobCardTable
                cards={pageData.items}
                mode="review"
                returnPath={tableReturnPath}
                currentUserCode={Number(session.userAccessCode) || null}
                page={pageData.page}
                totalPages={pageData.totalPages}
                pageHref={(nextPage) =>
                  jobCardPageHref("/job-cards/authorizer-vehicles", query, nextPage)
                }
              />
            </section>
          ) : null}
          {selectedDetails ? <AuthorizerJobCardDetails details={selectedDetails} /> : null}
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
          <Link className="button button-secondary" href="/job-cards/authorizer-vehicles">
            Try again
          </Link>
        </section>
      </main>
    );
  }
}
