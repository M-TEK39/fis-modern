import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  AccidentApiError,
  getAccidentDepartmentFinancialYearReport,
  type AccidentDepartmentFinancialYearGarageMode,
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

function getGarageMode(query: ReportQuery): {
  mode: AccidentDepartmentFinancialYearGarageMode;
  invalid: boolean;
} {
  const rawMode = (getQueryValue(query, "garage", "Radio1") ?? "").trim().toLowerCase();
  switch (rawMode) {
    case "jhb":
    case "radiojhb":
      return { mode: "jhb", invalid: false };
    case "pta":
    case "radiopta":
      return { mode: "pta", invalid: false };
    case "all":
    case "radioall":
    case "":
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

function formatAmount(value: number) {
  return value.toFixed(2);
}

function LoadingState() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Loading department financial year report...</p>
    </div>
  );
}

function ErrorState() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>The department financial year report could not be loaded.</h2>
      <p className="muted-copy">Retry when the FIS API is available.</p>
      <Link className="button button-primary" href="/accidents/reports/department-finyear">
        Try again
      </Link>
    </section>
  );
}

function DepartmentFinancialYearReportTable({
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
      aria-labelledby="department-finyear-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="department-finyear-results-title">
            Department/Site Accident Report for {financialYear}
          </h2>
        </div>
        <span className="form-hint">{rows.length} record(s)</span>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">
            Department or site accident report for book or financial year {financialYear}
          </caption>
          <thead>
            <tr>
              <th scope="col">Prov Reg Number</th>
              <th scope="col">GG Number</th>
              <th scope="col">Garage</th>
              <th scope="col">Fin Year</th>
              <th scope="col">Accid Date</th>
              <th scope="col">Accid Place</th>
              <th scope="col">Accident Category</th>
              <th scope="col">Trip Auth</th>
              <th scope="col">Driver Name</th>
              <th scope="col">Dept/Site Code</th>
              <th scope="col">Dept/Site</th>
              <th scope="col">Trans Officer</th>
              <th scope="col">TO Tel</th>
              <th scope="col">HQ Ref</th>
              <th scope="col">GG Ref</th>
              <th scope="col">Case Num</th>
              <th scope="col">GG Car Damage</th>
              <th scope="col">Priv Car Damage</th>
              <th scope="col">Cost Claim Agains Dept</th>
              <th scope="col">Notes</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row, index) => (
              <tr key={`${row.accidentCode}-${index}`}>
                <td>{valueOrDash(row.registrationNumber)}</td>
                <td>{valueOrDash(row.fleetNumber)}</td>
                <td>{valueOrDash(row.locationDescription)}</td>
                <td>{valueOrDash(row.financialYear)}</td>
                <td>{formatDate(row.accidentDate)}</td>
                <td>{valueOrDash(row.accidentPlace)}</td>
                <td>{valueOrDash(row.accidentTypeDescription)}</td>
                <td>{valueOrDash(row.tripAuthority)}</td>
                <td>{valueOrDash(row.driverName)}</td>
                <td>{valueOrDash(row.departmentNumber)}</td>
                <td>{valueOrDash(row.siteDescription)}</td>
                <td>{valueOrDash(row.transportOfficerName)}</td>
                <td>{valueOrDash(row.transportOfficerTelephone)}</td>
                <td>{valueOrDash(row.hqReference)}</td>
                <td>{valueOrDash(row.ggReference)}</td>
                <td>{valueOrDash(row.caseNumber)}</td>
                <td>{valueOrDash(row.costOfRepair)}</td>
                <td>{valueOrDash(row.thirdPartyClaim)}</td>
                <td>{valueOrDash(row.claimAgainstDepartment)}</td>
                <td>{valueOrDash(row.notes)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="vehicle-pagination-meta">
        <span>Total Number: {rows.length}</span>
        <span>Total Cost of Repairs: R {formatAmount(totalRepairCost)}</span>
      </div>
    </section>
  );
}

async function DepartmentFinancialYearReportContent({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return <SessionRecovery returnPath="/accidents/reports/department-finyear" />;
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
  const departmentNumber = (getQueryValue(query, "departmentNumber", "XDEPT") ?? "").trim();
  const rawFinancialYear = getQueryValue(query, "financialYear", "FINY") ?? "";
  const financialYear = rawFinancialYear.trim();
  const { mode: garage, invalid: invalidGarage } = getGarageMode(query);
  const shouldRun =
    getQueryValue(query, "run") === "1" ||
    ["departmentNumber", "XDEPT", "financialYear", "FINY", "garage", "Radio1"].some(
      (key) => getQueryValue(query, key) !== undefined,
    );
  const errorMessage = invalidGarage
    ? "Choose a valid garage."
    : shouldRun && (financialYear.length === 0 || financialYear.length > 5)
      ? "Enter a book / financial year between 1 and 5 characters, for example 02/03."
      : null;

  let rows: AccidentVehicleReportRow[] | null = null;
  if (shouldRun && !errorMessage) {
    try {
      rows = await getAccidentDepartmentFinancialYearReport(
        departmentNumber,
        garage,
        financialYear,
      );
    } catch (error) {
      if (error instanceof AccidentApiError && error.reason === "unauthorized")
        return <SessionRecovery returnPath="/accidents/reports/department-finyear" />;
      console.error(
        "FIS accident department financial year report failed",
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
            <input name="garage" type="radio" value="jhb" defaultChecked={garage === "jhb"} /> JHB
          </label>
          <label className="vehicle-checkbox-label">
            <input name="garage" type="radio" value="pta" defaultChecked={garage === "pta"} /> PTA
          </label>
          <label className="vehicle-checkbox-label">
            <input name="garage" type="radio" value="all" defaultChecked={garage === "all"} /> ALL
          </label>
        </fieldset>
        <div className="form-grid">
          <div className="field">
            <label htmlFor="accident-department-finyear-year">Book / Financial Year</label>
            <input
              id="accident-department-finyear-year"
              name="financialYear"
              maxLength={5}
              placeholder="02/03"
              defaultValue={rawFinancialYear}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="accident-department-finyear-department">Dept/Site</label>
            <input
              id="accident-department-finyear-department"
              name="departmentNumber"
              maxLength={7}
              defaultValue={departmentNumber}
            />
            <span className="form-hint">
              Type the first 5 characters of the department or site code.
            </span>
          </div>
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
      {rows ? (
        rows.length > 0 ? (
          <DepartmentFinancialYearReportTable rows={rows} financialYear={financialYear} />
        ) : (
          <section className="vehicle-empty-state" aria-live="polite">
            <p className="eyebrow">No accidents found</p>
            <h2>No accidents matched the selected filters.</h2>
            <p className="muted-copy">
              Update the garage, book / financial year, or department/site and submit again.
            </p>
          </section>
        )
      ) : null}
      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/accidents">
          Accident Menu
        </Link>
        <Link className="button button-secondary" href="/accidents/reports/department-finyear">
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

export default async function DepartmentFinancialYearReportPage({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  await connection();
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="accident-department-finyear-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Accident reports</p>
            <h1 id="accident-department-finyear-title">
              Accident Report for a Department/Site, for a Book / Financial Year
            </h1>
            <p>Review accident records by garage, book / financial year, and department or site.</p>
          </div>
          <Link className="button button-secondary" href="/accidents/reports">
            Report Menu
          </Link>
        </header>
        <Suspense fallback={<LoadingState />}>
          <DepartmentFinancialYearReportContent searchParams={searchParams} />
        </Suspense>
      </section>
    </main>
  );
}
