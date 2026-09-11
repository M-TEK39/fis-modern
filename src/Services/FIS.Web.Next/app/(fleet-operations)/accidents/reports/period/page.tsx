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
  AccidentReportPageShell,
} from "@/app/(fleet-operations)/accidents/reports/_report-components";
import {
  authorizeAccidentReport,
  loadAccidentReport,
} from "@/app/(fleet-operations)/accidents/reports/_report-runtime";
import VehicleTable, {
  type VehicleTableColumn,
} from "@/app/(fleet-operations)/accidents/vehicle-table";
import {
  getAccidentPeriodReport,
  type AccidentPeriodReportRow,
  type AccidentPeriodReportStatus,
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

  const yearFirst = normalized.match(/^(\d{4})[/-](\d{1,2})[/-](\d{1,2})$/);
  if (yearFirst) {
    return `${yearFirst[1]}-${yearFirst[2].padStart(2, "0")}-${yearFirst[3].padStart(2, "0")}`;
  }

  const dayFirst = normalized.match(/^(\d{1,2})[/-](\d{1,2})[/-](\d{4})$/);
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
  if (value === null || value === "") {
    return "-";
  }

  return String(value);
}

function formatDate(value: string | null) {
  if (!value) {
    return "-";
  }

  const normalized = normalizeDate(value.slice(0, 10));
  return normalized || value;
}

const periodReportColumns: readonly VehicleTableColumn<AccidentPeriodReportRow>[] = [
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
    key: "accidentDescription",
    label: "Accident Description",
    render: (row) => valueOrDash(row.accidentDescription),
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
    key: "fileCloseDate",
    label: "File Close Date",
    render: (row) => formatDate(row.fileCloseDate),
  },
];

function periodReportRowKey(row: AccidentPeriodReportRow) {
  return JSON.stringify(row);
}

function ReportTable({
  rows,
  status,
}: {
  rows: AccidentPeriodReportRow[];
  status: AccidentPeriodReportStatus;
}) {
  const title = status === "closed" ? "Closed Accidents" : "Open Accidents";
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="accident-period-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="accident-period-results-title">{title}</h2>
        </div>
        <span className="form-hint">{rows.length} record(s)</span>
      </div>
      <div className="vehicle-table-wrapper">
        <VehicleTable
          caption={`${title} for the selected period`}
          columns={periodReportColumns}
          rows={rows}
          rowKey={periodReportRowKey}
        />
      </div>
      <p className="form-hint">Total Number: {rows.length}</p>
    </section>
  );
}

function PeriodReportForm({
  errorMessage,
  locationOptions,
  departmentNumber,
  startDate,
  endDate,
  status,
}: {
  errorMessage: string | null;
  locationOptions: readonly LocationOption[];
  departmentNumber: string;
  startDate: string;
  endDate: string;
  status: AccidentPeriodReportStatus;
}) {
  return (
    <>
      <AccidentReportFormError message={errorMessage} />
      <form className="vehicle-status-maintenance-panel" method="get">
        <div className="form-grid">
          <AccidentDepartmentSelect
            id="accident-period-department"
            value={departmentNumber}
            options={locationOptions}
          />
          <AccidentDateRangeFields
            startId="accident-period-start"
            endId="accident-period-end"
            startDate={startDate}
            endDate={endDate}
          />
          <fieldset className="vehicle-search-options">
            <legend>Accidents</legend>
            <label className="vehicle-checkbox-label">
              <input name="status" type="radio" value="open" defaultChecked={status === "open"} />{" "}
              Open
            </label>
            <label className="vehicle-checkbox-label">
              <input
                name="status"
                type="radio"
                value="closed"
                defaultChecked={status === "closed"}
              />{" "}
              Closed
            </label>
          </fieldset>
        </div>
        <AccidentReportFormActions submitLabel="SUBMIT" />
      </form>
    </>
  );
}

function PeriodReportResults({
  rows,
  status,
}: {
  rows: AccidentPeriodReportRow[] | null;
  status: AccidentPeriodReportStatus;
}) {
  return rows ? (
    rows.length > 0 ? (
      <ReportTable rows={rows} status={status} />
    ) : (
      <section className="vehicle-empty-state" aria-live="polite">
        <p className="eyebrow">No accidents found</p>
        <h2>No accidents matched the selected filters.</h2>
        <p className="muted-copy">
          Update the department, dates, or open/closed selection and submit again.
        </p>
      </section>
    )
  ) : null;
}

const PeriodReportContent = renderPeriodReportContent;

async function renderPeriodReportContent({ searchParams }: { searchParams: Promise<ReportQuery> }) {
  const authorization = await authorizeAccidentReport();
  if (authorization === "expired")
    return <SessionRecovery returnPath="/accidents/reports/period" />;
  if (authorization === "unavailable") {
    return (
      <AccidentReportErrorState
        title="The accident period report could not be loaded."
        retryHref="/accidents/reports/period"
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
    context: "FIS accident period department/site lookup failed",
    isUnauthorized: (error) =>
      (error instanceof DepartmentApiError && error.reason === "unauthorized") ||
      (error instanceof SiteApiError && error.reason === "unauthorized"),
  });
  if (locations.status === "unauthorized") {
    return <SessionRecovery returnPath="/accidents/reports/period" />;
  }
  if (locations.status !== "success") {
    return (
      <AccidentReportErrorState
        title="The accident period report could not be loaded."
        retryHref="/accidents/reports/period"
      />
    );
  }
  const locationOptions = locations.data;

  const query = await searchParams;
  const departmentNumber = (getQueryValue(query, "departmentNumber", "xdept") ?? "").trim();
  const startDate = normalizeDate(getQueryValue(query, "startDate", "BDAT") ?? "");
  const endDate = normalizeDate(getQueryValue(query, "endDate", "EDAT") ?? "");
  const rawStatus = (getQueryValue(query, "status") ?? "").trim().toLowerCase();
  const legacyStatus = (getQueryValue(query, "Radio2") ?? "").trim().toLowerCase();
  const status: AccidentPeriodReportStatus =
    rawStatus === "closed" || rawStatus === "close" || legacyStatus === "radio_close"
      ? "closed"
      : "open";
  const hasInvalidStatus = rawStatus.length > 0 && !["open", "closed", "close"].includes(rawStatus);
  const shouldRun =
    getQueryValue(query, "run") === "1" ||
    ["departmentNumber", "xdept", "startDate", "BDAT", "endDate", "EDAT", "status", "Radio2"].some(
      (key) => getQueryValue(query, key) !== undefined,
    );
  let errorMessage: string | null = hasInvalidStatus ? "Status must be open or closed." : null;
  if (shouldRun && (!startDate || !endDate || !isValidDate(startDate) || !isValidDate(endDate))) {
    errorMessage = "Enter a valid begin date and end date.";
  } else if (shouldRun && endDate < startDate) {
    errorMessage = "The begin date must be on or before the end date.";
  }

  const report = await loadAccidentReport({
    shouldRun,
    errorMessage,
    load: () => getAccidentPeriodReport(departmentNumber, startDate, endDate, status),
    context: "FIS accident period report failed",
  });
  if (report.status === "unauthorized") {
    return <SessionRecovery returnPath="/accidents/reports/period" />;
  }
  if (report.status === "error") {
    return (
      <AccidentReportErrorState
        title="The accident period report could not be loaded."
        retryHref="/accidents/reports/period"
      />
    );
  }
  const rows = report.data;

  return (
    <>
      <PeriodReportForm
        errorMessage={errorMessage}
        locationOptions={locationOptions}
        departmentNumber={departmentNumber}
        startDate={startDate}
        endDate={endDate}
        status={status}
      />
      <PeriodReportResults rows={rows} status={status} />
      <AccidentReportFooter clearHref="/accidents/reports/period" />
    </>
  );
}

export default function AccidentPeriodReportPage({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  return (
    <AccidentReportPageShell
      titleId="accident-period-title"
      title="Accident Report For A Period"
      description="Review open or closed accidents for a department/site and inclusive date period."
      fallback={<AccidentReportLoadingState />}
    >
      <Suspense fallback={<AccidentReportLoadingState />}>
        <PeriodReportContent searchParams={searchParams} />
      </Suspense>
    </AccidentReportPageShell>
  );
}
