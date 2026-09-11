import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import { getDepartments } from "@/lib/api/reference-data/api-departments";
import {
  LossReportApiError,
  getLossReport,
  type LossReportFilters,
  type LossReportMode,
} from "@/lib/api/fleet-operations/api-loss-reports";
import { getLossTypes } from "@/lib/api/fleet-operations/api-loss-types";
import {
  getLossVehicleMatches,
  type LossVehicleMatch,
} from "@/lib/api/fleet-operations/api-losses";
import { getSession } from "@/lib/auth/session";

const REPORT_MODES = ["vehicle", "all", "no-report", "with-report", "dept-period"] as const;
type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export type LossReportPageProps = {
  params: Promise<{ mode: string }>;
  searchParams: SearchParams;
  routePath?: string;
};

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function parsePositiveInt(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function validDate(value: string | undefined) {
  return value && /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : "";
}

function hasLossReportAccess(roles: readonly string[]) {
  return roles.some((role) =>
    ["Losses", "Reports"].some(
      (expected) => role.localeCompare(expected, undefined, { sensitivity: "accent" }) === 0,
    ),
  );
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function titleForMode(mode: LossReportMode) {
  return {
    vehicle: "Losses for One Vehicle",
    all: "All Losses Report",
    "no-report": "Losses Without Department Reports",
    "with-report": "Losses With Department Reports",
    "dept-period": "Losses Report by Department Period",
  }[mode];
}

function filterVehicles(matches: LossVehicleMatch[], mode: "GG" | "GP", search: string) {
  const term = search.trim().toLocaleLowerCase();
  return matches
    .filter((vehicle) => {
      const value = mode === "GP" ? vehicle.registrationNumber : vehicle.fleetNumber;
      return value?.trim().toLocaleLowerCase().includes(term) === true;
    })
    .toSorted((left, right) => {
      const leftValue = mode === "GP" ? left.registrationNumber : left.fleetNumber;
      const rightValue = mode === "GP" ? right.registrationNumber : right.fleetNumber;
      const leftExact = leftValue?.trim().toLocaleLowerCase() === term ? 0 : 1;
      const rightExact = rightValue?.trim().toLocaleLowerCase() === term ? 0 : 1;
      return leftExact - rightExact || left.vmfCode - right.vmfCode;
    });
}

function ReportForm({
  mode,
  query,
  vehicles,
  departments,
  lossTypes,
}: Readonly<{
  mode: LossReportMode;
  query: Record<string, string>;
  vehicles: LossVehicleMatch[];
  departments: Awaited<ReturnType<typeof getDepartments>>;
  lossTypes: Awaited<ReturnType<typeof getLossTypes>>;
}>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <input name="run" type="hidden" value="1" />
      {mode === "vehicle" ? (
        <div className="form-grid">
          <fieldset className="vehicle-search-options">
            <legend>Find vehicle by</legend>
            <label className="vehicle-checkbox-label">
              <input
                type="radio"
                name="searchMode"
                value="GG"
                defaultChecked={query.searchMode !== "GP"}
              />{" "}
              GG
            </label>
            <label className="vehicle-checkbox-label">
              <input
                type="radio"
                name="searchMode"
                value="GP"
                defaultChecked={query.searchMode === "GP"}
              />{" "}
              GP
            </label>
          </fieldset>
          <div className="form-field">
            <label className="form-label" htmlFor="loss-report-vehicle-search">
              Vehicle number
            </label>
            <input
              className="form-input"
              id="loss-report-vehicle-search"
              name="vehicleNumber"
              defaultValue={query.vehicleNumber}
              maxLength={30}
              placeholder={query.searchMode === "GP" ? "GP number" : "GG number"}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="loss-report-vmf">
              Vehicle
            </label>
            <select
              className="form-select"
              id="loss-report-vmf"
              name="vmfCode"
              defaultValue={query.vmfCode}
            >
              <option value="">Select vehicle...</option>
              {vehicles.map((vehicle) => (
                <option key={vehicle.vmfCode} value={vehicle.vmfCode}>
                  {valueOrDash(vehicle.fleetNumber)} / {valueOrDash(vehicle.registrationNumber)} (
                  {vehicle.vmfCode})
                </option>
              ))}
            </select>
            {vehicles.length === 0 && query.vehicleNumber ? (
              <p className="form-hint">
                No exact vehicle match was found. Search by the other identifier.
              </p>
            ) : null}
          </div>
        </div>
      ) : null}
      {mode === "all" ? (
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="loss-report-type">
              Loss Type
            </label>
            <select
              className="form-select"
              id="loss-report-type"
              name="lossTypeCode"
              defaultValue={query.lossTypeCode}
            >
              <option value="">All loss types</option>
              {lossTypes
                .toSorted((left, right) =>
                  (left.description ?? "").localeCompare(right.description ?? ""),
                )
                .map((lossType) => (
                  <option key={lossType.lossTypeCode} value={lossType.lossTypeCode}>
                    {valueOrDash(lossType.description)} ({lossType.lossTypeCode})
                  </option>
                ))}
            </select>
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="loss-report-department">
              Department
            </label>
            <select
              className="form-select"
              id="loss-report-department"
              name="department"
              defaultValue={query.department}
            >
              <option value="">All departments</option>
              {departments
                .toSorted((left, right) =>
                  (left.description ?? "").localeCompare(right.description ?? ""),
                )
                .map((department) => (
                  <option
                    key={department.departmentCode}
                    value={department.departmentNumber ?? department.departmentCode}
                  >
                    {valueOrDash(department.description)} (
                    {valueOrDash(department.departmentNumber ?? department.departmentCode)})
                  </option>
                ))}
            </select>
          </div>
        </div>
      ) : null}
      {mode === "no-report" ? (
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="loss-report-no-department">
              Department
            </label>
            <select
              className="form-select"
              id="loss-report-no-department"
              name="department"
              defaultValue={query.department}
            >
              <option value="">All departments</option>
              {departments
                .toSorted((left, right) =>
                  (left.description ?? "").localeCompare(right.description ?? ""),
                )
                .map((department) => (
                  <option
                    key={department.departmentCode}
                    value={department.departmentNumber ?? department.departmentCode}
                  >
                    {valueOrDash(department.description)} (
                    {valueOrDash(department.departmentNumber ?? department.departmentCode)})
                  </option>
                ))}
            </select>
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="loss-report-no-date">
              Loss Date from
            </label>
            <input
              className="form-input"
              id="loss-report-no-date"
              name="beginDate"
              type="date"
              defaultValue={query.beginDate}
            />
          </div>
        </div>
      ) : null}
      {mode === "with-report" ? (
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="loss-report-status">
              Report Status
            </label>
            <select
              className="form-select"
              id="loss-report-status"
              name="reportStatus"
              defaultValue={query.reportStatus || "Submitted"}
            >
              <option>Submitted</option>
              <option>Verified</option>
            </select>
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="loss-report-with-date">
              Reported Date from
            </label>
            <input
              className="form-input"
              id="loss-report-with-date"
              name="beginDate"
              type="date"
              defaultValue={query.beginDate}
            />
          </div>
        </div>
      ) : null}
      {mode === "dept-period" ? (
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="loss-report-dept">
              Department
            </label>
            <select
              className="form-select"
              id="loss-report-dept"
              name="department"
              defaultValue={query.department}
            >
              <option value="">All departments</option>
              {departments
                .toSorted((left, right) =>
                  (left.description ?? "").localeCompare(right.description ?? ""),
                )
                .map((department) => (
                  <option
                    key={department.departmentCode}
                    value={department.departmentNumber ?? department.departmentCode}
                  >
                    {valueOrDash(department.description)} (
                    {valueOrDash(department.departmentNumber ?? department.departmentCode)})
                  </option>
                ))}
            </select>
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="loss-report-from">
              Begin Date
            </label>
            <input
              className="form-input"
              id="loss-report-from"
              name="beginDate"
              type="date"
              defaultValue={query.beginDate}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="loss-report-to">
              End Date
            </label>
            <input
              className="form-input"
              id="loss-report-to"
              name="endDate"
              type="date"
              defaultValue={query.endDate}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="loss-report-hire">
              Hire Type
            </label>
            <select
              className="form-select"
              id="loss-report-hire"
              name="hireType"
              defaultValue={query.hireType || "VIP"}
            >
              <option>VIP</option>
              <option>GG</option>
              <option>Permanent</option>
            </select>
          </div>
        </div>
      ) : null}
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Generate report
        </button>
        <Link className="button button-secondary" href="/losses/reports">
          Reports menu
        </Link>
      </div>
    </form>
  );
}

function ReportResults({
  report,
}: Readonly<{ report: { columns: string[]; rows: Array<Record<string, string | null>> } }>) {
  if (report.rows.length === 0)
    return (
      <section className="vehicle-empty-state" aria-live="polite">
        <p className="eyebrow">No records found</p>
        <h2>No losses matched the selected filters.</h2>
        <p className="muted-copy">Adjust the report parameters and try again.</p>
      </section>
    );
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="loss-report-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="loss-report-results-title">{report.rows.length} record(s) returned</h2>
        </div>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">Loss report results</caption>
          <thead>
            <tr>
              {report.columns.map((column) => (
                <th key={column} scope="col">
                  {column}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {report.rows.map((row, index) => (
              <tr key={`${row[report.columns[0]] ?? "row"}-${index}`}>
                {report.columns.map((column) => (
                  <td key={column}>{valueOrDash(row[column])}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function ApiUnavailable({ routePath }: Readonly<{ routePath: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h1>Loss report data could not be loaded.</h1>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href={routePath}>
        Try again
      </Link>
    </section>
  );
}

async function LossReportPageContent({
  params,
  searchParams,
  routePath = "/losses/reports",
}: LossReportPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  if (!hasLossReportAccess(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h1>You do not have permission to run Losses reports.</h1>
        </section>
      </main>
    );

  const modeValue = (await params).mode.trim().toLowerCase();
  if (!REPORT_MODES.includes(modeValue as LossReportMode)) notFound();
  const mode = modeValue as LossReportMode;
  const rawQuery = await searchParams;
  const query = Object.fromEntries(
    Object.entries(rawQuery).map(([key, value]) => [key, queryValue(value) ?? ""]),
  );
  const searchMode = query.searchMode === "GP" || query.Radio1 === "Radiogp" ? "GP" : "GG";
  const vehicleNumber = (query.vehicleNumber || query.txtGGNum || query.searchQuery || "")
    .trim()
    .slice(0, 30);
  const vmfCode = parsePositiveInt(query.vmfCode || query.vmf);
  const beginDate = validDate(query.beginDate || query.BDAT || query.from);
  const endDate = validDate(query.endDate || query.EDAT || query.to);
  const run = query.run === "1";

  let vehicles: LossVehicleMatch[] = [];
  let departments: Awaited<ReturnType<typeof getDepartments>> = [];
  let lossTypes: Awaited<ReturnType<typeof getLossTypes>> = [];
  let report: Awaited<ReturnType<typeof getLossReport>> | null = null;
  let errorMessage: string | null = null;

  try {
    const lookups = await Promise.all([
      mode === "vehicle" && vehicleNumber
        ? getLossVehicleMatches(vehicleNumber)
        : Promise.resolve([] as LossVehicleMatch[]),
      mode !== "vehicle"
        ? getDepartments()
        : Promise.resolve([] as Awaited<ReturnType<typeof getDepartments>>),
      mode === "all"
        ? getLossTypes()
        : Promise.resolve([] as Awaited<ReturnType<typeof getLossTypes>>),
    ]);
    vehicles = filterVehicles(lookups[0], searchMode, vehicleNumber);
    departments = lookups[1];
    lossTypes = lookups[2];

    if (run) {
      if (mode === "vehicle" && !vmfCode)
        errorMessage = "Select a vehicle before generating this report.";
      else if (mode === "dept-period" && (!beginDate || !endDate))
        errorMessage = "Enter both a begin date and an end date before generating this report.";
      else if (mode === "dept-period" && beginDate > endDate)
        errorMessage = "The begin date must be before the end date.";
      else {
        const filters: LossReportFilters = {
          vmfCode: vmfCode ?? undefined,
          vehicleNumber: vehicleNumber || undefined,
          searchMode,
          lossTypeCode: parsePositiveInt(query.lossTypeCode) ?? undefined,
          department: query.department.trim() || undefined,
          beginDate: beginDate || undefined,
          endDate: endDate || undefined,
          reportStatus: query.reportStatus || undefined,
          hireType:
            query.hireType === "GG" || query.hireType === "Permanent" ? query.hireType : "VIP",
        };
        report = await getLossReport(mode, filters);
      }
    }
  } catch (error) {
    if (error instanceof LossReportApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    console.error(
      "FIS loss report request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="loss-report-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Losses reports</p>
            <h1 id="loss-report-title">{titleForMode(mode)}</h1>
            <p>Run the legacy Losses report against the current compatible data.</p>
          </div>
          <Link className="button button-secondary" href="/losses/reports">
            Reports menu
          </Link>
        </header>
        <ReportForm
          mode={mode}
          query={{
            ...query,
            searchMode,
            vehicleNumber,
            vmfCode: vmfCode ? String(vmfCode) : "",
            beginDate,
            endDate,
          }}
          vehicles={vehicles}
          departments={departments}
          lossTypes={lossTypes}
        />
        {errorMessage ? (
          <div className="notice notice-error" role="alert">
            {errorMessage}
          </div>
        ) : null}
        {report ? (
          <ReportResults report={report} />
        ) : (
          <section className="vehicle-status-card">
            <p className="eyebrow">Parameters required</p>
            <h2>Enter the report parameters and generate the report.</h2>
          </section>
        )}
      </section>
    </main>
  );
}

export default function LossReportPage(props: LossReportPageProps) {
  return (
    <StreamedRoute>
      <LossReportPageContent {...props} />
    </StreamedRoute>
  );
}
