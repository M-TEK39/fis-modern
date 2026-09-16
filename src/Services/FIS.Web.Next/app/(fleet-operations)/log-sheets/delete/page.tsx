import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";

import {
  LogsheetShell,
  LogsheetTable,
  VehicleSearchForm,
} from "@/app/(fleet-operations)/log-sheets/_components";
import {
  accessRestricted,
  filterVehicles,
  getLogsheetSession,
  hasLogsheetAccess,
  queryValue,
  sessionMessage,
  statusMessage,
} from "@/app/(fleet-operations)/log-sheets/_page";
import { getLogsheetsPage, LogsheetApiError } from "@/lib/api/fleet-operations/api-logsheets";
import { getVehicleOptions, VehicleApiError } from "@/lib/api/vehicles/api-vehicles";

const PAGE_SIZE = 24;

function pageValue(value: string | string[] | undefined) {
  const parsed = Number(queryValue(value));
  return Number.isInteger(parsed) && parsed > 0 ? parsed : 1;
}

function pageHref(values: Record<string, string | number | undefined>, page: number) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(values)) {
    if (value !== undefined && value !== "") params.set(key, String(value));
  }
  if (page > 1) params.set("page", String(page));
  const query = params.toString();
  return `/log-sheets/delete${query ? `?${query}` : ""}`;
}

const LogsheetDeletePageContent = renderLogsheetDeletePageContent;

async function renderLogsheetDeletePageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getLogsheetSession();
  const problem = sessionMessage(session, "/log-sheets/delete");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasLogsheetAccess(session))
    return accessRestricted("Your profile does not include Log Sheets access.");
  if (!session.userAccessCode || ![279, 47, 38].includes(Number(session.userAccessCode)))
    return accessRestricted("Your profile cannot delete logsheets.");

  const query = await searchParams;
  const search = queryValue(query.search);
  const requisition = queryValue(query.requisition);
  const mode = queryValue(query.mode).toUpperCase() === "GP" ? "GP" : "GG";
  const vmfCode = Number(queryValue(query.vmfCode));
  const requestedPage = pageValue(query.page);
  const message = statusMessage(query);
  try {
    const options = await getVehicleOptions();
    const matches = filterVehicles(options, search, mode);
    const selectedVehicle =
      Number.isInteger(vmfCode) && vmfCode > 0
        ? options.find((vehicle) => vehicle.vmfCode === vmfCode)
        : null;
    const recordPage =
      requisition || selectedVehicle
        ? await getLogsheetsPage({
            page: requestedPage,
            pageSize: PAGE_SIZE,
            requisition: requisition || undefined,
            vmfCode: requisition ? undefined : selectedVehicle?.vmfCode,
          })
        : null;
    const filtered = recordPage?.items ?? [];
    const pageValues = {
      ...(search ? { search } : {}),
      mode,
      ...(requisition ? { requisition } : {}),
      ...(selectedVehicle ? { vmfCode } : {}),
    };
    const returnPath = pageHref(pageValues, recordPage?.page ?? 1);
    return (
      <LogsheetShell
        title="Delete a Logsheet"
        description="Search for a captured logsheet before removing it."
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
          action="/log-sheets/delete"
          search={search}
          mode={mode}
          vmfCode={selectedVehicle ? String(vmfCode) : ""}
          options={matches}
          requisition={requisition}
          resetPage
        />
        {filtered.length > 0 || requisition || selectedVehicle ? (
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="logsheet-delete-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">
                  {recordPage?.total ?? 0} record{recordPage?.total === 1 ? "" : "s"}
                </p>
                <h2 id="logsheet-delete-results-title">Matching logsheets</h2>
              </div>
            </div>
            <LogsheetTable records={filtered} mode="delete" returnPath={returnPath} />
            {recordPage && recordPage.totalPages > 1 ? (
              <nav className="vehicle-pagination" aria-label="Matching logsheets pages">
                {recordPage.page > 1 ? (
                  <Link
                    className="vehicle-pagination-button"
                    href={pageHref(pageValues, recordPage.page - 1)}
                  >
                    Previous
                  </Link>
                ) : (
                  <span className="vehicle-pagination-button vehicle-pagination-disabled">
                    Previous
                  </span>
                )}
                <span className="vehicle-pagination-meta" aria-live="polite">
                  Page {recordPage.page} of {recordPage.totalPages}
                </span>
                {recordPage.page < recordPage.totalPages ? (
                  <Link
                    className="vehicle-pagination-button"
                    href={pageHref(pageValues, recordPage.page + 1)}
                  >
                    Next
                  </Link>
                ) : (
                  <span className="vehicle-pagination-button vehicle-pagination-disabled">
                    Next
                  </span>
                )}
              </nav>
            ) : null}
          </section>
        ) : (
          <p className="muted-copy">Search by requisition or vehicle to load logsheets.</p>
        )}
        <div className="button-row">
          <Link className="button button-secondary" href="/log-sheets">
            Main menu
          </Link>
        </div>
      </LogsheetShell>
    );
  } catch (error) {
    const messageText =
      error instanceof LogsheetApiError || error instanceof VehicleApiError
        ? "The Logsheet service is temporarily unavailable. Please try again."
        : "Logsheets could not be loaded.";
    return (
      <LogsheetShell
        title="Delete a Logsheet"
        description="Search for a captured logsheet before removing it."
      >
        <section className="vehicle-status-card" role="alert">
          <h2>{messageText}</h2>
          <Link className="button button-secondary" href="/log-sheets/delete">
            Try again
          </Link>
        </section>
      </LogsheetShell>
    );
  }
}

export default function LogsheetDeletePage(props: Parameters<typeof LogsheetDeletePageContent>[0]) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LogsheetDeletePageContent {...props} />
    </Suspense>
  );
}
