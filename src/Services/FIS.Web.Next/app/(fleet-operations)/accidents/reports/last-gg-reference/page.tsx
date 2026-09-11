import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import PrintButton from "@/app/(fleet-operations)/accidents/reports/print-button";
import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  AccidentApiError,
  getAccidentLastGgReferenceReport,
  type AccidentLastGgReferenceRow,
} from "@/lib/api/fleet-operations/api-accidents";
import { getSession } from "@/lib/auth/session";

const ACCIDENTS_ROLE = "Accidents";

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function valueOrDash(value: string | null) {
  return value?.trim() || "-";
}

function LoadingState() {
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
      <h2>The GG reference report could not be loaded.</h2>
      <p className="muted-copy">Retry when the FIS API is available.</p>
      <Link className="button button-primary" href="/accidents/reports/last-gg-reference">
        Try again
      </Link>
    </section>
  );
}

function ReferenceTable({ rows }: { rows: AccidentLastGgReferenceRow[] }) {
  if (rows.length === 0) {
    return (
      <section className="vehicle-empty-state" aria-live="polite">
        <p className="eyebrow">No references found</p>
        <h2>No GG reference numbers matched the legacy report threshold.</h2>
      </section>
    );
  }

  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="last-gg-reference-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="last-gg-reference-results-title">Last GG Reference Numbers Used</h2>
        </div>
        <span className="form-hint">{rows.length} record(s)</span>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">Last GG reference numbers used</caption>
          <DataTableHeader
            columns={[
              { key: "column-1", label: <>GG Reference Number</> },
              { key: "column-2", label: <>Prov Reg Number</> },
              { key: "column-3", label: <>GG Number</> },
              { key: "column-4", label: <>Teller</> },
            ]}
          />
          <tbody>
            {rows.map((row) => (
              <tr key={row.accidentCode}>
                <td>{valueOrDash(row.ggReference)}</td>
                <td>{valueOrDash(row.registrationNumber)}</td>
                <td>{valueOrDash(row.fleetNumber)}</td>
                <td>{row.accidentCode}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

async function LastGgReferenceContent() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return <SessionRecovery returnPath="/accidents/reports/last-gg-reference" />;
  if (session.status === "unavailable") return <ErrorState />;
  if (!hasRole(session.roles, ACCIDENTS_ROLE)) {
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Access restricted</p>
        <h2>You do not have permission to run accident reports.</h2>
      </section>
    );
  }

  try {
    return <ReferenceTable rows={await getAccidentLastGgReferenceReport()} />;
  } catch (error) {
    if (error instanceof AccidentApiError && error.reason === "unauthorized")
      return <SessionRecovery returnPath="/accidents/reports/last-gg-reference" />;
    console.error(
      "FIS last GG reference report failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return <ErrorState />;
  }
}

async function PrintedAt() {
  await connection();
  const printedAt = new Date();
  return <time dateTime={printedAt.toISOString()}>{printedAt.toLocaleString("en-ZA")}</time>;
}

export default function LastGgReferencePage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <article className="vehicle-card" aria-labelledby="last-gg-reference-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Accident reports</p>
            <h1 id="last-gg-reference-title">LAST GG Reference Numbers Used</h1>
            <p>
              Date Printed:{" "}
              <Suspense fallback={<span aria-hidden="true">—</span>}>
                <PrintedAt />
              </Suspense>
            </p>
          </div>
          <div className="button-row">
            <Link className="button button-secondary" href="/accidents/reports">
              Report Menu
            </Link>
            <PrintButton label="Print reference list" />
          </div>
        </header>
        <Suspense fallback={<LoadingState />}>
          <LastGgReferenceContent />
        </Suspense>
        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href="/accidents">
            Accident Menu
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
    </main>
  );
}
