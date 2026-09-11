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

const REPORTS_ROLE = "Reports";

export type FineDeletePageProps = {
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
  return roles.some(
    (role) => role.localeCompare(REPORTS_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function buildDetailHref(fineCode: number) {
  return `/fines/delete/detail?fineId=${encodeURIComponent(fineCode)}`;
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
    <nav className="vehicle-pagination" aria-label="Fine deletion pages">
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

function SearchForm({
  searchType,
  searchQuery,
}: Readonly<{ searchType: FineSearchType; searchQuery: string }>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <SearchTypeFieldset selectedType={searchType} legend="Search by" />
      <div className="vehicle-search-row">
        <label className="sr-only" htmlFor="fine-delete-search">
          {searchType === "GG" ? "GG number" : "GP number"}
        </label>
        <input
          className="vehicle-search"
          id="fine-delete-search"
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

function Resolution({
  searchType,
  vehicles,
}: Readonly<{ searchType: FineSearchType; vehicles: FineVehicleOption[] }>) {
  if (vehicles.length === 0) {
    return null;
  }

  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="fine-delete-resolution-title"
    >
      <p className="eyebrow">Vehicle resolution</p>
      <h2 id="fine-delete-resolution-title">Matched vehicle{vehicles.length === 1 ? "" : "s"}</h2>
      <ul className="form-hint fis-list-pad">
        {vehicles.slice(0, 5).map((vehicle) => (
          <li key={vehicle.vmfCode}>
            {vehicle.fleetNumber ?? "-"} / {vehicle.registrationNumber ?? "-"} ({vehicle.vmfCode})
            {searchType === "GP" && vehicle.isHistoricalMatch && vehicle.matchedRegistration
              ? ` — historical GP ${vehicle.matchedRegistration}`
              : ""}
          </li>
        ))}
      </ul>
    </section>
  );
}

function FineRows({ fines }: Readonly<{ fines: FineRecord[] }>) {
  if (fines.length === 0) {
    return (
      <div className="vehicle-empty-state">
        <p className="eyebrow">No records found</p>
        <h2>No fines matched this vehicle.</h2>
        <p className="muted-copy">Try another GG or GP number.</p>
      </div>
    );
  }

  return (
    <div className="vehicle-table-wrapper" aria-live="polite">
      <table className="vehicle-table">
        <caption className="sr-only">Fines available for deletion</caption>
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
              <td>{fine.documentType ?? fine.offenceName ?? "-"}</td>
              <td>{formatDate(fine.receiveGgDate)}</td>
              <td>
                <Link
                  className="button button-danger button-small"
                  href={buildDetailHref(fine.fineCode)}
                >
                  DELETE
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
      <h2>Fines could not be loaded for deletion.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href="/fines/delete">
        Try again
      </Link>
    </section>
  );
}

const FineDeletePageContent = renderFineDeletePageContent;

async function renderFineDeletePageContent({
  searchParams,
  routePath = "/fines/delete",
}: FineDeletePageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") {
    redirect("/login");
  }
  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  }
  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }
  if (!hasReportsRole(session.roles)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to delete Fines.</h2>
        </section>
      </main>
    );
  }

  const query = await searchParams;
  const searchType = getSearchType(getQueryValue(query.searchType) ?? getQueryValue(query.Radio1));
  const searchQuery = (getQueryValue(query.searchQuery) ?? getQueryValue(query.txtGGNum) ?? "")
    .trim()
    .slice(0, 8);
  const requestedPage = getPositiveQueryInt(getQueryValue(query.page)) ?? 1;
  const error = getQueryValue(query.error);
  const deleted = getQueryValue(query.deleted) === "1";

  try {
    const [result, vehicles] = await Promise.all([
      getFinePage(searchType, searchQuery, requestedPage, DEFAULT_FINE_PAGE_SIZE),
      searchQuery
        ? searchFineVehicles(searchType, searchQuery)
        : Promise.resolve([] as FineVehicleOption[]),
    ]);

    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="fine-delete-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Fines maintenance</p>
              <h1 id="fine-delete-title">Delete Fines</h1>
              <p>
                Find a fine by GG or current/historical GP number, review it, then confirm deletion.
              </p>
            </div>
            <Link className="button button-secondary" href="/fines">
              Fines Menu
            </Link>
          </header>
          {deleted ? (
            <div className="notice notice-success" role="status">
              Fine deleted successfully.
            </div>
          ) : null}
          {error ? (
            <div className="notice notice-error" role="alert">
              {error}
            </div>
          ) : null}
          <SearchForm searchType={searchType} searchQuery={searchQuery} />
          <Resolution searchType={searchType} vehicles={vehicles} />
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="fine-delete-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Fine maintenance</p>
                <h2 id="fine-delete-results-title">Fines found</h2>
              </div>
            </div>
            <FineRows fines={result.items} />
            <FinePagination
              routePath={routePath}
              searchType={searchType}
              searchQuery={searchQuery}
              page={result.page}
              totalPages={result.totalPages}
            />
          </section>
          <div className="vehicle-footer-actions">
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
          </div>
        </section>
      </main>
    );
  } catch (requestError) {
    if (requestError instanceof FineApiError && requestError.reason === "unauthorized") {
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    }
    console.error(
      "FIS fine deletion lookup failed",
      requestError instanceof Error ? requestError.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }
}

export default function FineDeletePage(props: FineDeletePageProps) {
  return (
    <StreamedRoute>
      <FineDeletePageContent {...props} />
    </StreamedRoute>
  );
}
