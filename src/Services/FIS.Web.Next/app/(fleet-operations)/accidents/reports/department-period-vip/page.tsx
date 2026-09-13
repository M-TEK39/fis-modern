import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { Suspense } from "react";
import {
  AccidentDateRangeFields,
  AccidentDepartmentSelect,
  AccidentReportAccessRestricted,
  AccidentReportErrorState,
  AccidentReportFooter,
  AccidentReportFormActions,
  AccidentReportFormError,
  AccidentReportLoadingState,
  AccidentReportPagination,
  AccidentReportPageShell,
} from "@/app/(fleet-operations)/accidents/reports/_report-components";
import {
  authorizeAccidentReport,
  getAccidentReportPageState,
  loadAccidentReport,
} from "@/app/(fleet-operations)/accidents/reports/_report-runtime";
import VehicleTable, {
  type VehicleTableColumn,
} from "@/app/(fleet-operations)/accidents/vehicle-table";
import {
  getAccidentDepartmentPeriodVipReport,
  type AccidentDepartmentPeriodVipMode,
  type AccidentReportPage,
  type AccidentVehicleReportRow,
} from "@/lib/api/fleet-operations/api-accidents";
import {
  DepartmentApiError,
  getDepartments,
  type DepartmentRecord,
} from "@/lib/api/reference-data/api-departments";
import { getSites, SiteApiError, type SiteRecord } from "@/lib/api/reference-data/api-sites";
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

const departmentPeriodVipColumns: readonly VehicleTableColumn<AccidentVehicleReportRow>[] = [
  {
    key: "registrationNumber",
    label: "Reg Number",
    render: (row) => valueOrDash(row.registrationNumber),
  },
  { key: "fleetNumber", label: "GG Number", render: (row) => valueOrDash(row.fleetNumber) },
  { key: "accidentDate", label: "Accid Date", render: (row) => formatDate(row.accidentDate) },
  {
    key: "departmentNumber",
    label: "Dept/Site Code",
    render: (row) => valueOrDash(row.departmentNumber),
  },
  { key: "siteDescription", label: "Dept/Site", render: (row) => valueOrDash(row.siteDescription) },
  { key: "hireType", label: "Hire Type", render: (row) => valueOrDash(row.hireType) },
  {
    key: "accidentTypeDescription",
    label: "Accident Description",
    render: (row) => valueOrDash(row.accidentTypeDescription),
  },
  { key: "driverName", label: "Driver", render: (row) => valueOrDash(row.driverName) },
  {
    key: "transportOfficerName",
    label: "Trans Officer",
    render: (row) => valueOrDash(row.transportOfficerName),
  },
  { key: "callRefer", label: "Call Refer", render: (row) => valueOrDash(row.callRefer) },
  { key: "costOfRepair", label: "GG Car Damage", render: (row) => valueOrDash(row.costOfRepair) },
  {
    key: "claimAgainstDepartment",
    label: "Cost Claim Against Dept",
    render: (row) => valueOrDash(row.claimAgainstDepartment),
  },
];

function DepartmentPeriodVipReportTable({
  rows,
  total,
  mode,
}: {
  rows: AccidentVehicleReportRow[];
  total: number;
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
        <span className="form-hint">{total} record(s)</span>
      </div>
      <div className="vehicle-table-wrapper">
        <VehicleTable
          caption={`${reportTitle(mode)} for the selected department or site and period`}
          columns={departmentPeriodVipColumns}
          rows={rows}
          rowKey={(row) => row.accidentCode}
        />
      </div>
      <p className="vehicle-pagination-meta">Total Number: {total}</p>
    </section>
  );
}

function DepartmentPeriodVipReportForm({
  errorMessage,
  locationOptions,
  departmentNumber,
  startDate,
  endDate,
  mode,
}: {
  errorMessage: string | null;
  locationOptions: LocationOption[];
  departmentNumber: string;
  startDate: string;
  endDate: string;
  mode: AccidentDepartmentPeriodVipMode;
}) {
  return (
    <>
      <AccidentReportFormError message={errorMessage} />
      <form className="vehicle-status-maintenance-panel" method="get">
        <div className="form-grid">
          <AccidentDepartmentSelect
            id="accident-department-period-vip-department"
            value={departmentNumber}
            options={locationOptions}
          />
          <AccidentDateRangeFields
            startId="accident-department-period-vip-start"
            endId="accident-department-period-vip-end"
            startDate={startDate}
            endDate={endDate}
          />
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
        <AccidentReportFormActions />
      </form>
    </>
  );
}

function DepartmentPeriodVipReportResults({
  query,
  report,
  mode,
}: {
  query: ReportQuery;
  report: AccidentReportPage<AccidentVehicleReportRow> | null;
  mode: AccidentDepartmentPeriodVipMode;
}) {
  if (report === null) {
    return null;
  }

  const rows = report.items;
  return rows.length > 0 ? (
    <>
      <DepartmentPeriodVipReportTable rows={rows} total={report.total} mode={mode} />
      <AccidentReportPagination
        page={report.page}
        pageSize={report.pageSize}
        pathname="/accidents/reports/department-period-vip"
        query={query}
        total={report.total}
        totalPages={report.totalPages}
      />
    </>
  ) : (
    <section className="vehicle-empty-state" aria-live="polite">
      <p className="eyebrow">No accidents found</p>
      <h2>No accidents matched the selected filters.</h2>
      <p className="muted-copy">Update the department, dates, or hire type and submit again.</p>
    </section>
  );
}

const DepartmentPeriodVipReportContent = renderDepartmentPeriodVipReportContent;

async function renderDepartmentPeriodVipReportContent({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  const authorization = await authorizeAccidentReport();
  if (authorization === "expired")
    return <SessionRecovery returnPath="/accidents/reports/department-period-vip" />;
  if (authorization === "unavailable") {
    return (
      <AccidentReportErrorState
        title="The VIP/GG accident report could not be loaded."
        retryHref="/accidents/reports/department-period-vip"
      />
    );
  }
  if (authorization === "forbidden") {
    return <AccidentReportAccessRestricted />;
  }

  const locations = await loadAccidentReport({
    shouldRun: true,
    errorMessage: null,
    load: async () => {
      const [departments, sites] = await Promise.all([getDepartments(), getSites()]);
      return getLocationOptions(departments, sites);
    },
    context: "FIS accident department period VIP department/site lookup failed",
    isUnauthorized: (error) =>
      (error instanceof DepartmentApiError && error.reason === "unauthorized") ||
      (error instanceof SiteApiError && error.reason === "unauthorized"),
  });
  if (locations.status === "unauthorized") {
    return <SessionRecovery returnPath="/accidents/reports/department-period-vip" />;
  }
  if (locations.status !== "success") {
    return (
      <AccidentReportErrorState
        title="The VIP/GG accident report could not be loaded."
        retryHref="/accidents/reports/department-period-vip"
      />
    );
  }
  const locationOptions = locations.data;

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

  const { page, pageSize } = getAccidentReportPageState(query);

  const report = await loadAccidentReport({
    shouldRun,
    errorMessage,
    load: () =>
      getAccidentDepartmentPeriodVipReport(
        departmentNumber,
        startDate,
        endDate,
        mode,
        page,
        pageSize,
      ),
    context: "FIS accident department period VIP report failed",
  });
  if (report.status === "unauthorized") {
    return <SessionRecovery returnPath="/accidents/reports/department-period-vip" />;
  }
  if (report.status === "error") {
    return (
      <AccidentReportErrorState
        title="The VIP/GG accident report could not be loaded."
        retryHref="/accidents/reports/department-period-vip"
      />
    );
  }
  return (
    <>
      <DepartmentPeriodVipReportForm
        errorMessage={errorMessage}
        locationOptions={locationOptions}
        departmentNumber={departmentNumber}
        startDate={startDate}
        endDate={endDate}
        mode={mode}
      />
      <DepartmentPeriodVipReportResults query={query} report={report.data} mode={mode} />
      <AccidentReportFooter clearHref="/accidents/reports/department-period-vip" />
    </>
  );
}

export default function DepartmentPeriodVipReportPage({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  return (
    <AccidentReportPageShell
      titleId="accident-department-period-vip-title"
      title="Accident Report on VIP/GG and Hire Type"
      description="Review department or site accidents by inclusive period and vehicle hire type."
      fallback={<AccidentReportLoadingState />}
    >
      <Suspense fallback={<AccidentReportLoadingState />}>
        <DepartmentPeriodVipReportContent searchParams={searchParams} />
      </Suspense>
    </AccidentReportPageShell>
  );
}
