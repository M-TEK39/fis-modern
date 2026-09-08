import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/actions/auth";
import SessionRecovery from "@/app/home/session-recovery";
import {
  AccidentApiError,
  getAccidentDuplicateReport,
  type AccidentGarageReportMode,
  type AccidentVehicleReportRow,
} from "@/lib/api-accidents";
import { getSession } from "@/lib/session";

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

function getMode(query: ReportQuery): { mode: AccidentGarageReportMode; invalid: boolean } {
  const rawMode = (getQueryValue(query, "garage", "mode", "Radio1") ?? "jhb").trim().toLowerCase();
  switch (rawMode) {
    case "jhb":
    case "radiojhb":
      return { mode: "jhb", invalid: false };
    case "pta":
    case "radiopta":
      return { mode: "pta", invalid: false };
    case "all":
    case "radioall":
      return { mode: "all", invalid: false };
    default:
      return { mode: "jhb", invalid: true };
  }
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function valueOrDash(value: string | number | null) {
  return value === null || value === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) ?? "-";
}

function formatTime(value: string | null) {
  if (!value) {
    return "-";
  }

  const timeStart = value.indexOf("T");
  return timeStart >= 0 ? value.slice(timeStart + 1, timeStart + 6) : value.slice(0, 5);
}

function reportTitle(mode: AccidentGarageReportMode) {
  switch (mode) {
    case "jhb":
      return "Duplicate Accidents - JHB";
    case "pta":
      return "Duplicate Accidents - PTA";
    default:
      return "Duplicate Accidents - ALL";
  }
}

function LoadingState() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Loading duplicate accident report...</p>
    </div>
  );
}

function ErrorState() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>The duplicate accident report could not be loaded.</h2>
      <p className="muted-copy">Retry when the FIS API is available.</p>
      <Link className="button button-primary" href="/accidents/reports/duplicate-accidents">
        Try again
      </Link>
    </section>
  );
}

function DuplicateAccidentsReportTable({
  rows,
  mode,
}: {
  rows: AccidentVehicleReportRow[];
  mode: AccidentGarageReportMode;
}) {
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="duplicate-accident-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="duplicate-accident-results-title">{reportTitle(mode)}</h2>
        </div>
        <span className="form-hint">{rows.length} record(s)</span>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">{reportTitle(mode)}</caption>
          <thead>
            <tr>
              <th scope="col">GG Number</th>
              <th scope="col">Prov Reg Number</th>
              <th scope="col">Garage</th>
              <th scope="col">Accident Date</th>
              <th scope="col">Accident Time</th>
              <th scope="col">Accident Place</th>
              <th scope="col">Fin year</th>
              <th scope="col">Description of Accident</th>
              <th scope="col">Trip Authority</th>
              <th scope="col">Driver Name</th>
              <th scope="col">Driver ID Number</th>
              <th scope="col">Driver Site</th>
              <th scope="col">Transport Officer</th>
              <th scope="col">Transport Officer Tel</th>
              <th scope="col">HQ Reference</th>
              <th scope="col">GG Reference</th>
              <th scope="col">Case Number</th>
              <th scope="col">GG Car Damage Amount</th>
              <th scope="col">GG Car Damage Desc</th>
              <th scope="col">Death</th>
              <th scope="col">Injured</th>
              <th scope="col">Private Party Regno</th>
              <th scope="col">Third Party Owner</th>
              <th scope="col">Private Car Damage</th>
              <th scope="col">Claim Against Dept</th>
              <th scope="col">Notes</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row, index) => (
              <tr key={`${row.accidentCode}-${index}`}>
                <td>{valueOrDash(row.fleetNumber)}</td>
                <td>{valueOrDash(row.registrationNumber)}</td>
                <td>{valueOrDash(row.locationDescription)}</td>
                <td>{formatDate(row.accidentDate)}</td>
                <td>{formatTime(row.accidentTime)}</td>
                <td>{valueOrDash(row.accidentPlace)}</td>
                <td>{valueOrDash(row.financialYear)}</td>
                <td>{valueOrDash(row.description)}</td>
                <td>{valueOrDash(row.tripAuthority)}</td>
                <td>{valueOrDash(row.driverName)}</td>
                <td>{valueOrDash(row.driverEmployNumber)}</td>
                <td>{valueOrDash(row.departmentNumber)}</td>
                <td>{valueOrDash(row.transportOfficerName)}</td>
                <td>{valueOrDash(row.transportOfficerTelephone)}</td>
                <td>{valueOrDash(row.hqReference)}</td>
                <td>{valueOrDash(row.ggReference)}</td>
                <td>{valueOrDash(row.caseNumber)}</td>
                <td>{valueOrDash(row.costOfRepair)}</td>
                <td>{valueOrDash(row.damageDescription)}</td>
                <td>{valueOrDash(row.death)}</td>
                <td>{valueOrDash(row.injured)}</td>
                <td>{valueOrDash(row.thirdPartyRegistration)}</td>
                <td>{valueOrDash(row.thirdPartyOwner)}</td>
                <td>{valueOrDash(row.thirdPartyClaim)}</td>
                <td>{valueOrDash(row.claimAgainstDepartment)}</td>
                <td>{valueOrDash(row.notes)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="vehicle-pagination-meta">Total Number: {rows.length}</p>
    </section>
  );
}

async function DuplicateAccidentsReportContent({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return <SessionRecovery returnPath="/accidents/reports/duplicate-accidents" />;
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
  const { mode, invalid } = getMode(query);
  const shouldRun =
    getQueryValue(query, "run") === "1" ||
    getQueryValue(query, "garage", "mode", "Radio1") !== undefined;
  const errorMessage = invalid ? "Choose a valid garage for the duplicate report." : null;
  let rows: AccidentVehicleReportRow[] | null = null;
  if (shouldRun && !errorMessage) {
    try {
      rows = await getAccidentDuplicateReport(mode);
    } catch (error) {
      if (error instanceof AccidentApiError && error.reason === "unauthorized")
        return <SessionRecovery returnPath="/accidents/reports/duplicate-accidents" />;
      console.error(
        "FIS duplicate accident report failed",
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
          <legend>Garage</legend>
          <label className="vehicle-checkbox-label">
            <input name="garage" type="radio" value="jhb" defaultChecked={mode === "jhb"} /> JHB
          </label>
          <label className="vehicle-checkbox-label">
            <input name="garage" type="radio" value="pta" defaultChecked={mode === "pta"} /> PTA
          </label>
          <label className="vehicle-checkbox-label">
            <input name="garage" type="radio" value="all" defaultChecked={mode === "all"} /> ALL
          </label>
        </fieldset>
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
      {rows ? (
        rows.length > 0 ? (
          <DuplicateAccidentsReportTable rows={rows} mode={mode} />
        ) : (
          <section className="vehicle-empty-state" aria-live="polite">
            <p className="eyebrow">No vehicles found</p>
            <h2>No duplicate accidents matched this garage selection.</h2>
            <p className="muted-copy">Choose another garage and submit again.</p>
          </section>
        )
      ) : null}
      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/accidents">
          Accident Menu
        </Link>
        <Link className="button button-secondary" href="/accidents/reports/duplicate-accidents">
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

export default async function DuplicateAccidentsReportPage({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  await connection();
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="duplicate-accident-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Accident reports</p>
            <h1 id="duplicate-accident-title">REPORT ON ALL Duplicate ACCIDENT'S</h1>
            <p>Review duplicate accident records for JHB, PTA, or all garages.</p>
          </div>
          <Link className="button button-secondary" href="/accidents/reports">
            Report Menu
          </Link>
        </header>
        <Suspense fallback={<LoadingState />}>
          <DuplicateAccidentsReportContent searchParams={searchParams} />
        </Suspense>
      </section>
    </main>
  );
}
