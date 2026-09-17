import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import SearchTypeFieldset from "@/components/ui/search-type-fieldset";
import {
  DEFAULT_FINE_PAGE_SIZE,
  FineApiError,
  getFinePage,
  searchFineVehicles,
  type FineRecord,
  type FineSearchType,
  type FineVehicleOption,
} from "@/lib/api/fleet-operations/api-fines";
import { getSession } from "@/lib/auth/session";
import { hasFinesAccess } from "@/app/(fleet-operations)/fines/access";

export type FineMaintenancePageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getSearchType(value: string | undefined): FineSearchType {
  return value === "GG" || value === "Radiogg" ? "GG" : "GP";
}

function getPositiveQueryInt(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasReportsRole(roles: readonly string[]) {
  return hasFinesAccess(roles);
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function buildDetailHref(searchType: FineSearchType, searchQuery: string, vmfCode?: number | null) {
  const params = new URLSearchParams();
  if (vmfCode) {
    params.set("vmfCode", String(vmfCode));
  }
  if (searchQuery) {
    params.set("searchType", searchType);
    params.set("searchQuery", searchQuery);
  }
  const query = params.toString();
  return `/fines/maintenance/detail${query ? `?${query}` : ""}`;
}

function pageHref(
  routePath: string,
  searchType: FineSearchType,
  searchQuery: string,
  page: number,
) {
  return `${routePath}?${new URLSearchParams({ searchType, searchQuery, page: String(page) }).toString()}`;
}

function FinePagination({
  routePath,
  searchType,
  searchQuery,
  page,
  totalPages,
}: Readonly<{
  routePath: string;
  searchType: FineSearchType;
  searchQuery: string;
  page: number;
  totalPages: number;
}>) {
  if (totalPages <= 1) return null;

  return (
    <nav className="vehicle-pagination" aria-label="Fine maintenance pages">
      {page > 1 ? (
        <Link
          className="vehicle-pagination-button"
          href={pageHref(routePath, searchType, searchQuery, page - 1)}
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
          href={pageHref(routePath, searchType, searchQuery, page + 1)}
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

function FineSearchForm({
  searchType,
  searchQuery,
}: Readonly<{ searchType: FineSearchType; searchQuery: string }>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <SearchTypeFieldset selectedType={searchType} legend="Search by" />
      <div className="vehicle-search-row">
        <label className="sr-only" htmlFor="fine-vehicle-search">
          {searchType === "GG" ? "GG number" : "GP number"}
        </label>
        <input
          className="vehicle-search"
          id="fine-vehicle-search"
          maxLength={8}
          name="searchQuery"
          placeholder={
            searchType === "GG" ? "Enter GG number" : "Enter current or historical GP number"
          }
          defaultValue={searchQuery}
        />
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Submit
        </button>
        <Link className="button button-secondary" href="/fines">
          Menu
        </Link>
      </div>
    </form>
  );
}

function VehicleResolution({
  searchType,
  vehicles,
}: Readonly<{ searchType: FineSearchType; vehicles: FineVehicleOption[] }>) {
  if (vehicles.length === 0) {
    return null;
  }

  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="fine-vehicle-resolution-title"
    >
      <p className="eyebrow">Vehicle resolution</p>
      <h2 id="fine-vehicle-resolution-title">Matched vehicle{vehicles.length === 1 ? "" : "s"}</h2>
      <ul className="form-hint fis-list-pad">
        {vehicles.slice(0, 5).map((vehicle) => (
          <li key={vehicle.vmfCode}>
            {valueOrDash(vehicle.fleetNumber)} / {valueOrDash(vehicle.registrationNumber)} (
            {vehicle.vmfCode})
            {searchType === "GP" && vehicle.isHistoricalMatch && vehicle.matchedRegistration
              ? ` — historical GP ${vehicle.matchedRegistration}`
              : ""}
          </li>
        ))}
      </ul>
    </section>
  );
}

function FineTable({
  fines,
  searchType,
  searchQuery,
}: Readonly<{ fines: FineRecord[]; searchType: FineSearchType; searchQuery: string }>) {
  if (fines.length === 0) {
    return (
      <div className="vehicle-empty-state">
        <p className="eyebrow">No records found</p>
        <h2>{searchQuery ? `No fines matched “${searchQuery}”.` : "No fines are available."}</h2>
        <p className="muted-copy">
          Try another GG or GP number, or add a fine for a selected vehicle.
        </p>
      </div>
    );
  }

  return (
    <div className="vehicle-table-wrapper" aria-live="polite">
      <table className="vehicle-table">
        <caption className="sr-only">Fines available for maintenance</caption>
        <DataTableHeader
          columns={[
            { key: "column-1", label: <>Offence Date</> },
            { key: "column-2", label: <>Document Type</> },
            { key: "column-3", label: <>Date at GMT</> },
            { key: "column-4", label: <>Action</> },
          ]}
        />
        <tbody>
          {fines.map((fine) => (
            <tr key={fine.fineCode}>
              <td>{formatDate(fine.offenceDate)}</td>
              <td>{valueOrDash(fine.documentType ?? fine.offenceName)}</td>
              <td>{formatDate(fine.receiveGgDate)}</td>
              <td>
                <Link
                  className="button button-secondary button-small"
                  href={`/fines/maintenance/detail?fineId=${fine.fineCode}`}
                >
                  Mod
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>Fines maintenance could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/fines/maintenance">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

const FineMaintenancePageContent = renderFineMaintenancePageContent;

async function renderFineMaintenancePageContent({
  searchParams,
  routePath = "/fines/maintenance",
}: FineMaintenancePageProps) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return <SessionRecovery returnPath={routePath} />;
  }

  if (session.status === "unavailable") {
    return <ApiUnavailable />;
  }

  if (!hasReportsRole(session.roles)) {
    return (
      <section className="vehicle-status-card" role="alert">
        <div className="status-icon status-icon-error" aria-hidden="true">
          !
        </div>
        <p className="eyebrow">Access restricted</p>
        <h2>You do not have permission to maintain Fines.</h2>
      </section>
    );
  }

  const query = await searchParams;
  const searchType = getSearchType(getQueryValue(query.searchType) ?? getQueryValue(query.Radio1));
  const searchQuery = (getQueryValue(query.searchQuery) ?? getQueryValue(query.txtGGNum) ?? "")
    .trim()
    .slice(0, 8);
  const requestedPage = getPositiveQueryInt(getQueryValue(query.page)) ?? 1;
  const notice =
    getQueryValue(query.saved) === "1"
      ? "Fine captured successfully."
      : getQueryValue(query.updated) === "1"
        ? "Fine updated successfully."
        : getQueryValue(query.error);

  try {
    const [result, vehicles] = await Promise.all([
      getFinePage(searchType, searchQuery, requestedPage, DEFAULT_FINE_PAGE_SIZE),
      searchQuery
        ? searchFineVehicles(searchType, searchQuery)
        : Promise.resolve([] as FineVehicleOption[]),
    ]);
    const addHref = buildDetailHref(
      searchType,
      searchQuery,
      vehicles.length === 1 ? vehicles[0].vmfCode : null,
    );

    return (
      <>
        {notice ? (
          <div
            className={getQueryValue(query.error) ? "notice notice-error" : "notice notice-success"}
            role="status"
          >
            {notice}
          </div>
        ) : null}
        <FineSearchForm searchType={searchType} searchQuery={searchQuery} />
        <VehicleResolution searchType={searchType} vehicles={vehicles} />
        <section
          className="vehicle-status-maintenance-panel"
          aria-labelledby="fine-maintenance-results-title"
        >
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Fine maintenance</p>
              <h2 id="fine-maintenance-results-title">Fines found</h2>
            </div>
            <Link className="button button-primary" href={addHref}>
              Add
            </Link>
          </div>
          <FineTable fines={result.items} searchType={searchType} searchQuery={searchQuery} />
          <FinePagination
            routePath={routePath}
            searchType={searchType}
            searchQuery={searchQuery}
            page={result.page}
            totalPages={result.totalPages}
          />
        </section>
      </>
    );
  } catch (error) {
    if (error instanceof FineApiError && error.reason === "unauthorized") {
      return <SessionRecovery returnPath={routePath} />;
    }

    console.error(
      "FIS fine maintenance request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return <ApiUnavailable />;
  }
}

export default function FineMaintenancePage(props: FineMaintenancePageProps) {
  return (
    <StreamedRoute>
      <FineMaintenancePageContent {...props} />
    </StreamedRoute>
  );
}
