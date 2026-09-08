import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import PrintButton from "@/app/accidents/reports/print-button";
import { logoutAction } from "@/app/actions/auth";
import SessionRecovery from "@/app/home/session-recovery";
import {
  AccidentApiError,
  getAccidentOutstandingDocumentReport,
  type AccidentOutstandingDocumentReport,
} from "@/lib/api-accidents";
import { getSession } from "@/lib/session";

const ACCIDENTS_ROLE = "Accidents";
type QueryValue = string | string[] | undefined;

function getQueryValue(query: Record<string, QueryValue>, ...keys: string[]) {
  for (const key of keys) {
    const value = query[key];
    if (value !== undefined) {
      return Array.isArray(value) ? value[0] : value;
    }
  }

  return undefined;
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

function OutstandingDocuments({ report }: { report: AccidentOutstandingDocumentReport }) {
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="outstanding-list-title">
      <h2 id="outstanding-list-title">The following documents are still outstanding:</h2>
      {report.documentStatusTrackingAvailable ? null : (
        <div className="notice notice-warning" role="status">
          Some legacy document-status fields are not available in this database. The letter includes
          the statuses that could be read.
        </div>
      )}
      {report.outstandingDocuments.length > 0 ? (
        <ul>
          {report.outstandingDocuments.map((document) => (
            <li key={document}>{document}</li>
          ))}
        </ul>
      ) : (
        <p className="muted-copy">No outstanding documents were recorded.</p>
      )}
    </section>
  );
}

function Letter({ report }: { report: AccidentOutstandingDocumentReport }) {
  const reference = `${valueOrDash(report.fleetNumber)}-${valueOrDash(report.ggReference)}`;
  return (
    <article className="vehicle-card accident-letter" aria-labelledby="outstanding-letter-title">
      <header className="vehicle-page-header">
        <div>
          <p className="eyebrow">Accident report letter</p>
          <h1 id="outstanding-letter-title">Outstanding Accident Documents Letter</h1>
        </div>
        <div className="button-row">
          <Link className="button button-secondary" href="/accidents/reports/outstanding-docs">
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
        <p>
          <strong>CONTACT PERSON:</strong> {valueOrDash(report.responsiblePerson)}{" "}
          <strong>PHONE:</strong> {valueOrDash(report.telephone)} <strong>FAX:</strong>{" "}
          {valueOrDash(report.fax)}
        </p>
        <dl className="contract-facts">
          <div>
            <dt>REG. NO</dt>
            <dd>{valueOrDash(report.registrationNumber)}</dd>
          </div>
          <div>
            <dt>Date Received</dt>
            <dd>{formatDate(report.reportedDate)}</dd>
          </div>
          <div>
            <dt>Damage</dt>
            <dd>{valueOrDash(report.damageDescription)}</dd>
          </div>
        </dl>
      </section>
      <section className="vehicle-status-maintenance-panel">
        <p>The above-mentioned incident refers.</p>
        <p>Please review and provide the outstanding documents listed below.</p>
      </section>
      <OutstandingDocuments report={report} />
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
        <Link className="button button-secondary" href="/accidents/reports/outstanding-docs">
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

export default async function OutstandingDocumentsLetterPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, QueryValue>>;
}) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/accidents/reports/outstanding-docs/letter" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>The outstanding document letter could not be loaded.</h2>
          <Link className="button button-primary" href="/accidents/reports/outstanding-docs">
            Try again
          </Link>
        </section>
      </main>
    );
  if (!hasRole(session.roles, ACCIDENTS_ROLE))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to print accident letters.</h2>
        </section>
      </main>
    );

  const query = await searchParams;
  const accidentCode = positiveInteger(getQueryValue(query, "accidentCode", "Code", "code"));
  if (!accidentCode)
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Accident not selected</p>
          <h2>Choose an accident from the outstanding document lookup.</h2>
          <Link className="button button-secondary" href="/accidents/reports/outstanding-docs">
            Outstanding document lookup
          </Link>
        </section>
      </main>
    );

  try {
    const report = await getAccidentOutstandingDocumentReport(accidentCode);
    return (
      <main className="page-shell vehicle-page-shell">
        <Letter report={report} />
      </main>
    );
  } catch (error) {
    if (error instanceof AccidentApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery
            returnPath={`/accidents/reports/outstanding-docs/letter?accidentCode=${accidentCode}`}
          />
        </main>
      );
    const notFound = error instanceof AccidentApiError && error.reason === "not-found";
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">{notFound ? "Record not found" : "Report unavailable"}</p>
          <h2>
            {notFound
              ? `Accident #${accidentCode} could not be found.`
              : "The outstanding document letter could not be loaded."}
          </h2>
          <p className="muted-copy">Return to the lookup and choose another accident.</p>
          <Link className="button button-secondary" href="/accidents/reports/outstanding-docs">
            Outstanding document lookup
          </Link>
        </section>
      </main>
    );
  }
}
