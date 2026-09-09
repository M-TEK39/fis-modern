import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import { logoutAction } from "@/app/actions/auth";
import SessionRecovery from "@/app/home/session-recovery";
import { AccidentApiError, getHqAccidentPage, type GarageSearchType } from "@/lib/api-accidents";
import { getSession } from "@/lib/session";

const ACCIDENTS_ROLE = "Accidents";

type HqDeletePageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

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

function buildDeleteHref(searchType: GarageSearchType, searchTerm: string, accidentCode: number) {
  const params = new URLSearchParams({ accidentId: String(accidentCode), type: searchType });
  if (searchTerm) params.set("q", searchTerm);
  return `/accidents/hq/delete/confirm?${params.toString()}`;
}

function buildSearchHref(searchType: GarageSearchType, searchTerm: string, page: number) {
  const params = new URLSearchParams({ type: searchType, page: String(page) });
  if (searchTerm) params.set("q", searchTerm);
  return `/accidents/hq/delete?${params.toString()}`;
}

function ErrorState() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>HQ accident records could not be loaded.</h2>
      <p className="muted-copy">Retry when the FIS API is available.</p>
      <Link className="button button-primary" href="/accidents/hq/delete">
        Try again
      </Link>
    </section>
  );
}

async function HqDeleteContent({ searchParams }: HqDeletePageProps) {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <SessionRecovery returnPath="/accidents/hq/delete" />;
  if (session.status === "unavailable") return <ErrorState />;
  if (
    !session.roles.some(
      (role) => role.localeCompare(ACCIDENTS_ROLE, undefined, { sensitivity: "accent" }) === 0,
    )
  ) {
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Access restricted</p>
        <h2>You do not have permission to delete HQ accidents.</h2>
      </section>
    );
  }

  const query = await searchParams;
  const searchType = getSearchType(getQueryValue(query.type) ?? getQueryValue(query.Radio1));
  const searchTerm = (getQueryValue(query.q) ?? getQueryValue(query.txtGGNum) ?? "").trim();
  const pageNumber = getPage(getQueryValue(query.page));
  const deleted = getQueryValue(query.deleted) === "1";

  let pageData;
  try {
    pageData = await getHqAccidentPage(pageNumber, searchType, searchTerm, 24, 2);
  } catch (error) {
    if (error instanceof AccidentApiError && error.reason === "unauthorized")
      return <SessionRecovery returnPath="/accidents/hq/delete" />;
    console.error(
      "FIS HQ accident delete lookup failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return <ErrorState />;
  }

  return (
    <>
      {deleted ? (
        <div className="notice notice-success" role="status">
          Accident deleted successfully.
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
          <label className="sr-only" htmlFor="hq-delete-search">
            {searchType === "GG" ? "GG Number" : "GP Number"}
          </label>
          <input
            className="vehicle-search"
            id="hq-delete-search"
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
        <div className="vehicle-empty-state">
          <p className="eyebrow">No records found</p>
          <h2>
            {searchTerm
              ? `No accidents matched “${searchTerm}”.`
              : "No HQ accidents are available."}
          </h2>
          <p className="muted-copy">Try another GG or GP number.</p>
        </div>
      ) : (
        <>
          <div className="vehicle-table-wrapper" aria-live="polite">
            <table className="vehicle-table">
              <caption className="sr-only">HQ accident records available for deletion</caption>
              <thead>
                <tr>
                  <th scope="col">Reg Number</th>
                  <th scope="col">Hire Type</th>
                  <th scope="col">Accident Date</th>
                  <th scope="col">Action</th>
                </tr>
              </thead>
              <tbody>
                {pageData.rows.map((row) => (
                  <tr key={row.accidentCode}>
                    <td>{row.vehicleNumber ?? "-"}</td>
                    <td>{row.hireType ?? "-"}</td>
                    <td>{row.accidentDate?.slice(0, 10) ?? "-"}</td>
                    <td>
                      <Link
                        className="button button-danger button-small"
                        href={buildDeleteHref(searchType, searchTerm, row.accidentCode)}
                      >
                        DELETE
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {pageData.totalPages > 1 ? (
            <nav className="vehicle-pagination" aria-label="HQ accident delete pages">
              {pageData.page > 1 ? (
                <Link
                  className="vehicle-pagination-button"
                  href={buildSearchHref(searchType, searchTerm, pageData.page - 1)}
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
                  href={buildSearchHref(searchType, searchTerm, pageData.page + 1)}
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
        </>
      )}
      <div className="vehicle-footer-actions">
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

export default async function HqDeletePage({ searchParams }: HqDeletePageProps) {
  await connection();
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="hq-delete-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Accident maintenance</p>
            <h1 id="hq-delete-title">Delete an Accident (HQ)</h1>
            <p>Find an accident by GG/GP number, review it, then confirm deletion.</p>
          </div>
          <Link className="button button-secondary" href="/accidents">
            Accident Menu
          </Link>
        </header>
        <Suspense
          fallback={
            <div className="loading-card" aria-busy="true">
              <span className="spinner" aria-hidden="true" />
              <p>Loading HQ accidents...</p>
            </div>
          }
        >
          <HqDeleteContent searchParams={searchParams} />
        </Suspense>
      </section>
    </main>
  );
}
