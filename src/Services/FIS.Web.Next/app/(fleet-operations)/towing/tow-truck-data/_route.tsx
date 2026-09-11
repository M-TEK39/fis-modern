import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";
import {
  getTowTruckPage,
  TowingApiError,
  type TowTruckRecord,
} from "@/lib/api/fleet-operations/api-towing";

const TOWING_ROLE = "Towing";

export type TowTruckDataPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
function hasTowingRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(TOWING_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}
function valueOrDash(value: string | null | undefined) {
  return value?.trim() || "-";
}

function SearchForm({ searchQuery }: Readonly<{ searchQuery: string }>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <div className="vehicle-search-row">
        <label className="sr-only" htmlFor="tow-truck-name-search">
          Tow truck name
        </label>
        <input
          className="vehicle-search"
          id="tow-truck-name-search"
          name="searchQuery"
          maxLength={30}
          defaultValue={searchQuery}
          placeholder="Enter tow truck name"
        />
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Submit
        </button>
        <Link className="button button-secondary" href="/towing">
          Menu
        </Link>
        <Link className="button button-secondary" href="/towing/tow-truck-data/detail">
          New tow truck
        </Link>
      </div>
    </form>
  );
}

function pageHref(routePath: string, searchQuery: string, page: number) {
  return `${routePath}?${new URLSearchParams({ searchQuery, page: String(page) }).toString()}`;
}

function TowTruckPagination({
  routePath,
  searchQuery,
  page,
  totalPages,
}: Readonly<{ routePath: string; searchQuery: string; page: number; totalPages: number }>) {
  if (!searchQuery || totalPages <= 1) return null;
  return (
    <nav className="vehicle-pagination" aria-label="Tow truck provider pages">
      {page > 1 ? (
        <Link
          className="vehicle-pagination-button"
          href={pageHref(routePath, searchQuery, page - 1)}
        >
          Previous
        </Link>
      ) : (
        <span className="vehicle-pagination-button vehicle-pagination-disabled">Previous</span>
      )}
      <span className="vehicle-pagination-meta" aria-live="polite">
        Page {page} of {totalPages}
      </span>
      {page < totalPages ? (
        <Link
          className="vehicle-pagination-button"
          href={pageHref(routePath, searchQuery, page + 1)}
        >
          Next
        </Link>
      ) : (
        <span className="vehicle-pagination-button vehicle-pagination-disabled">Next</span>
      )}
    </nav>
  );
}

function Rows({
  trucks,
  searchQuery,
}: Readonly<{ trucks: TowTruckRecord[]; searchQuery: string }>) {
  if (!searchQuery)
    return (
      <div className="vehicle-empty-state">
        <p className="eyebrow">Search required</p>
        <h2>Search for a tow truck by name.</h2>
        <p className="muted-copy">
          The legacy screen uses the tow-company name to find records, and also allows a new record
          to be captured.
        </p>
      </div>
    );
  if (trucks.length === 0)
    return (
      <div className="vehicle-empty-state">
        <p className="eyebrow">No records found</p>
        <h2>No tow truck matched “{searchQuery}”.</h2>
        <p className="muted-copy">
          You can capture a new assistance firm if this is a new provider.
        </p>
        <Link
          className="button button-primary"
          href={`/towing/tow-truck-data/detail?${new URLSearchParams({ towName: searchQuery }).toString()}`}
        >
          Capture new tow truck
        </Link>
      </div>
    );
  return (
    <div className="vehicle-table-wrapper" aria-live="polite">
      <table className="vehicle-table">
        <caption className="sr-only">Tow truck companies</caption>
        <thead>
          <tr>
            <th scope="col">Tow Truck Name</th>
            <th scope="col">Area Operate</th>
            <th scope="col">Tel</th>
            <th scope="col">Action</th>
          </tr>
        </thead>
        <tbody>
          {trucks.map((truck) => (
            <tr key={truck.towCode}>
              <td>{valueOrDash(truck.name)}</td>
              <td>{valueOrDash(truck.area)}</td>
              <td>{valueOrDash(truck.telephone)}</td>
              <td>
                <div className="button-row">
                  <Link
                    className="button button-secondary button-small"
                    href={`/towing/tow-truck-data/detail?towId=${truck.towCode}`}
                  >
                    Mod
                  </Link>
                  <Link
                    className="button button-danger button-small"
                    href={`/towing/tow-truck-data/detail?towId=${truck.towCode}&confirmDelete=1`}
                  >
                    Del
                  </Link>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

async function TowTruckDataPageContent({
  searchParams,
  routePath = "/towing/tow-truck-data",
}: TowTruckDataPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Tow truck data could not be loaded.</h2>
          <p className="muted-copy">Retry when the FIS API is available.</p>
        </section>
      </main>
    );
  if (!hasTowingRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to maintain tow truck data.</h2>
        </section>
      </main>
    );

  const query = await searchParams;
  const searchQuery = (getQueryValue(query.searchQuery) ?? getQueryValue(query.xName) ?? "")
    .trim()
    .slice(0, 30);
  const requestedPage = Math.max(1, Number.parseInt(getQueryValue(query.page) ?? "1", 10) || 1);
  const notice =
    getQueryValue(query.error) ??
    (getQueryValue(query.saved) === "1"
      ? "Tow truck captured successfully."
      : getQueryValue(query.updated) === "1"
        ? "Tow truck updated successfully."
        : getQueryValue(query.deleted) === "1"
          ? "Tow truck deleted successfully."
          : "");
  try {
    const result = searchQuery ? await getTowTruckPage(searchQuery, requestedPage) : null;
    const trucks = result?.items ?? [];
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="tow-truck-data-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Road Side Assistance</p>
              <h1 id="tow-truck-data-title">Road Side Assistance Info Maintenance</h1>
              <p>
                Find, capture, edit, or delete assistance firm information from the legacy Tow_Truck
                table.
              </p>
            </div>
            <Link className="button button-secondary" href="/towing">
              Towing Menu
            </Link>
          </header>
          {notice ? (
            <div
              className={
                getQueryValue(query.error) ? "notice notice-error" : "notice notice-success"
              }
              role={getQueryValue(query.error) ? "alert" : "status"}
            >
              {notice}
            </div>
          ) : null}
          <SearchForm searchQuery={searchQuery} />
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="tow-truck-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Existing providers</p>
                <h2 id="tow-truck-results-title">Tow truck records</h2>
              </div>
            </div>
            <Rows trucks={trucks} searchQuery={searchQuery} />
            {result ? (
              <TowTruckPagination
                routePath={routePath}
                searchQuery={searchQuery}
                page={result.page}
                totalPages={result.totalPages}
              />
            ) : null}
          </section>
          <div className="vehicle-footer-actions">
            <Link className="button button-secondary" href="/towing/tow-truck-data/report">
              All firm data
            </Link>
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
          </div>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof TowingApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS tow truck data request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Tow truck data could not be loaded.</h2>
          <Link className="button button-primary" href={routePath}>
            Try again
          </Link>
        </section>
      </main>
    );
  }
}

export default function TowTruckDataPage(props: Parameters<typeof TowTruckDataPageContent>[0]) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <TowTruckDataPageContent {...props} />
    </Suspense>
  );
}

type LegacyTowTruckDataPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export function createLegacyTowTruckDataPage(routePath: string) {
  return function LegacyTowTruckDataPage({ searchParams }: Readonly<LegacyTowTruckDataPageProps>) {
    return <TowTruckDataPage routePath={routePath} searchParams={searchParams} />;
  };
}
