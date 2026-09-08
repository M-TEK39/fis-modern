import Link from "next/link";

import {
  hasTripAuthorityAccess,
  getTripSession,
  parsePositiveInteger,
  queryValue,
  tripAccessRestricted,
  tripSessionMessage,
} from "@/app/trips/_page";
import { FinanceApiError } from "@/lib/api-finance";
import { getTripQueryRows, type TripQueryRow } from "@/lib/api-trip-queries";

type SearchParams = Record<string, string | string[] | undefined>;
const PAGE_SIZE = 12;

function rowMatches(row: TripQueryRow, search: string) {
  if (!search) return true;
  return [row.key, row.vehicle, row.department, row.tripCount, row.kilometres].some((value) =>
    String(value).toLowerCase().includes(search),
  );
}

function filterMatches(row: TripQueryRow, filter: string) {
  if (filter === "vehicle") return row.vehicle !== "Unknown";
  if (filter === "department") return row.department !== "-";
  if (filter === "multiple") return row.tripCount > 1;
  return true;
}

function formatDate(value: string | null) {
  if (!value) return "-";
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? value
    : new Intl.DateTimeFormat("en-ZA", {
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        timeZone: "UTC",
      }).format(date);
}

function formatNumber(value: number) {
  return new Intl.NumberFormat("en-ZA", { maximumFractionDigits: 2 }).format(value);
}

function pageHref(search: string, filter: string, page: number) {
  const params = new URLSearchParams();
  if (search) params.set("search", search);
  if (filter) params.set("filter", filter);
  params.set("page", String(page));
  return `/trip-query?${params.toString()}`;
}

export default async function TripQueryPage({
  searchParams,
}: Readonly<{ searchParams: Promise<SearchParams> }>) {
  const session = await getTripSession();
  const sessionMessage = tripSessionMessage(session, "/trip-query");
  if (sessionMessage) return sessionMessage;
  if (session.status !== "authenticated")
    return tripAccessRestricted("The sign-in service is temporarily unavailable.");
  if (!hasTripAuthorityAccess(session)) return tripAccessRestricted();

  const query = await searchParams;
  const search = queryValue(query.search).trim().toLowerCase();
  const filter = queryValue(query.filter).trim().toLowerCase();
  let rows: TripQueryRow[] = [];
  let error: string | null = null;
  try {
    rows = await getTripQueryRows();
  } catch (caught) {
    error =
      caught instanceof FinanceApiError ? caught.message : "Trip queries could not be loaded.";
  }
  const filteredRows = rows.filter((row) => rowMatches(row, search) && filterMatches(row, filter));
  const totalPages = Math.max(1, Math.ceil(filteredRows.length / PAGE_SIZE));
  const page = Math.min(parsePositiveInteger(queryValue(query.page)) ?? 1, totalPages);
  const visibleRows = filteredRows.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="trip-query-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Trips</p>
            <h1 id="trip-query-title">Trip Queries</h1>
            <p>Review trip summary records from the authenticated reporting service.</p>
          </div>
          <div className="button-row">
            <Link className="button button-secondary" href="/trip-query">
              Refresh
            </Link>
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
          </div>
        </header>
        {error ? (
          <div className="notice notice-error" role="alert">
            {error}
          </div>
        ) : null}
        <form className="vehicle-status-maintenance-panel" method="get">
          <div className="form-grid">
            <div className="form-field">
              <label className="form-label" htmlFor="trip-query-search">
                Search summaries
              </label>
              <input
                className="form-input"
                id="trip-query-search"
                name="search"
                type="search"
                placeholder="Search contract, vehicle, site..."
                defaultValue={search}
              />
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="trip-query-filter">
                Filter
              </label>
              <select
                className="form-select"
                id="trip-query-filter"
                name="filter"
                defaultValue={filter}
              >
                <option value="">All trip summaries</option>
                <option value="vehicle">Vehicle-linked summaries</option>
                <option value="department">Summaries with a site</option>
                <option value="multiple">Multiple trips in summary</option>
              </select>
            </div>
          </div>
          <div className="button-row">
            <button className="button button-primary" type="submit">
              Apply filters
            </button>
            <Link className="button button-secondary" href="/trip-query">
              Clear
            </Link>
          </div>
        </form>
        {filteredRows.length === 0 ? (
          <section className="vehicle-status-card" role="status">
            <h2>No trip queries</h2>
            <p className="muted-copy">No trip summary records matched the current criteria.</p>
          </section>
        ) : (
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="trip-query-results"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">{filteredRows.length} record(s)</p>
                <h2 id="trip-query-results">Trip summary results</h2>
              </div>
            </div>
            <div className="vehicle-table-wrapper">
              <table className="vehicle-table">
                <caption className="sr-only">Trip summary results</caption>
                <thead>
                  <tr>
                    <th scope="col">Contract / VMF</th>
                    <th scope="col">Vehicle</th>
                    <th scope="col">Site / Department</th>
                    <th scope="col">Trips</th>
                    <th scope="col">Kilometres</th>
                    <th scope="col">First Trip</th>
                    <th scope="col">Last Trip</th>
                  </tr>
                </thead>
                <tbody>
                  {visibleRows.map((row, index) => (
                    <tr key={`${row.key}-${index}`}>
                      <td>{row.key}</td>
                      <td>{row.vehicle}</td>
                      <td>{row.department}</td>
                      <td>{formatNumber(row.tripCount)}</td>
                      <td>{formatNumber(row.kilometres)}</td>
                      <td>{formatDate(row.firstTrip)}</td>
                      <td>{formatDate(row.lastTrip)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {totalPages > 1 ? (
              <nav className="vehicle-pagination" aria-label="Trip query pages">
                {page > 1 ? (
                  <Link
                    className="vehicle-pagination-button"
                    href={pageHref(search, filter, page - 1)}
                  >
                    Previous
                  </Link>
                ) : (
                  <span className="vehicle-pagination-button vehicle-pagination-disabled">
                    Previous
                  </span>
                )}
                <span>
                  Page {page} of {totalPages}
                </span>
                {page < totalPages ? (
                  <Link
                    className="vehicle-pagination-button"
                    href={pageHref(search, filter, page + 1)}
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
        )}
        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href="/trip-authorities">
            Trip Authorities
          </Link>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </div>
      </section>
    </main>
  );
}
