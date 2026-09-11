import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  AccidentApiError,
  getAccidentDepartmentPeriodVipReport,
  type AccidentDepartmentPeriodVipMode,
  type AccidentVehicleReportRow,
} from "@/lib/api/fleet-operations/api-accidents";
import {
  DepartmentApiError,
  getDepartments,
  type DepartmentRecord,
} from "@/lib/api/reference-data/api-departments";
import { getSites, SiteApiError, type SiteRecord } from "@/lib/api/reference-data/api-sites";
import { getSession } from "@/lib/auth/session";

const ACCIDENTS_ROLE = "Accidents";
type QueryValue = string | string[] | undefined;
type ReportQuery = Record<string, QueryValue>;
type LocationOption = { value: string; label: string };

function getLocationOptions(
  departments: readonly DepartmentRecord[],
  sites: readonly SiteRecord[],
): LocationOption[] {
  const labelsByDepartmentNumber = new Map<string, Set<string>>();

  function addOption(kind: string, description: string | null, departmentNumber: string | null) {
    const value = departmentNumber?.trim();
    if (!value) return;

    const labels = labelsByDepartmentNumber.get(value) ?? new Set<string>();
    labels.add(`${kind}: ${description?.trim() || "Unnamed"}`);
    labelsByDepartmentNumber.set(value, labels);
  }

  departments.forEach((department) =>
    addOption("Department", department.description, department.departmentNumber),
  );
  sites.forEach((site) => addOption("Site", site.description, site.departmentNumber));

  return Array.from(labelsByDepartmentNumber, ([value, labels]) => ({
    value,
    label: `${Array.from(labels).join(" / ")} (${value})`,
  })).toSorted((left, right) => left.label.localeCompare(right.label));
}

function getQueryValue(query: ReportQuery, ...keys: string[]) {
  for (const key of keys) {
    const value = query[key];
    if (value !== undefined) {
      return Array.isArray(value) ? value[0] : value;
    }
  }

  return undefined;
}

function normalizeDate(value: string) {
  const normalized = value.trim();
  if (/^\d{4}-\d{2}-\d{2}$/.test(normalized)) {
    return normalized;
  }

  const yearFirst = normalized.match(/^(\d{4})[\/-](\d{1,2})[\/-](\d{1,2})$/);
  if (yearFirst) {
    return `${yearFirst[1]}-${yearFirst[2].padStart(2, "0")}-${yearFirst[3].padStart(2, "0")}`;
  }

  const dayFirst = normalized.match(/^(\d{1,2})[\/-](\d{1,2})[\/-](\d{4})$/);
  if (dayFirst) {
    return `${dayFirst[3]}-${dayFirst[2].padStart(2, "0")}-${dayFirst[1].padStart(2, "0")}`;
  }

  return "";
}

function isValidDate(value: string) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return false;
  }

  const [year, month, day] = value.split("-").map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  return (
    date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day
  );
}

function getMode(query: ReportQuery): { mode: AccidentDepartmentPeriodVipMode; invalid: boolean } {
  const rawMode = (getQueryValue(query, "mode", "hireType", "Radio2") ?? "").trim().toLowerCase();
  switch (rawMode) {
    case "vip":
    case "radiovip":
      return { mode: "vip", invalid: false };
    case "pool":
    case "radiopool":
      return { mode: "pool", invalid: false };
    case "permanent":
    case "radioperm":
      return { mode: "permanent", invalid: false };
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
  if (!value) {
    return "-";
  }

  const normalized = normalizeDate(value.slice(0, 10));
  return normalized || value;
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
      <h2>The VIP/GG accident report could not be loaded.</h2>
      <p className="muted-copy">Retry when the FIS API is available.</p>
      <Link className="button button-primary" href="/accidents/reports/department-period-vip">
        Try again
      </Link>
    </section>
  );
}

function reportTitle(mode: AccidentDepartmentPeriodVipMode) {
  switch (mode) {
    case "vip":
      return "VIP Accidents";
    case "pool":
      return "Pool Accidents";
    case "permanent":
      return "Permanent Accidents";
    default:
      return "All Department/Site Accidents";
  }
}

function DepartmentPeriodVipReportTable({
  rows,
  mode,
}: {
  rows: AccidentVehicleReportRow[];
  mode: AccidentDepartmentPeriodVipMode;
}) {
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="department-period-vip-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="department-period-vip-results-title">{reportTitle(mode)}</h2>
        </div>
        <span className="form-hint">{rows.length} record(s)</span>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">
            {reportTitle(mode)} for the selected department or site and period
          </caption>
          <thead>
            <tr>
              <th scope="col">Reg Number</th>
              <th scope="col">GG Number</th>
              <th scope="col">Accid Date</th>
              <th scope="col">Dept/Site Code</th>
              <th scope="col">Dept/Site</th>
              <th scope="col">Hire Type</th>
              <th scope="col">Accident Description</th>
              <th scope="col">Driver</th>
              <th scope="col">Trans Officer</th>
              <th scope="col">Call Refer</th>
              <th scope="col">GG Car Damage</th>
              <th scope="col">Cost Claim Against Dept</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row, index) => (
              <tr key={`${row.accidentCode}-${index}`}>
                <td>{valueOrDash(row.registrationNumber)}</td>
                <td>{valueOrDash(row.fleetNumber)}</td>
                <td>{formatDate(row.accidentDate)}</td>
                <td>{valueOrDash(row.departmentNumber)}</td>
                <td>{valueOrDash(row.siteDescription)}</td>
                <td>{valueOrDash(row.hireType)}</td>
                <td>{valueOrDash(row.accidentTypeDescription)}</td>
                <td>{valueOrDash(row.driverName)}</td>
                <td>{valueOrDash(row.transportOfficerName)}</td>
                <td>{valueOrDash(row.callRefer)}</td>
                <td>{valueOrDash(row.costOfRepair)}</td>
                <td>{valueOrDash(row.claimAgainstDepartment)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="vehicle-pagination-meta">Total Number: {rows.length}</p>
    </section>
  );
}

async function DepartmentPeriodVipReportContent({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return <SessionRecovery returnPath="/accidents/reports/department-period-vip" />;
  if (session.status === "unavailable") return <ErrorState />;
  if (!hasRole(session.roles, ACCIDENTS_ROLE)) {
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Access restricted</p>
        <h2>You do not have permission to run accident reports.</h2>
      </section>
    );
  }

  let locationOptions: LocationOption[];
  try {
    const [departments, sites] = await Promise.all([getDepartments(), getSites()]);
    locationOptions = getLocationOptions(departments, sites);
  } catch (error) {
    if (
      (error instanceof DepartmentApiError && error.reason === "unauthorized") ||
      (error instanceof SiteApiError && error.reason === "unauthorized")
    ) {
      return <SessionRecovery returnPath="/accidents/reports/department-period-vip" />;
    }
    console.error(
      "FIS accident department period VIP department/site lookup failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return <ErrorState />;
  }

  const query = await searchParams;
  const departmentNumber = (getQueryValue(query, "departmentNumber", "xdept") ?? "").trim();
  const startDate = normalizeDate(getQueryValue(query, "startDate", "BDAT") ?? "");
  const endDate = normalizeDate(getQueryValue(query, "endDate", "EDAT") ?? "");
  const { mode, invalid } = getMode(query);
  const shouldRun =
    getQueryValue(query, "run") === "1" ||
    [
      "departmentNumber",
      "xdept",
      "startDate",
      "BDAT",
      "endDate",
      "EDAT",
      "mode",
      "hireType",
      "Radio2",
    ].some((key) => getQueryValue(query, key) !== undefined);
  let errorMessage: string | null = invalid ? "Choose a valid hire type report." : null;
  if (shouldRun && (!startDate || !endDate || !isValidDate(startDate) || !isValidDate(endDate))) {
    errorMessage = "Enter a valid begin date and end date.";
  } else if (shouldRun && endDate < startDate) {
    errorMessage = "The begin date must be on or before the end date.";
  }

  let rows: AccidentVehicleReportRow[] | null = null;
  if (shouldRun && !errorMessage) {
    try {
      rows = await getAccidentDepartmentPeriodVipReport(departmentNumber, startDate, endDate, mode);
    } catch (error) {
      if (error instanceof AccidentApiError && error.reason === "unauthorized")
        return <SessionRecovery returnPath="/accidents/reports/department-period-vip" />;
      console.error(
        "FIS accident department period VIP report failed",
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
            <label htmlFor="accident-department-period-vip-department">Department or site</label>
            <select
              id="accident-department-period-vip-department"
              name="departmentNumber"
              defaultValue={departmentNumber}
            >
              <option value="">All departments and sites</option>
              {locationOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="accident-department-period-vip-start">Begin Date</label>
            <input
              id="accident-department-period-vip-start"
              name="startDate"
              type="date"
              defaultValue={startDate}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="accident-department-period-vip-end">End Date</label>
            <input
              id="accident-department-period-vip-end"
              name="endDate"
              type="date"
              defaultValue={endDate}
              required
            />
          </div>
        </div>
        <fieldset className="vehicle-search-options">
          <legend>Hire Type</legend>
          <label className="vehicle-checkbox-label">
            <input name="mode" type="radio" value="all" defaultChecked={mode === "all"} /> All
          </label>
          <label className="vehicle-checkbox-label">
            <input name="mode" type="radio" value="vip" defaultChecked={mode === "vip"} /> VIP
          </label>
          <label className="vehicle-checkbox-label">
            <input name="mode" type="radio" value="pool" defaultChecked={mode === "pool"} /> Pool
          </label>
          <label className="vehicle-checkbox-label">
            <input
              name="mode"
              type="radio"
              value="permanent"
              defaultChecked={mode === "permanent"}
            />{" "}
            Permanent
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
          <DepartmentPeriodVipReportTable rows={rows} mode={mode} />
        ) : (
          <section className="vehicle-empty-state" aria-live="polite">
            <p className="eyebrow">No accidents found</p>
            <h2>No accidents matched the selected filters.</h2>
            <p className="muted-copy">
              Update the department, dates, or hire type and submit again.
            </p>
          </section>
        )
      ) : null}
      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/accidents">
          Accident Menu
        </Link>
        <Link className="button button-secondary" href="/accidents/reports/department-period-vip">
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

export default function DepartmentPeriodVipReportPage({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="accident-department-period-vip-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Accident reports</p>
            <h1 id="accident-department-period-vip-title">
              Accident Report on VIP/GG and Hire Type
            </h1>
            <p>Review department or site accidents by inclusive period and vehicle hire type.</p>
          </div>
          <Link className="button button-secondary" href="/accidents/reports">
            Report Menu
          </Link>
        </header>
        <Suspense fallback={<LoadingState />}>
          <DepartmentPeriodVipReportContent searchParams={searchParams} />
        </Suspense>
      </section>
    </main>
  );
}
