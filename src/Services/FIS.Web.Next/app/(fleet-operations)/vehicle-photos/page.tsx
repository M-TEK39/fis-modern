import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import {
  getQueryValue,
  hasVehicleManagementPermission,
} from "@/app/(administration)/drivers/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import RouteLoading from "@/components/app-shell/route-loading";
import {
  getVehicleSearchCriteria,
  searchVehiclePhotosPage,
  VEHICLE_PHOTO_PAGE_SIZE,
  VehiclePhotoApiError,
  type VehiclePhotoSearchPage,
  type VehiclePhotoSearchRecord,
} from "@/lib/api/vehicles/api-vehicle-photos";
import { getSession } from "@/lib/auth/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
type ResolvedSearchParams = Record<string, string | string[] | undefined>;
type SearchMode = "gg" | "gp";

function formatDate(value: string | null) {
  if (!value) return "-";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value.slice(0, 10) : date.toLocaleDateString("en-ZA");
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || value === "" ? "-" : String(value);
}

function pageHref(
  searchParams: ResolvedSearchParams,
  query: string,
  mode: SearchMode,
  nextPage: number,
) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(searchParams)) {
    if (key === "page" || key === "q" || key === "keyword" || key === "mode") continue;
    for (const item of Array.isArray(value) ? value : value === undefined ? [] : [value]) {
      params.append(key, item);
    }
  }
  if (query) {
    params.set("q", query);
    params.set("mode", mode);
    if (nextPage > 1) params.set("page", String(nextPage));
  }
  const queryString = params.toString();
  return queryString ? `/vehicle-photos?${queryString}` : "/vehicle-photos";
}

function StatusCard({
  title,
  message,
  routePath = "/vehicle-photos",
}: Readonly<{ title: string; message: string; routePath?: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">{title}</p>
      <h2>{message}</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href={routePath}>
          Try again
        </Link>
        <Link className="button button-secondary" href="/home">
          Home
        </Link>
      </div>
    </section>
  );
}

function VehicleResults({
  resultPage,
  searchParams,
  query,
  mode,
}: Readonly<{
  resultPage: VehiclePhotoSearchPage;
  searchParams: ResolvedSearchParams;
  query: string;
  mode: SearchMode;
}>) {
  const { items, page: currentPage, pageSize, total, totalPages } = resultPage;

  if (items.length === 0)
    return (
      <div className="vehicle-empty-state" role="status" aria-live="polite">
        <p className="eyebrow">No vehicles found</p>
        <p>No vehicle matched the selected {mode.toUpperCase()} search.</p>
      </div>
    );

  return (
    <>
      <div className="table-container">
        <div className="table-header">
          <span className="table-title">
            {total} vehicle{total === 1 ? "" : "s"} · {pageSize} per page
          </span>
        </div>
        <div className="table-wrapper">
          <table className="data-table">
            <caption className="sr-only">Vehicle photo search results</caption>
            <thead>
              <tr>
                <th scope="col">GG Number</th>
                <th scope="col">Registration Number</th>
                <th scope="col">Make and Model</th>
                <th scope="col">Year Manufactured</th>
                <th scope="col">Colour</th>
                <th scope="col">Hire Type</th>
                <th scope="col">Status</th>
                <th scope="col">Hired From</th>
                <th scope="col">Status Date</th>
                <th scope="col">Actions</th>
              </tr>
            </thead>
            <tbody>
              {items.map((vehicle) => (
                <tr key={vehicle.vmfCode}>
                  <td>{valueOrDash(vehicle.ggNumber)}</td>
                  <td>{valueOrDash(vehicle.registrationNumber)}</td>
                  <td>{valueOrDash(vehicle.makeAndModel)}</td>
                  <td>{valueOrDash(vehicle.yearManufactured)}</td>
                  <td>{valueOrDash(vehicle.colour)}</td>
                  <td>{valueOrDash(vehicle.hireType)}</td>
                  <td>{valueOrDash(vehicle.status)}</td>
                  <td>{valueOrDash(vehicle.hiredFrom)}</td>
                  <td>{formatDate(vehicle.statusDate)}</td>
                  <td>
                    <Link
                      className="button button-secondary button-small"
                      href={`/vehicle-photos/manage/${vehicle.vmfCode}?keyword=${encodeURIComponent(query)}`}
                    >
                      Manage photos
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
      <nav className="vehicle-pagination" aria-label="Vehicle photo search pagination">
        {currentPage <= 1 ? (
          <span
            className="vehicle-pagination-button vehicle-pagination-disabled"
            aria-disabled="true"
          >
            Previous
          </span>
        ) : (
          <Link
            className="vehicle-pagination-button"
            href={pageHref(searchParams, query, mode, currentPage - 1)}
          >
            Previous
          </Link>
        )}
        <span className="vehicle-pagination-meta" aria-live="polite">
          Page {currentPage} of {totalPages} · {total} total vehicles
        </span>
        {currentPage >= totalPages ? (
          <span
            className="vehicle-pagination-button vehicle-pagination-disabled"
            aria-disabled="true"
          >
            Next
          </span>
        ) : (
          <Link
            className="vehicle-pagination-button"
            href={pageHref(searchParams, query, mode, currentPage + 1)}
          >
            Next
          </Link>
        )}
      </nav>
    </>
  );
}

async function VehiclePhotosPageContent({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/vehicle-photos" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard title="API unavailable" message="Vehicle photo search is unavailable." />
      </main>
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="Access restricted"
          message="You do not have permission to maintain vehicle photos."
        />
      </main>
    );

  const query = await searchParams;
  const searchQuery = (getQueryValue(query.q) ?? getQueryValue(query.keyword) ?? "").trim();
  const mode: SearchMode = getQueryValue(query.mode)?.toLowerCase() === "gp" ? "gp" : "gg";
  const requestedPage = Number.parseInt(getQueryValue(query.page) ?? "1", 10);
  const page = Number.isFinite(requestedPage) ? Math.max(1, requestedPage) : 1;

  try {
    const criteria = await getVehicleSearchCriteria();
    const resultPage = searchQuery
      ? await searchVehiclePhotosPage(searchQuery, mode === "gp" ? "GP" : "GG", page)
      : {
          items: [],
          page: 1,
          pageSize: VEHICLE_PHOTO_PAGE_SIZE,
          total: 0,
          totalPages: 1,
        };
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="vehicle-photos-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Vehicle administration</p>
              <h1 id="vehicle-photos-title">Vehicle Photo Upload</h1>
              <p>
                Search for vehicles by GG or registration number, then manage their photo
                references.
              </p>
            </div>
            <div className="button-row">
              <Link className="button button-secondary" href="/home">
                Home
              </Link>
              <form action={logoutAction}>
                <button className="button button-secondary" type="submit">
                  Sign out
                </button>
              </form>
            </div>
          </header>
          <form action="/vehicle-photos" className="vehicle-photo-search-form" method="get">
            <fieldset>
              <legend>Search for a vehicle</legend>
              <div className="vehicle-photo-search-options">
                <label>
                  <input type="radio" name="mode" value="gg" defaultChecked={mode === "gg"} /> GG
                  number
                </label>
                <label>
                  <input type="radio" name="mode" value="gp" defaultChecked={mode === "gp"} />{" "}
                  Registration number
                </label>
              </div>
              <div className="field">
                <label htmlFor="vehicle-photo-search">Search keyword</label>
                <input
                  id="vehicle-photo-search"
                  name="q"
                  type="search"
                  list="vehicle-photo-keywords"
                  defaultValue={searchQuery}
                  placeholder={mode === "gg" ? "GG number" : "Registration number"}
                  autoComplete="off"
                  required
                />
                <datalist id="vehicle-photo-keywords">
                  {criteria.map((keyword) => (
                    <option key={keyword} value={keyword} />
                  ))}
                </datalist>
              </div>
              <button className="button button-primary" type="submit">
                Search
              </button>
            </fieldset>
          </form>
          {searchQuery ? (
            <VehicleResults
              resultPage={resultPage}
              searchParams={query}
              query={searchQuery}
              mode={mode}
            />
          ) : (
            <div className="vehicle-empty-state" role="status" aria-live="polite">
              <p className="eyebrow">Ready to search</p>
              <p>Enter a GG or registration number to find a vehicle.</p>
            </div>
          )}
          <div className="vehicle-footer-actions">
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
          </div>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof VehiclePhotoApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/vehicle-photos" />
        </main>
      );
    console.error(
      "FIS vehicle photo search failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="API unavailable"
          message="Vehicle photo search could not be loaded."
          routePath={pageHref(query, searchQuery, mode, page)}
        />
      </main>
    );
  }
}

export default function VehiclePhotosPage(props: Readonly<{ searchParams: SearchParams }>) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <VehiclePhotosPageContent {...props} />
    </Suspense>
  );
}
