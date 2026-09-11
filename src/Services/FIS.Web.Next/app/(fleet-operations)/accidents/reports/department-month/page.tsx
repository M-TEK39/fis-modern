import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  AccidentApiError,
  getAccidentDepartmentMonthReport,
  type AccidentDepartmentMonthGarageMode,
  type AccidentDepartmentMonthPeriodMode,
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
  mode: AccidentDepartmentMonthGarageMode;
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

function getPeriodMode(query: ReportQuery): {
  mode: AccidentDepartmentMonthPeriodMode;
  invalid: boolean;
} {
  const rawMode = (getQueryValue(query, "period", "Radio2") ?? "").trim().toLowerCase();
  switch (rawMode) {
    case "month":
    case "radiomon":
    case "":
      return { mode: "month", invalid: false };
    case "year":
    case "radioyear":
      return { mode: "year", invalid: false };
    case "02/03":
    case "radiof23":
      return { mode: "02/03", invalid: false };
    case "01/02":
    case "radioy12":
      return { mode: "01/02", invalid: false };
    default:
      return { mode: "month", invalid: true };
  }
}

function parseInteger(value: string) {
  return /^\d+$/.test(value.trim()) ? Number(value) : null;
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

function reportTitle(mode: AccidentDepartmentMonthPeriodMode) {
  switch (mode) {
    case "year":
      return "Department/Site Accident Report for a Year";
    case "02/03":
      return "Department/Site Accident Report for 02/03";
    case "01/02":
      return "Department/Site Accident Report for 01/02";
    default:
      return "Department/Site Accident Report for a Month";
  }
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
      <h2>The department month report could not be loaded.</h2>
      <p className="muted-copy">Retry when the FIS API is available.</p>
      <Link className="button button-primary" href="/accidents/reports/department-month">
        Try again
      </Link>
    </section>
  );
}

function DepartmentMonthReportTable({
  rows,
  mode,
}: {
  rows: AccidentVehicleReportRow[];
  mode: AccidentDepartmentMonthPeriodMode;
}) {
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="department-month-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="department-month-results-title">{reportTitle(mode)}</h2>
        </div>
        <span className="form-hint">{rows.length} record(s)</span>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">
            {reportTitle(mode)} for the selected garage and department/site
          </caption>
          <thead>
            <tr>
              <th scope="col">Prov Reg Number</th>
              <th scope="col">GG Number</th>
              <th scope="col">Garage</th>
              <th scope="col">Accid Date</th>
              <th scope="col">Accid Place</th>
              <th scope="col">Fin Year</th>
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
                <td>{formatDate(row.accidentDate)}</td>
                <td>{valueOrDash(row.accidentPlace)}</td>
                <td>{valueOrDash(row.financialYear)}</td>
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
      <p className="vehicle-pagination-meta">Total Number: {rows.length}</p>
    </section>
  );
}

async function DepartmentMonthReportContent({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return <SessionRecovery returnPath="/accidents/reports/department-month" />;
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
  const departmentNumber = (getQueryValue(query, "departmentNumber", "xdept") ?? "").trim();
  const rawYear = getQueryValue(query, "year", "XYR") ?? "";
  const rawMonth = getQueryValue(query, "month", "XMON") ?? "";
  const year = parseInteger(rawYear);
  const month = parseInteger(rawMonth);
  const { mode: garage, invalid: invalidGarage } = getGarageMode(query);
  const { mode: period, invalid: invalidPeriod } = getPeriodMode(query);
  const shouldRun =
    getQueryValue(query, "run") === "1" ||
    [
      "departmentNumber",
      "xdept",
      "garage",
      "Radio1",
      "period",
      "Radio2",
      "year",
      "XYR",
      "month",
      "XMON",
    ].some((key) => getQueryValue(query, key) !== undefined);
  let errorMessage: string | null = invalidGarage
    ? "Choose a valid garage."
    : invalidPeriod
      ? "Choose a valid reporting period."
      : null;
  if (
    shouldRun &&
    !errorMessage &&
    (period === "month" || period === "year") &&
    (year === null || year < 1 || year > 9999)
  ) {
    errorMessage = "Enter a year between 1 and 9999 for the selected period.";
  } else if (
    shouldRun &&
    !errorMessage &&
    period === "month" &&
    (month === null || month < 1 || month > 12)
  ) {
    errorMessage = "Enter a month between 1 and 12 for a monthly report.";
  }

  let rows: AccidentVehicleReportRow[] | null = null;
  if (shouldRun && !errorMessage) {
    try {
      rows = await getAccidentDepartmentMonthReport(departmentNumber, garage, period, year, month);
    } catch (error) {
      if (error instanceof AccidentApiError && error.reason === "unauthorized")
        return <SessionRecovery returnPath="/accidents/reports/department-month" />;
      console.error(
        "FIS accident department month report failed",
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
        <fieldset className="vehicle-search-options">
          <legend>Period</legend>
          <label className="vehicle-checkbox-label">
            <input name="period" type="radio" value="month" defaultChecked={period === "month"} />{" "}
            Month
          </label>
          <label className="vehicle-checkbox-label">
            <input name="period" type="radio" value="year" defaultChecked={period === "year"} />{" "}
            Year
          </label>
          <label className="vehicle-checkbox-label">
            <input name="period" type="radio" value="02/03" defaultChecked={period === "02/03"} />{" "}
            02/03
          </label>
          <label className="vehicle-checkbox-label">
            <input name="period" type="radio" value="01/02" defaultChecked={period === "01/02"} />{" "}
            01/02
          </label>
        </fieldset>
        <div className="form-grid">
          <div className="field">
            <label htmlFor="accident-department-month-year">Accident Year</label>
            <input
              id="accident-department-month-year"
              name="year"
              type="number"
              min="1"
              max="9999"
              defaultValue={rawYear}
            />
          </div>
          <div className="field">
            <label htmlFor="accident-department-month-month">Accident Month</label>
            <input
              id="accident-department-month-month"
              name="month"
              type="number"
              min="1"
              max="12"
              defaultValue={rawMonth}
            />
          </div>
          <div className="field">
            <label htmlFor="accident-department-month-department">Dept/Site Code</label>
            <input
              id="accident-department-month-department"
              name="departmentNumber"
              maxLength={30}
              defaultValue={departmentNumber}
            />
          </div>
        </div>
        <p className="form-hint">
          Year and month are used for Month/Year. The 02/03 and 01/02 options use the legacy
          March-to-March windows.
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
          <DepartmentMonthReportTable rows={rows} mode={period} />
        ) : (
          <section className="vehicle-empty-state" aria-live="polite">
            <p className="eyebrow">No accidents found</p>
            <h2>No accidents matched the selected filters.</h2>
            <p className="muted-copy">
              Update the garage, period, year, month, or department and submit again.
            </p>
          </section>
        )
      ) : null}
      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/accidents">
          Accident Menu
        </Link>
        <Link className="button button-secondary" href="/accidents/reports/department-month">
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

export default function DepartmentMonthReportPage({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="accident-department-month-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Accident reports</p>
            <h1 id="accident-department-month-title">
              Accident Report for a Department/Site, for a Month/Year
            </h1>
            <p>Review accident records by garage and calendar or legacy financial period.</p>
          </div>
          <Link className="button button-secondary" href="/accidents/reports">
            Report Menu
          </Link>
        </header>
        <Suspense fallback={<LoadingState />}>
          <DepartmentMonthReportContent searchParams={searchParams} />
        </Suspense>
      </section>
    </main>
  );
}
