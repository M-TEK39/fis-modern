import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";

import {
  LogbookShell,
  LogbookTable,
  VehicleSearchForm,
} from "@/app/(fleet-operations)/log-books/_components";
import {
  accessRestricted,
  getLogbookSession,
  hasLogbookAccess,
  parsePositiveInteger,
  queryValue,
  sessionMessage,
} from "@/app/(fleet-operations)/log-books/_page";
import {
  DEFAULT_LOGBOOK_PAGE_SIZE,
  getLogbookPage,
  LogbookApiError,
} from "@/lib/api/fleet-operations/api-logbooks";
import { getVehicleOptions, type VehicleOption } from "@/lib/api/vehicles/api-vehicles";

const routePath = "/log-books/delete";

function historyPath(search: string, mode: string, vmfCode: number | null, page: number) {
  const params = new URLSearchParams({ mode });
  if (search) params.set("search", search);
  if (vmfCode) params.set("vmfCode", String(vmfCode));
  if (page > 1) params.set("page", String(page));
  return `${routePath}?${params.toString()}`;
}

function LogbookDeletePagination({
  search,
  mode,
  vmfCode,
  page,
  totalPages,
}: Readonly<{
  search: string;
  mode: string;
  vmfCode: number;
  page: number;
  totalPages: number;
}>) {
  if (totalPages <= 1) return null;

  return (
    <nav className="vehicle-pagination" aria-label="Logbook deletion pages">
      {page > 1 ? (
        <Link
          className="vehicle-pagination-button"
          href={historyPath(search, mode, vmfCode, page - 1)}
          aria-label={`Go to logbook deletion page ${page - 1}`}
        >
          Previous
        </Link>
      ) : (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Previous
        </span>
      )}
      <span className="vehicle-pagination-meta" aria-live="polite">
        Page {page} of {totalPages}
      </span>
      {page < totalPages ? (
        <Link
          className="vehicle-pagination-button"
          href={historyPath(search, mode, vmfCode, page + 1)}
          aria-label={`Go to logbook deletion page ${page + 1}`}
        >
          Next
        </Link>
      ) : (
        <span
          className="vehicle-pagination-button vehicle-pagination-disabled"
          aria-disabled="true"
        >
          Next
        </span>
      )}
    </nav>
  );
}

function filterVehicles(options: readonly VehicleOption[], search: string, mode: string) {
  const normalized = search.trim().toLocaleLowerCase();
  if (!normalized) return [];
  const isGp = mode === "GP";
  return options
    .filter((vehicle) =>
      (isGp ? vehicle.registrationNumber : vehicle.fleetNumber)
        ?.toLocaleLowerCase()
        .includes(normalized),
    )
    .sort((left, right) => left.vmfCode - right.vmfCode);
}

function statusMessage(query: Record<string, string | string[] | undefined>) {
  const key = ["deleted", "error"].find((name) => query[name]);
  return key ? { key, value: queryValue(query[key]) } : null;
}

const LogbookDeletePageContent = renderLogbookDeletePageContent;

async function renderLogbookDeletePageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getLogbookSession();
  const problem = sessionMessage(session, "/log-books/delete");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasLogbookAccess(session))
    return accessRestricted("Your profile does not include Logbooks access.");

  const query = await searchParams;
  const search = queryValue(query.search);
  const mode = queryValue(query.mode) === "GP" ? "GP" : "GG";
  const vmfCode = parsePositiveInteger(queryValue(query.vmfCode));
  const requestedPage = parsePositiveInteger(queryValue(query.page)) ?? 1;
  const retryPath = historyPath(search, mode, vmfCode, requestedPage);
  const message = statusMessage(query);
  try {
    const [options, logbookPage] = await Promise.all([
      getVehicleOptions(),
      vmfCode
        ? getLogbookPage({
            page: requestedPage,
            pageSize: DEFAULT_LOGBOOK_PAGE_SIZE,
            vmfCode,
          })
        : Promise.resolve(null),
    ]);
    const matches = filterVehicles(options, search, mode);
    const currentPage = logbookPage?.page ?? requestedPage;
    const returnPath = historyPath(search, mode, vmfCode, currentPage);
    const selectedRecords = logbookPage?.items ?? [];
    return (
      <LogbookShell
        title="Delete a Logbook Handout"
        description="Find a vehicle and delete a returned logbook handout."
      >
        {message ? (
          <p
            className={`alert ${message.key === "error" ? "alert-error" : "alert-success"}`}
            role="status"
          >
            {message.value}
          </p>
        ) : null}
        <VehicleSearchForm
          action="/log-books/delete"
          search={search}
          mode={mode}
          vmfCode={vmfCode?.toString() ?? ""}
          options={matches}
        />
        {vmfCode ? (
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="logbook-delete-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">
                  {logbookPage?.total ?? 0} record{logbookPage?.total === 1 ? "" : "s"}
                </p>
                <h2 id="logbook-delete-results-title">Handouts for selected vehicle</h2>
              </div>
            </div>
            <LogbookTable records={selectedRecords} mode="delete" returnPath={returnPath} />
            {logbookPage ? (
              <>
                <LogbookDeletePagination
                  search={search}
                  mode={mode}
                  vmfCode={vmfCode}
                  page={logbookPage.page}
                  totalPages={logbookPage.totalPages}
                />
                <div className="pagination-meta">
                  Total records: {logbookPage.total} | Page size: {logbookPage.pageSize}
                </div>
              </>
            ) : null}
          </section>
        ) : (
          <p className="muted-copy">
            Search for a vehicle, then select it to load handouts for deletion.
          </p>
        )}
        <Link className="button button-secondary" href="/log-books">
          Main menu
        </Link>
      </LogbookShell>
    );
  } catch (error) {
    const messageText =
      error instanceof LogbookApiError
        ? "The Logbooks service is temporarily unavailable. Please try again."
        : "Logbook handouts could not be loaded.";
    return (
      <LogbookShell
        title="Delete a Logbook Handout"
        description="Find a vehicle and delete a returned logbook handout."
      >
        <section className="vehicle-status-card" role="alert">
          <h2>{messageText}</h2>
          <Link className="button button-secondary" href={retryPath}>
            Try again
          </Link>
        </section>
      </LogbookShell>
    );
  }
}

export default function LogbookDeletePage(props: Parameters<typeof LogbookDeletePageContent>[0]) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LogbookDeletePageContent {...props} />
    </Suspense>
  );
}
