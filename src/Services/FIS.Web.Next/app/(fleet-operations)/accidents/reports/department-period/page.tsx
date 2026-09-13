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
  getAccidentDepartmentPeriodReport,
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

const departmentPeriodColumns: readonly VehicleTableColumn<AccidentVehicleReportRow>[] = [
  {
    key: "registrationNumber",
    label: "Prov Reg Number",
    render: (row) => valueOrDash(row.registrationNumber),
  },
  { key: "fleetNumber", label: "GG Number", render: (row) => valueOrDash(row.fleetNumber) },
  { key: "callRefer", label: "Call Refer", render: (row) => valueOrDash(row.callRefer) },
  {
    key: "locationDescription",
    label: "Garage",
    render: (row) => valueOrDash(row.locationDescription),
  },
  { key: "accidentDate", label: "Accid Date", render: (row) => formatDate(row.accidentDate) },
  { key: "accidentPlace", label: "Accid Place", render: (row) => valueOrDash(row.accidentPlace) },
  {
    key: "accidentTypeDescription",
    label: "Accident Category",
    render: (row) => valueOrDash(row.accidentTypeDescription),
  },
  { key: "tripAuthority", label: "Trip Auth", render: (row) => valueOrDash(row.tripAuthority) },
  { key: "driverName", label: "Driver Name", render: (row) => valueOrDash(row.driverName) },
  {
    key: "departmentNumber",
    label: "Dept/Site Code",
    render: (row) => valueOrDash(row.departmentNumber),
  },
  { key: "siteDescription", label: "Dept/Site", render: (row) => valueOrDash(row.siteDescription) },
  {
    key: "transportOfficerName",
    label: "Trans Officer",
    render: (row) => valueOrDash(row.transportOfficerName),
  },
  {
    key: "transportOfficerTelephone",
    label: "TO Tel",
    render: (row) => valueOrDash(row.transportOfficerTelephone),
  },
  { key: "hqReference", label: "HQ Ref", render: (row) => valueOrDash(row.hqReference) },
  { key: "ggReference", label: "GG Ref", render: (row) => valueOrDash(row.ggReference) },
  { key: "caseNumber", label: "Case Num", render: (row) => valueOrDash(row.caseNumber) },
  { key: "costOfRepair", label: "GG Car Damage", render: (row) => valueOrDash(row.costOfRepair) },
  {
    key: "thirdPartyClaim",
    label: "Priv Car Damage",
    render: (row) => valueOrDash(row.thirdPartyClaim),
  },
  {
    key: "claimAgainstDepartment",
    label: "Cost Claim Agains Dept",
    render: (row) => valueOrDash(row.claimAgainstDepartment),
  },
  { key: "notes", label: "Notes", render: (row) => valueOrDash(row.notes) },
];

function DepartmentPeriodReportTable({
  rows,
  total,
}: {
  rows: AccidentVehicleReportRow[];
  total: number;
}) {
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="department-period-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="department-period-results-title">Department/Site Accident Report</h2>
        </div>
        <span className="form-hint">{total} record(s)</span>
      </div>
      <div className="vehicle-table-wrapper">
        <VehicleTable
          caption="Accident report for the selected department or site and period"
          columns={departmentPeriodColumns}
          rows={rows}
          rowKey={(row) => row.accidentCode}
        />
      </div>
      <p className="vehicle-pagination-meta">Total Number: {total}</p>
    </section>
  );
}

function DepartmentPeriodReportForm({
  errorMessage,
  locationOptions,
  departmentNumber,
  startDate,
  endDate,
}: {
  errorMessage: string | null;
  locationOptions: LocationOption[];
  departmentNumber: string;
  startDate: string;
  endDate: string;
}) {
  return (
    <>
      <AccidentReportFormError message={errorMessage} />
      <form className="vehicle-status-maintenance-panel" method="get">
        <div className="form-grid">
          <AccidentDepartmentSelect
            id="accident-department-period-department"
            value={departmentNumber}
            options={locationOptions}
          />
          <AccidentDateRangeFields
            startId="accident-department-period-start"
            endId="accident-department-period-end"
            startDate={startDate}
            endDate={endDate}
          />
        </div>
        <AccidentReportFormActions />
      </form>
    </>
  );
}

function DepartmentPeriodReportResults({
  query,
  report,
}: {
  query: ReportQuery;
  report: AccidentReportPage<AccidentVehicleReportRow> | null;
}) {
  if (report === null) {
    return null;
  }

  const rows = report.items;
  return rows.length > 0 ? (
    <>
      <DepartmentPeriodReportTable rows={rows} total={report.total} />
      <AccidentReportPagination
        page={report.page}
        pageSize={report.pageSize}
        pathname="/accidents/reports/department-period"
        query={query}
        total={report.total}
        totalPages={report.totalPages}
      />
    </>
  ) : (
    <section className="vehicle-empty-state" aria-live="polite">
      <p className="eyebrow">No accidents found</p>
      <h2>No accidents matched the selected department and period.</h2>
      <p className="muted-copy">Update the department or dates and submit again.</p>
    </section>
  );
}

const DepartmentPeriodReportContent = renderDepartmentPeriodReportContent;

async function renderDepartmentPeriodReportContent({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  const authorization = await authorizeAccidentReport();
  if (authorization === "expired")
    return <SessionRecovery returnPath="/accidents/reports/department-period" />;
  if (authorization === "unavailable") {
    return (
      <AccidentReportErrorState
        title="The department accident report could not be loaded."
        retryHref="/accidents/reports/department-period"
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
    context: "FIS accident department period department/site lookup failed",
    isUnauthorized: (error) =>
      (error instanceof DepartmentApiError && error.reason === "unauthorized") ||
      (error instanceof SiteApiError && error.reason === "unauthorized"),
  });
  if (locations.status === "unauthorized") {
    return <SessionRecovery returnPath="/accidents/reports/department-period" />;
  }
  if (locations.status !== "success") {
    return (
      <AccidentReportErrorState
        title="The department accident report could not be loaded."
        retryHref="/accidents/reports/department-period"
      />
    );
  }
  const locationOptions = locations.data;

  const query = await searchParams;
  const departmentNumber = (getQueryValue(query, "departmentNumber", "xdept") ?? "").trim();
  const startDate = normalizeDate(getQueryValue(query, "startDate", "BDAT") ?? "");
  const endDate = normalizeDate(getQueryValue(query, "endDate", "EDAT") ?? "");
  const shouldRun =
    getQueryValue(query, "run") === "1" ||
    ["departmentNumber", "xdept", "startDate", "BDAT", "endDate", "EDAT"].some(
      (key) => getQueryValue(query, key) !== undefined,
    );
  let errorMessage: string | null = null;
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
      getAccidentDepartmentPeriodReport(departmentNumber, startDate, endDate, page, pageSize),
    context: "FIS accident department period report failed",
  });
  if (report.status === "unauthorized") {
    return <SessionRecovery returnPath="/accidents/reports/department-period" />;
  }
  if (report.status === "error") {
    return (
      <AccidentReportErrorState
        title="The department accident report could not be loaded."
        retryHref="/accidents/reports/department-period"
      />
    );
  }
  return (
    <>
      <DepartmentPeriodReportForm
        errorMessage={errorMessage}
        locationOptions={locationOptions}
        departmentNumber={departmentNumber}
        startDate={startDate}
        endDate={endDate}
      />
      <DepartmentPeriodReportResults query={query} report={report.data} />
      <AccidentReportFooter clearHref="/accidents/reports/department-period" />
    </>
  );
}

export default function DepartmentPeriodReportPage({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  return (
    <AccidentReportPageShell
      titleId="accident-department-period-title"
      title="Accident Report for a Department/Site, for a Period"
      description="Review the legacy department or site accident report for an inclusive date period."
      fallback={<AccidentReportLoadingState />}
    >
      <Suspense fallback={<AccidentReportLoadingState />}>
        <DepartmentPeriodReportContent searchParams={searchParams} />
      </Suspense>
    </AccidentReportPageShell>
  );
}
