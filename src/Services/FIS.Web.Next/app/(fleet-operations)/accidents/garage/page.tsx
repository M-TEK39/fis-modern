import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  AccidentApiError,
  getGarageAccidentPage,
  type GarageSearchType,
} from "@/lib/api/fleet-operations/api-accidents";
import { getSession } from "@/lib/auth/session";

const ACCIDENTS_ROLE = "Accidents";

type GaragePageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getSearchType(value: string | undefined): GarageSearchType {
  return value === "GG" || value === "Radiogg" ? "GG" : "GP";
}

function getPage(value: string | undefined) {
  const parsed = Number.parseInt(value ?? "1", 10);
  return Number.isFinite(parsed) ? parsed : 1;
}

function buildGarageHref(searchType: GarageSearchType, searchTerm: string, page: number) {
  const params = new URLSearchParams({ type: searchType, page: String(page) });
  if (searchTerm) {
    params.set("q", searchTerm);
  }

  return `/accidents/garage?${params.toString()}`;
}

function buildGarageAddHref(searchType: GarageSearchType, searchTerm: string) {
  const params = new URLSearchParams({ type: searchType });
  if (searchTerm) {
    params.set("q", searchTerm);
  }

  return `/accidents/garage/add?${params.toString()}`;
}

function GarageFallback() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Loading page…</p>
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
      <h2>Garage accident records could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/accidents/garage">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to maintain garage accidents.</h2>
      <p className="muted-copy">
        Contact your FIS administrator if you need accident-management access.
      </p>
    </section>
  );
}

function NoRecords({ searchTerm }: { searchTerm: string }) {
  return (
    <div className="vehicle-empty-state">
      <p className="eyebrow">No records found</p>
      <h2>
        {searchTerm
          ? `No accidents matched “${searchTerm}”.`
          : "No garage accidents are available."}
      </h2>
      <p className="muted-copy">Try another GG or GP number, or add a new accident record.</p>
    </div>
  );
}

async function GarageContent({ searchParams }: GaragePageProps) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return <SessionRecovery returnPath="/accidents/garage" />;
  }

  if (session.status === "unavailable") {
    return <ApiUnavailable />;
  }

  if (!hasRole(session.roles, ACCIDENTS_ROLE)) {
    return <AccessRestricted />;
  }

  const query = await searchParams;
  const legacySearchType = getQueryValue(query.Radio1);
  const searchType = getSearchType(getQueryValue(query.type) ?? legacySearchType);
  const searchTerm = (getQueryValue(query.q) ?? getQueryValue(query.txtGGNum) ?? "").trim();
  const pageNumber = getPage(getQueryValue(query.page));
  const created = getQueryValue(query.created) === "1";
  const updated = getQueryValue(query.updated) === "1";

  let pageData;
  try {
    pageData = await getGarageAccidentPage(pageNumber, searchType, searchTerm);
  } catch (error) {
    if (error instanceof AccidentApiError && error.reason === "unauthorized") {
      return <SessionRecovery returnPath="/accidents/garage" />;
    }

    console.error(
      "FIS garage accident request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return <ApiUnavailable />;
  }

  return (
    <>
      {created ? (
        <div className="notice notice-success" role="status">
          Accident created successfully.
        </div>
      ) : null}
      {updated ? (
        <div className="notice notice-success" role="status">
          Accident updated successfully.
        </div>
      ) : null}
      <form className="accident-garage-search" method="get">
        <fieldset className="accident-garage-search-options">
          <legend>Search by</legend>
          <label className="vehicle-checkbox-label">
            <input type="radio" name="type" value="GG" defaultChecked={searchType === "GG"} /> GG
          </label>
          <label className="vehicle-checkbox-label">
            <input type="radio" name="type" value="GP" defaultChecked={searchType === "GP"} /> GP
          </label>
        </fieldset>
        <div className="vehicle-search-row">
          <label className="sr-only" htmlFor="garage-search">
            {searchType === "GG" ? "GG Number" : "GP Number"}
          </label>
          <input
            className="vehicle-search"
            id="garage-search"
            maxLength={8}
            name="q"
            placeholder={searchType === "GG" ? "Enter GG number" : "Enter GP number"}
            defaultValue={searchTerm}
          />
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            Submit
          </button>
          <Link className="button button-secondary" href="/accidents">
            Menu
          </Link>
        </div>
      </form>

      {pageData.totalRecords === 0 ? (
        <NoRecords searchTerm={searchTerm} />
      ) : (
        <>
          <div className="vehicle-table-wrapper" aria-live="polite">
            <table className="vehicle-table">
              <caption className="sr-only">Garage accident records</caption>
              <thead>
                <tr>
                  <th scope="col">Vehicle Number</th>
                  <th scope="col">Hire Type</th>
                  <th scope="col">Accident Date</th>
                  <th scope="col">GG Reference</th>
                  <th scope="col">Action</th>
                </tr>
              </thead>
              <tbody>
                {pageData.rows.map((row) => (
                  <tr key={row.accidentCode}>
                    <td>{row.vehicleNumber ?? "-"}</td>
                    <td>{row.hireType ?? "-"}</td>
                    <td>{row.accidentDate?.slice(0, 10) ?? "-"}</td>
                    <td>{row.reference ?? "-"}</td>
                    <td>
                      <Link
                        className="button button-secondary button-small"
                        href={`/accidents/garage/edit?accidentId=${encodeURIComponent(row.accidentCode)}`}
                      >
                        Edit
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {pageData.totalPages > 1 ? (
            <nav className="vehicle-pagination" aria-label="Garage accident pages">
              {pageData.page > 1 ? (
                <Link
                  className="vehicle-pagination-button"
                  href={buildGarageHref(searchType, searchTerm, pageData.page - 1)}
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
              <span>
                Page {pageData.page} of {pageData.totalPages}
              </span>
              {pageData.page < pageData.totalPages ? (
                <Link
                  className="vehicle-pagination-button"
                  href={buildGarageHref(searchType, searchTerm, pageData.page + 1)}
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
          ) : null}

          <p className="vehicle-pagination-meta">
            Total records: {pageData.totalRecords} | Page size: {pageData.pageSize}
          </p>
        </>
      )}

      <div className="vehicle-footer-actions">
        <Link className="button button-primary" href={buildGarageAddHref(searchType, searchTerm)}>
          Add New
        </Link>
        <Link className="button button-secondary" href="/accidents">
          Menu
        </Link>
        <Link className="button button-secondary" href="/home">
          Home
        </Link>
        <form action={logoutAction}>
          <button className="button button-secondary" type="submit">
            Sign out
          </button>
        </form>
      </div>
    </>
  );
}

export default function GarageAccidentPage({ searchParams }: GaragePageProps) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="garage-accidents-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Accident maintenance</p>
            <h1 id="garage-accidents-title">Accident Maintenance (Garage)</h1>
            <p>Search by GG or GP number, then edit or add accidents.</p>
          </div>
          <Link className="button button-secondary" href="/accidents">
            Accident Menu
          </Link>
        </header>
        <Suspense fallback={<GarageFallback />}>
          <GarageContent searchParams={searchParams} />
        </Suspense>
      </section>
    </main>
  );
}
