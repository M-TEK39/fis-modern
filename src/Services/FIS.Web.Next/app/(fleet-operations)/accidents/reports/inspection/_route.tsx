import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import PrintButton from "@/app/(fleet-operations)/accidents/reports/print-button";
import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  AccidentApiError,
  getAccidentInspectionLetterLookup,
  getAccidentInspectionLetterReport,
  type AccidentOutstandingDocumentLookupRow,
  type AccidentOutstandingDocumentReport,
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

function positiveInteger(value: string | undefined) {
  return value && /^\d+$/.test(value) && Number(value) > 0 ? Number(value) : null;
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

export function InspectionLoadingState() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Loading page…</p>
    </div>
  );
}

function ErrorState() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>The inspection letter lookup could not be loaded.</h2>
      <p className="muted-copy">Retry when the FIS API is available.</p>
      <Link className="button button-primary" href="/accidents/reports/inspection">
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
      aria-labelledby="inspection-letter-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="inspection-letter-results-title">Accidents found</h2>
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
                    href={`/accidents/reports/inspection/letter?accidentCode=${encodeURIComponent(row.accidentCode)}`}
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

async function InspectionLookupContent({ searchParams }: { searchParams: Promise<ReportQuery> }) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return <SessionRecovery returnPath="/accidents/reports/inspection" />;
  if (session.status === "unavailable") return <ErrorState />;
  if (!hasRole(session.roles, ACCIDENTS_ROLE))
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Access restricted</p>
        <h2>You do not have permission to run accident reports.</h2>
      </section>
    );

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
      rows = await getAccidentInspectionLetterLookup(searchTerm, mode);
    } catch (error) {
      if (error instanceof AccidentApiError && error.reason === "unauthorized")
        return <SessionRecovery returnPath="/accidents/reports/inspection" />;
      console.error(
        "FIS inspection letter lookup failed",
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
          <label htmlFor="inspection-letter-search">Number</label>
          <input
            id="inspection-letter-search"
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
        <Link className="button button-secondary" href="/accidents/reports/inspection">
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

function LetterAddress({ report }: { report: AccidentOutstandingDocumentReport }) {
  const lines = [report.address1, report.address2, report.postalCode].filter(
    (line): line is string => Boolean(line?.trim()),
  );
  return (
    <address>
      {lines.length > 0 ? (
        lines.map((line, index) => <div key={`${index}-${line}`}>{line}</div>)
      ) : (
        <div>-</div>
      )}
    </address>
  );
}

function InspectionLetter({ report }: { report: AccidentOutstandingDocumentReport }) {
  const fleetNumber = report.fleetNumber?.trim() || "";
  const ggReference = report.ggReference?.trim() || "";
  const reference = fleetNumber || ggReference ? `${fleetNumber}-${ggReference}` : "-";
  return (
    <article className="vehicle-card accident-letter" aria-labelledby="inspection-letter-title">
      <header className="vehicle-page-header">
        <div>
          <p className="eyebrow">Accident report letter</p>
          <h1 id="inspection-letter-title">INSPECTION LETTER</h1>
        </div>
        <div className="button-row">
          <Link className="button button-secondary" href="/accidents/reports/inspection">
            Back
          </Link>
          <PrintButton />
        </div>
      </header>
      <section className="vehicle-status-maintenance-panel accident-letter-header">
        <p className="accident-letter-government">
          <strong>GOVERNMENT GARAGE - STAATSGARAGE : JOHANNESBURG</strong>
        </p>
        <p>
          16 BOEINGSTR. EAST, BEDFORDVIEW, PRIVATE BAG X1 BEDFORDVIEW 2008, TEL : 3729048 / 67 / 00
        </p>
        <p>
          <strong>ENQUIRIES:</strong> M. Abbott <strong>Ref Number:</strong> {reference}
        </p>
      </section>
      <section className="vehicle-status-maintenance-panel">
        <h2>The Transport Manager</h2>
        <p>
          <strong>
            {valueOrDash(report.departmentNumber)} - {valueOrDash(report.siteDescription)}
          </strong>
        </p>
        <LetterAddress report={report} />
        <dl className="contract-facts">
          <div>
            <dt>CONTACT PERSON</dt>
            <dd>{valueOrDash(report.responsiblePerson)}</dd>
          </div>
          <div>
            <dt>PHONE / MOBILE NO</dt>
            <dd>{valueOrDash(report.telephone)}</dd>
          </div>
          <div>
            <dt>FAX NO</dt>
            <dd>{valueOrDash(report.fax)}</dd>
          </div>
          <div>
            <dt>REG. NO</dt>
            <dd>{valueOrDash(report.registrationNumber)}</dd>
          </div>
        </dl>
      </section>
      <section className="vehicle-status-maintenance-panel">
        <p>The above-mentioned incident refers.</p>
        <p>
          Kindly bring vehicle, <strong>{valueOrDash(report.registrationNumber)}</strong>, for
          inspection and if deemed necessary repair of the vehicle.
        </p>
        <p>
          Kindly take note that should the vehicle be involved in another accident and the damage
          cannot be separated, your Department will be held liable for the cost.
        </p>
        <p>This letter must accompany the vehicle if brought in for inspection.</p>
      </section>
      <section className="vehicle-status-maintenance-panel">
        <p>Please mention my reference number in all correspondence.</p>
        <p>
          All correspondence addressed to this office must be accompanied by a departmental
          letterhead.
        </p>
        <p>Thank you in advance.</p>
        <p>
          _______________________
          <br />
          for Deputy Manager
        </p>
      </section>
      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/accidents/reports/inspection">
          Report Menu
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
    </article>
  );
}

export async function InspectionLetterContent({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return <SessionRecovery returnPath="/accidents/reports/inspection/letter" />;
  if (session.status === "unavailable")
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">API unavailable</p>
        <h2>The inspection letter could not be loaded.</h2>
        <Link className="button button-primary" href="/accidents/reports/inspection">
          Try again
        </Link>
      </section>
    );
  if (!hasRole(session.roles, ACCIDENTS_ROLE))
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Access restricted</p>
        <h2>You do not have permission to print inspection letters.</h2>
      </section>
    );

  const query = await searchParams;
  const accidentCode = positiveInteger(getQueryValue(query, "accidentCode", "Code", "code"));
  if (!accidentCode)
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Accident not selected</p>
        <h2>Choose an accident from the inspection letter lookup.</h2>
        <Link className="button button-secondary" href="/accidents/reports/inspection">
          Inspection letter lookup
        </Link>
      </section>
    );

  try {
    const report = await getAccidentInspectionLetterReport(accidentCode);
    return <InspectionLetter report={report} />;
  } catch (error) {
    if (error instanceof AccidentApiError && error.reason === "unauthorized")
      return (
        <SessionRecovery
          returnPath={`/accidents/reports/inspection/letter?accidentCode=${accidentCode}`}
        />
      );
    const notFound = error instanceof AccidentApiError && error.reason === "not-found";
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">{notFound ? "Record not found" : "Report unavailable"}</p>
        <h2>
          {notFound
            ? `Accident #${accidentCode} could not be found.`
            : "The inspection letter could not be loaded."}
        </h2>
        <p className="muted-copy">Return to the lookup and choose another accident.</p>
        <Link className="button button-secondary" href="/accidents/reports/inspection">
          Inspection letter lookup
        </Link>
      </section>
    );
  }
}

export default function InspectionLetterLookupPage({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="inspection-letter-page-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Accident reports</p>
            <h1 id="inspection-letter-page-title">LETTER for Inspection</h1>
            <p>Find an accident by GP or GG number and print the inspection letter.</p>
          </div>
          <Link className="button button-secondary" href="/accidents/reports">
            Report Menu
          </Link>
        </header>
        <Suspense fallback={<InspectionLoadingState />}>
          <InspectionLookupContent searchParams={searchParams} />
        </Suspense>
      </section>
    </main>
  );
}
