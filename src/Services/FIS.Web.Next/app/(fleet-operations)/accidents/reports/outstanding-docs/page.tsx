import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  AccidentApiError,
  getAccidentOutstandingDocumentLookup,
  type AccidentOutstandingDocumentLookupRow,
  type AccidentVehicleReportMode,
} from "@/lib/api/fleet-operations/api-accidents";
import { getSession } from "@/lib/auth/session";

const ACCIDENTS_ROLE = "Accidents";
type QueryValue = string | string[] | undefined;
type ReportQuery = Record<string, QueryValue>;

function getQueryValue(query: ReportQuery, ...keys: string[]) {
  for (const key of keys) {
    const value = query[key];
    if (value !== undefined) {
      return Array.isArray(value) ? value[0] : value;
    }
  }

  return undefined;
}

function getMode(value: string | undefined): AccidentVehicleReportMode {
  const normalized = value?.trim().toLowerCase();
  return normalized === "fleet" || normalized === "gg" || normalized === "radiogg"
    ? "fleet"
    : "registration";
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function valueOrDash(value: string | null) {
  return value?.trim() || "-";
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function LoadingState() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Loading outstanding document lookup...</p>
    </div>
  );
}

function ErrorState() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>The outstanding document lookup could not be loaded.</h2>
      <p className="muted-copy">Retry when the FIS API is available.</p>
      <Link className="button button-primary" href="/accidents/reports/outstanding-docs">
        Try again
      </Link>
    </section>
  );
}

function LookupTable({
  rows,
  mode,
}: {
  rows: AccidentOutstandingDocumentLookupRow[];
  mode: AccidentVehicleReportMode;
}) {
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-live="polite"
      aria-labelledby="outstanding-documents-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="outstanding-documents-results-title">Accidents found</h2>
        </div>
        <span className="form-hint">{rows.length} record(s)</span>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">Accidents found for the selected vehicle number</caption>
          <thead>
            <tr>
              <th scope="col">Vehicle Number</th>
              <th scope="col">Refer Number (GMT Number)</th>
              <th scope="col">Accident Date</th>
              <th scope="col">Action</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.accidentCode}>
                <td>{valueOrDash(mode === "fleet" ? row.fleetNumber : row.registrationNumber)}</td>
                <td>{valueOrDash(row.ggReference)}</td>
                <td>{formatDate(row.accidentDate)}</td>
                <td>
                  <Link
                    className="button button-secondary button-small"
                    href={`/accidents/reports/outstanding-docs/letter?accidentCode=${encodeURIComponent(row.accidentCode)}`}
                  >
                    Report
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

async function OutstandingDocumentsContent({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return <SessionRecovery returnPath="/accidents/reports/outstanding-docs" />;
  if (session.status === "unavailable") return <ErrorState />;
  if (!hasRole(session.roles, ACCIDENTS_ROLE)) {
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Access restricted</p>
        <h2>You do not have permission to run accident reports.</h2>
      </section>
    );
  }

  const query = await searchParams;
  const rawMode = getQueryValue(query, "mode", "Radio1");
  const mode = getMode(rawMode);
  const searchTerm = (getQueryValue(query, "searchTerm", "xggnum") ?? "").trim();
  const shouldRun =
    getQueryValue(query, "run") === "1" ||
    searchTerm.length > 0 ||
    getQueryValue(query, "mode", "Radio1") !== undefined;
  const errorMessage =
    shouldRun && searchTerm.length === 0
      ? "Enter a GP or GG number."
      : searchTerm.length > 8
        ? "Vehicle numbers can contain no more than 8 characters."
        : null;

  let rows: AccidentOutstandingDocumentLookupRow[] | null = null;
  if (shouldRun && !errorMessage) {
    try {
      rows = await getAccidentOutstandingDocumentLookup(searchTerm, mode);
    } catch (error) {
      if (error instanceof AccidentApiError && error.reason === "unauthorized")
        return <SessionRecovery returnPath="/accidents/reports/outstanding-docs" />;
      console.error(
        "FIS outstanding accident document lookup failed",
        error instanceof Error ? error.message : "unknown error",
      );
      return <ErrorState />;
    }
  }

  return (
    <>
      {errorMessage ? (
        <div className="notice notice-error" role="alert">
          {errorMessage}
        </div>
      ) : null}
      <form className="vehicle-status-maintenance-panel" method="get">
        <fieldset className="vehicle-search-options">
          <legend>Find vehicle by</legend>
          <label className="vehicle-checkbox-label">
            <input
              type="radio"
              name="mode"
              value="registration"
              defaultChecked={mode === "registration"}
            />{" "}
            GP
          </label>
          <label className="vehicle-checkbox-label">
            <input type="radio" name="mode" value="fleet" defaultChecked={mode === "fleet"} /> GG
          </label>
        </fieldset>
        <div className="field">
          <label htmlFor="outstanding-documents-search">Number</label>
          <input
            id="outstanding-documents-search"
            name="searchTerm"
            maxLength={8}
            defaultValue={searchTerm}
            required
          />
        </div>
        <input name="run" type="hidden" value="1" />
        <div className="button-row">
          <button className="button button-primary" type="submit">
            Submit
          </button>
          <Link className="button button-secondary" href="/accidents/reports">
            Report Menu
          </Link>
        </div>
      </form>
      {rows !== null ? (
        rows.length > 0 ? (
          <LookupTable rows={rows} mode={mode} />
        ) : (
          <section className="vehicle-empty-state" aria-live="polite">
            <p className="eyebrow">Vehicle not found</p>
            <h2>This vehicle number does not exist.</h2>
            <p className="muted-copy">Try another GP or GG number.</p>
          </section>
        )
      ) : null}
      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/accidents">
          Accident Menu
        </Link>
        <Link className="button button-secondary" href="/accidents/reports/outstanding-docs">
          Clear
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

export default async function OutstandingDocumentsPage({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  await connection();
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="outstanding-documents-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Accident reports</p>
            <h1 id="outstanding-documents-title">LETTER for Outstanding Accident documents</h1>
            <p>Find an accident by GP or GG number and print the outstanding-document letter.</p>
          </div>
          <Link className="button button-secondary" href="/accidents/reports">
            Report Menu
          </Link>
        </header>
        <Suspense fallback={<LoadingState />}>
          <OutstandingDocumentsContent searchParams={searchParams} />
        </Suspense>
      </section>
    </main>
  );
}
