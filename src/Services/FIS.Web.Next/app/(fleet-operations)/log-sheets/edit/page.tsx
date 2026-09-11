import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";

import {
  LogsheetForm,
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
import { getSites, SiteApiError } from "@/lib/api/reference-data/api-sites";
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
  return `/log-sheets/edit${query ? `?${query}` : ""}`;
}

async function LogsheetEditPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getLogsheetSession();
  const problem = sessionMessage(session, "/log-sheets/edit");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasLogsheetAccess(session))
    return accessRestricted("Your profile does not include Reports access.");
  if (!session.userAccessCode || ![279, 47, 38].includes(Number(session.userAccessCode)))
    return accessRestricted("Your profile cannot edit logsheets.");

  const query = await searchParams;
  const search = queryValue(query.search);
  const requisition = queryValue(query.requisition);
  const mode = queryValue(query.mode).toUpperCase() === "GP" ? "GP" : "GG";
  const vmfCode = Number(queryValue(query.vmfCode));
  const editCode = Number(queryValue(query.edit));
  const requestedPage = pageValue(query.page);
  const message = statusMessage(query);
  try {
    const [options, sites] = await Promise.all([getVehicleOptions(), getSites()]);
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
    const selectedRecords = recordPage?.items ?? [];
    const editing =
      Number.isInteger(editCode) && editCode > 0
        ? (selectedRecords.find((record) => record.logCode === editCode) ?? null)
        : null;
    const pageValues = {
      ...(search ? { search } : {}),
      mode,
      ...(requisition ? { requisition } : {}),
      ...(selectedVehicle ? { vmfCode } : {}),
    };
    const returnPath = pageHref(pageValues, recordPage?.page ?? 1);
    return (
      <LogsheetShell
        title="Edit Logsheets"
        description="Search by requisition or vehicle, then update captured values."
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
          action="/log-sheets/edit"
          search={search}
          mode={mode}
          vmfCode={selectedVehicle ? String(vmfCode) : ""}
          options={matches}
          requisition={requisition}
          resetPage
        />
        {selectedRecords.length > 0 || requisition || selectedVehicle ? (
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="logsheet-edit-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">
                  {recordPage?.total ?? 0} record{recordPage?.total === 1 ? "" : "s"}
                </p>
                <h2 id="logsheet-edit-results-title">Matching logsheets</h2>
              </div>
            </div>
            <LogsheetTable records={selectedRecords} mode="edit" returnPath={returnPath} />
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
        {editing ? (
          <LogsheetForm
            record={editing}
            vmfCode={editing.vmfCode}
            sites={sites}
            returnPath={returnPath}
          />
        ) : null}
        <div className="button-row">
          <Link className="button button-secondary" href="/log-sheets">
            Main menu
          </Link>
        </div>
      </LogsheetShell>
    );
  } catch (error) {
    const messageText =
      error instanceof LogsheetApiError ||
      error instanceof VehicleApiError ||
      error instanceof SiteApiError
        ? "The Logsheet service is temporarily unavailable. Please try again."
        : "Logsheets could not be loaded.";
    return (
      <LogsheetShell
        title="Edit Logsheets"
        description="Search by requisition or vehicle, then update captured values."
      >
        <section className="vehicle-status-card" role="alert">
          <h2>{messageText}</h2>
          <Link className="button button-secondary" href="/log-sheets/edit">
            Try again
          </Link>
        </section>
      </LogsheetShell>
    );
  }
}

export default function LogsheetEditPage(props: Parameters<typeof LogsheetEditPageContent>[0]) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LogsheetEditPageContent {...props} />
    </Suspense>
  );
}
