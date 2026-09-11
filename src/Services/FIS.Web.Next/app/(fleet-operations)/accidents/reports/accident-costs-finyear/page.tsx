import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  AccidentApiError,
  getAccidentCostsFinancialYearReport,
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

function formatAmount(value: number) {
  return value.toFixed(2);
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
      <h2>The accident costs report could not be loaded.</h2>
      <p className="muted-copy">Retry when the FIS API is available.</p>
      <Link className="button button-primary" href="/accidents/reports/accident-costs-finyear">
        Try again
      </Link>
    </section>
  );
}

function AccidentCostsReportTable({
  rows,
  financialYear,
}: {
  rows: AccidentVehicleReportRow[];
  financialYear: string;
}) {
  const totalRepairCost = rows.reduce((total, row) => total + (row.costOfRepair ?? 0), 0);

  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="accident-costs-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="accident-costs-results-title">Accident Costs for {financialYear}</h2>
        </div>
        <span className="form-hint">{rows.length} record(s)</span>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">
            Accident costs report for financial year {financialYear}
          </caption>
          <thead>
            <tr>
              <th scope="col">Prov Reg Number</th>
              <th scope="col">GG Number</th>
              <th scope="col">Fin Year</th>
              <th scope="col">Accid Date</th>
              <th scope="col">Date Reported</th>
              <th scope="col">Dept/Site</th>
              <th scope="col">HQ Ref</th>
              <th scope="col">GG Ref</th>
              <th scope="col">GG cost of Damages</th>
              <th scope="col">Value Written Off</th>
              <th scope="col">Third Party Claim ?</th>
              <th scope="col">Third Party Damages</th>
              <th scope="col">Third Party Claim Amount</th>
              <th scope="col">File Close Date</th>
              <th scope="col">Notes</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row, index) => (
              <tr key={`${row.accidentCode}-${index}`}>
                <td>{valueOrDash(row.registrationNumber)}</td>
                <td>{valueOrDash(row.fleetNumber)}</td>
                <td>{valueOrDash(row.financialYear)}</td>
                <td>{formatDate(row.accidentDate)}</td>
                <td>{formatDate(row.reportedDate)}</td>
                <td>{valueOrDash(row.siteDescription)}</td>
                <td>{valueOrDash(row.hqReference)}</td>
                <td>{valueOrDash(row.ggReference)}</td>
                <td>{valueOrDash(row.costOfRepair)}</td>
                <td>{valueOrDash(row.writeOffAmount)}</td>
                <td>{valueOrDash(row.insuranceClaim)}</td>
                <td>{valueOrDash(row.thirdPartyClaim)}</td>
                <td>{valueOrDash(row.claimAgainstDepartment)}</td>
                <td>{formatDate(row.fileCloseDate)}</td>
                <td>{valueOrDash(row.notes)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="vehicle-pagination-meta">
        <span>Total Number: {rows.length}</span>
        <span>Total GG Cost of damaged: R {formatAmount(totalRepairCost)}</span>
      </div>
    </section>
  );
}

async function AccidentCostsReportContent({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return <SessionRecovery returnPath="/accidents/reports/accident-costs-finyear" />;
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
  const rawFinancialYear = getQueryValue(query, "financialYear", "FINY") ?? "";
  const financialYear = rawFinancialYear.trim();
  const shouldRun =
    getQueryValue(query, "run") === "1" ||
    ["financialYear", "FINY"].some((key) => getQueryValue(query, key) !== undefined);
  const errorMessage =
    shouldRun && (financialYear.length === 0 || financialYear.length > 5)
      ? "Enter a financial year between 1 and 5 characters, for example 04/05."
      : null;

  let rows: AccidentVehicleReportRow[] | null = null;
  if (shouldRun && !errorMessage) {
    try {
      rows = await getAccidentCostsFinancialYearReport(financialYear);
    } catch (error) {
      if (error instanceof AccidentApiError && error.reason === "unauthorized")
        return <SessionRecovery returnPath="/accidents/reports/accident-costs-finyear" />;
      console.error(
        "FIS accident costs financial year report failed",
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
        <div className="form-grid">
          <div className="field">
            <label htmlFor="accident-costs-finyear-year">Financial Year</label>
            <input
              id="accident-costs-finyear-year"
              name="financialYear"
              maxLength={5}
              placeholder="04/05"
              defaultValue={rawFinancialYear}
              required
            />
          </div>
        </div>
        <p className="form-hint">
          Enter the legacy five-character financial-year code, for example 04/05.
        </p>
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
          <AccidentCostsReportTable rows={rows} financialYear={financialYear} />
        ) : (
          <section className="vehicle-empty-state" aria-live="polite">
            <p className="eyebrow">No accidents found</p>
            <h2>No accident costs matched the selected financial year.</h2>
            <p className="muted-copy">Update the financial year and submit again.</p>
          </section>
        )
      ) : null}
      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/accidents">
          Accident Menu
        </Link>
        <Link className="button button-secondary" href="/accidents/reports/accident-costs-finyear">
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

export default function AccidentCostsFinancialYearReportPage({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="accident-costs-finyear-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Accident reports</p>
            <h1 id="accident-costs-finyear-title">Report on Accident Costs for a Financial Year</h1>
            <p>
              Review damage, claims, written-off values, and closure information for a financial
              year.
            </p>
          </div>
          <Link className="button button-secondary" href="/accidents/reports">
            Report Menu
          </Link>
        </header>
        <Suspense fallback={<LoadingState />}>
          <AccidentCostsReportContent searchParams={searchParams} />
        </Suspense>
      </section>
    </main>
  );
}
