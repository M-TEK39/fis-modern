import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  AccidentApiError,
  getHqAccidentPage,
  type GarageSearchType,
} from "@/lib/api/fleet-operations/api-accidents";
import { getSession } from "@/lib/auth/session";

const ACCIDENTS_ROLE = "Accidents";

export type HqPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  locationCode?: number;
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

function buildHqHref(
  searchType: GarageSearchType,
  searchTerm: string,
  page: number,
  locationCode?: number,
) {
  const params = new URLSearchParams({ type: searchType, page: String(page) });
  if (searchTerm) params.set("q", searchTerm);
  const path = locationCode === 2 ? "/Accident/MNT_accidentp_getreg.aspx" : "/accidents/hq";
  return `${path}?${params.toString()}`;
}

function buildHqAddHref(searchType: GarageSearchType, searchTerm: string, locationCode?: number) {
  const params = new URLSearchParams({ type: searchType });
  if (searchTerm) params.set("q", searchTerm);
  if (locationCode === 2) {
    params.set("Action", "ADD");
    return `/Accident/MNT_accidentp_getdetail.aspx?${params.toString()}`;
  }

  return `/accidents/hq/add?${params.toString()}`;
}

function LoadingState() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Loading HQ accidents...</p>
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
      <h2>HQ accident records could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/accidents/hq">
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
      <h2>You do not have permission to maintain HQ accidents.</h2>
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
        {searchTerm ? `No accidents matched “${searchTerm}”.` : "No HQ accidents are available."}
      </h2>
      <p className="muted-copy">Try another GG or GP number, or add a new accident record.</p>
    </div>
  );
}

async function HqContent({ searchParams, locationCode }: HqPageProps) {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <SessionRecovery returnPath="/accidents/hq" />;
  if (session.status === "unavailable") return <ApiUnavailable />;
  if (!hasRole(session.roles, ACCIDENTS_ROLE)) return <AccessRestricted />;

  const query = await searchParams;
  const searchType = getSearchType(getQueryValue(query.type) ?? getQueryValue(query.Radio1));
  const searchTerm = (getQueryValue(query.q) ?? getQueryValue(query.txtGGNum) ?? "").trim();
  const pageNumber = getPage(getQueryValue(query.page));
  const created = getQueryValue(query.created) === "1";
  const updated = getQueryValue(query.updated) === "1";

  let pageData;
  try {
    pageData = await getHqAccidentPage(pageNumber, searchType, searchTerm, 24, locationCode);
  } catch (error) {
    if (error instanceof AccidentApiError && error.reason === "unauthorized") {
      return <SessionRecovery returnPath="/accidents/hq" />;
    }
    console.error(
      "FIS HQ accident request failed",
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
          <label className="sr-only" htmlFor="hq-search">
            {searchType === "GG" ? "GG Number" : "GP Number"}
          </label>
          <input
            className="vehicle-search"
            id="hq-search"
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
              <caption className="sr-only">HQ accident records</caption>
              <thead>
                <tr>
                  <th scope="col">Vehicle Number</th>
                  <th scope="col">Hire Type</th>
                  <th scope="col">Accident Date</th>
                  <th scope="col">Reference</th>
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
                        href={`/accidents/hq/edit?accidentId=${encodeURIComponent(row.accidentCode)}`}
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
            <nav className="vehicle-pagination" aria-label="HQ accident pages">
              {pageData.page > 1 ? (
                <Link
                  className="vehicle-pagination-button"
                  href={buildHqHref(searchType, searchTerm, pageData.page - 1, locationCode)}
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
                  href={buildHqHref(searchType, searchTerm, pageData.page + 1, locationCode)}
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
        <Link
          className="button button-primary"
          href={buildHqAddHref(searchType, searchTerm, locationCode)}
        >
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

export default async function HqAccidentPage({ searchParams, locationCode }: HqPageProps) {
  await connection();
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="hq-accidents-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Accident maintenance</p>
            <h1 id="hq-accidents-title">Accident Maintenance (HQ)</h1>
            <p>Search by GG or GP number, then edit or add HQ accidents.</p>
          </div>
          <Link className="button button-secondary" href="/accidents">
            Accident Menu
          </Link>
        </header>
        <Suspense fallback={<LoadingState />}>
          <HqContent searchParams={searchParams} locationCode={locationCode} />
        </Suspense>
      </section>
    </main>
  );
}
