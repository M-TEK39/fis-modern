import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  AccidentApiError,
  getAccidentNewAccidentsReport,
  type AccidentNewAccidentReportMode,
  type AccidentVehicleReportRow,
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

function getMode(query: ReportQuery): { mode: AccidentNewAccidentReportMode; invalid: boolean } {
  const rawMode = (getQueryValue(query, "mode", "Radio1") ?? "").trim().toLowerCase();
  switch (rawMode) {
    case "call":
    case "radiocal":
      return { mode: "call", invalid: false };
    case "garage":
    case "radionew":
      return { mode: "garage", invalid: false };
    case "confirm":
    case "radiocon":
      return { mode: "confirm", invalid: false };
    case "all":
    case "radioall":
    case "":
      return { mode: "all", invalid: false };
    default:
      return { mode: "all", invalid: true };
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

function reportTitle(mode: AccidentNewAccidentReportMode) {
  switch (mode) {
    case "call":
      return "New Accidents Reported by Call Centre";
    case "garage":
      return "New Accidents Reported to HQ by Garage";
    case "confirm":
      return "Confirmation of Receipt by HQ";
    default:
      return "Both New Accident Notifications";
  }
}

function LoadingState() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Loading new accident report...</p>
    </div>
  );
}

function ErrorState() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>The new accident report could not be loaded.</h2>
      <p className="muted-copy">Retry when the FIS API is available.</p>
      <Link className="button button-primary" href="/accidents/reports/new-accidents">
        Try again
      </Link>
    </section>
  );
}

function NewAccidentReportTable({
  rows,
  mode,
}: {
  rows: AccidentVehicleReportRow[];
  mode: AccidentNewAccidentReportMode;
}) {
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="new-accident-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="new-accident-results-title">{reportTitle(mode)}</h2>
        </div>
        <span className="form-hint">{rows.length} record(s)</span>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">{reportTitle(mode)}</caption>
          <thead>
            <tr>
              <th scope="col">Prov Reg Number</th>
              <th scope="col">GG Number</th>
              <th scope="col">Garage</th>
              <th scope="col">Accid Date</th>
              <th scope="col">Accid Time</th>
              <th scope="col">Accid Place</th>
              <th scope="col">Fin Year</th>
              <th scope="col">Date Updated</th>
              <th scope="col">Notify Garage</th>
              <th scope="col">Notify Garage Date</th>
              <th scope="col">Accident Description</th>
              <th scope="col">Trip Auth</th>
              <th scope="col">Driver Name</th>
              <th scope="col">ID Number</th>
              <th scope="col">Site</th>
              <th scope="col">Trans Officer</th>
              <th scope="col">TO Tel</th>
              <th scope="col">HQ Ref</th>
              <th scope="col">GG Ref</th>
              <th scope="col">Case Num</th>
              <th scope="col">GG Car Damage</th>
              <th scope="col">GG Car Damage</th>
              <th scope="col">Driver Fault</th>
              <th scope="col">Death</th>
              <th scope="col">Injured</th>
              <th scope="col">Private Party Regno</th>
              <th scope="col">Private Party</th>
              <th scope="col">Priv Car Damage</th>
              <th scope="col">File Close Date</th>
              <th scope="col">Notes</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row, index) => (
              <tr key={`${row.accidentCode}-${index}`}>
                <td>{valueOrDash(row.registrationNumber)}</td>
                <td>{valueOrDash(row.fleetNumber)}</td>
                <td>{valueOrDash(row.locationDescription)}</td>
                <td>{formatDate(row.accidentDate)}</td>
                <td>{formatTime(row.accidentTime)}</td>
                <td>{valueOrDash(row.accidentPlace)}</td>
                <td>{valueOrDash(row.financialYear)}</td>
                <td>{formatDate(row.dateUpdated)}</td>
                <td>{valueOrDash(row.notifiedGarage)}</td>
                <td>{formatDate(row.notifiedGarageDate)}</td>
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
                <td>{valueOrDash(row.driverFault)}</td>
                <td>{valueOrDash(row.death)}</td>
                <td>{valueOrDash(row.injured)}</td>
                <td>{valueOrDash(row.thirdPartyRegistration)}</td>
                <td>{valueOrDash(row.thirdPartyOwner)}</td>
                <td>{valueOrDash(row.thirdPartyClaim)}</td>
                <td>{formatDate(row.fileCloseDate)}</td>
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

async function NewAccidentsReportContent({ searchParams }: { searchParams: Promise<ReportQuery> }) {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return <SessionRecovery returnPath="/accidents/reports/new-accidents" />;
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
    getQueryValue(query, "run") === "1" || getQueryValue(query, "mode", "Radio1") !== undefined;
  const errorMessage = invalid ? "Mode must be all, call centre, garage, or confirmation." : null;
  let rows: AccidentVehicleReportRow[] | null = null;
  if (shouldRun && !errorMessage) {
    try {
      rows = await getAccidentNewAccidentsReport(mode);
    } catch (error) {
      if (error instanceof AccidentApiError && error.reason === "unauthorized")
        return <SessionRecovery returnPath="/accidents/reports/new-accidents" />;
      console.error(
        "FIS new accident report failed",
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
          <legend>Report on</legend>
          <label className="vehicle-checkbox-label">
            <input name="mode" type="radio" value="call" defaultChecked={mode === "call"} /> New
            accidents reported by Call Centre
          </label>
          <label className="vehicle-checkbox-label">
            <input name="mode" type="radio" value="garage" defaultChecked={mode === "garage"} /> New
            accidents reported to HQ by Garage
          </label>
          <label className="vehicle-checkbox-label">
            <input name="mode" type="radio" value="confirm" defaultChecked={mode === "confirm"} />{" "}
            Confirmation of receipt by HQ
          </label>
          <label className="vehicle-checkbox-label">
            <input name="mode" type="radio" value="all" defaultChecked={mode === "all"} /> Both
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
          <NewAccidentReportTable rows={rows} mode={mode} />
        ) : (
          <section className="vehicle-empty-state" aria-live="polite">
            <p className="eyebrow">No accidents found</p>
            <h2>No accidents matched this notification status.</h2>
            <p className="muted-copy">Choose another report type and submit again.</p>
          </section>
        )
      ) : null}
      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/accidents">
          Accident Menu
        </Link>
        <Link className="button button-secondary" href="/accidents/reports/new-accidents">
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

export default async function NewAccidentsReportPage({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  await connection();
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="new-accident-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Accident reports</p>
            <h1 id="new-accident-title">Report on New Accidents</h1>
            <p>Review accident notifications by their legacy HQ status flag.</p>
          </div>
          <Link className="button button-secondary" href="/accidents/reports">
            Report Menu
          </Link>
        </header>
        <Suspense fallback={<LoadingState />}>
          <NewAccidentsReportContent searchParams={searchParams} />
        </Suspense>
      </section>
    </main>
  );
}
