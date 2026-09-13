import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { Suspense } from "react";
import {
  AccidentGarageRadioOptions,
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
  getAccidentDepartmentMonthReport,
  type AccidentDepartmentMonthGarageMode,
  type AccidentDepartmentMonthPeriodMode,
  type AccidentReportPage,
  type AccidentVehicleReportRow,
} from "@/lib/api/fleet-operations/api-accidents";
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

function valueOrDash(value: string | number | null) {
  return value === null || value === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) ?? "-";
}

const departmentMonthColumns: readonly VehicleTableColumn<AccidentVehicleReportRow>[] = [
  {
    key: "registrationNumber",
    label: "Prov Reg Number",
    render: (row) => valueOrDash(row.registrationNumber),
  },
  { key: "fleetNumber", label: "GG Number", render: (row) => valueOrDash(row.fleetNumber) },
  {
    key: "locationDescription",
    label: "Garage",
    render: (row) => valueOrDash(row.locationDescription),
  },
  { key: "accidentDate", label: "Accid Date", render: (row) => formatDate(row.accidentDate) },
  { key: "accidentPlace", label: "Accid Place", render: (row) => valueOrDash(row.accidentPlace) },
  { key: "financialYear", label: "Fin Year", render: (row) => valueOrDash(row.financialYear) },
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

function DepartmentMonthReportTable({
  rows,
  total,
  mode,
}: {
  rows: AccidentVehicleReportRow[];
  total: number;
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
        <span className="form-hint">{total} record(s)</span>
      </div>
      <div className="vehicle-table-wrapper">
        <VehicleTable
          caption={`${reportTitle(mode)} for the selected garage and department/site`}
          columns={departmentMonthColumns}
          rows={rows}
          rowKey={(row) => row.accidentCode}
        />
      </div>
      <p className="vehicle-pagination-meta">Total Number: {total}</p>
    </section>
  );
}

function DepartmentMonthReportForm({
  errorMessage,
  garage,
  period,
  rawYear,
  rawMonth,
  departmentNumber,
}: {
  errorMessage: string | null;
  garage: string;
  period: AccidentDepartmentMonthPeriodMode;
  rawYear: string;
  rawMonth: string;
  departmentNumber: string;
}) {
  return (
    <>
      <AccidentReportFormError message={errorMessage} />
      <form className="vehicle-status-maintenance-panel" method="get">
        <AccidentGarageRadioOptions name="garage" mode={garage} />
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
        <AccidentReportFormActions />
      </form>
    </>
  );
}

function DepartmentMonthReportResults({
  query,
  report,
  period,
}: {
  query: ReportQuery;
  report: AccidentReportPage<AccidentVehicleReportRow> | null;
  period: AccidentDepartmentMonthPeriodMode;
}) {
  if (report === null) {
    return null;
  }

  const rows = report.items;
  return rows.length > 0 ? (
    <>
      <DepartmentMonthReportTable rows={rows} total={report.total} mode={period} />
      <AccidentReportPagination
        page={report.page}
        pageSize={report.pageSize}
        pathname="/accidents/reports/department-month"
        query={query}
        total={report.total}
        totalPages={report.totalPages}
      />
    </>
  ) : (
    <section className="vehicle-empty-state" aria-live="polite">
      <p className="eyebrow">No accidents found</p>
      <h2>No accidents matched the selected filters.</h2>
      <p className="muted-copy">
        Update the garage, period, year, month, or department and submit again.
      </p>
    </section>
  );
}

const DepartmentMonthReportContent = renderDepartmentMonthReportContent;

async function renderDepartmentMonthReportContent({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  const authorization = await authorizeAccidentReport();
  if (authorization === "expired")
    return <SessionRecovery returnPath="/accidents/reports/department-month" />;
  if (authorization === "unavailable") {
    return (
      <AccidentReportErrorState
        title="The department month report could not be loaded."
        retryHref="/accidents/reports/department-month"
      />
    );
  }
  if (authorization === "forbidden") {
    return <AccidentReportAccessRestricted />;
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

  const { page, pageSize } = getAccidentReportPageState(query);

  const report = await loadAccidentReport({
    shouldRun,
    errorMessage,
    load: () =>
      getAccidentDepartmentMonthReport(
        departmentNumber,
        garage,
        period,
        year,
        month,
        page,
        pageSize,
      ),
    context: "FIS accident department month report failed",
  });
  if (report.status === "unauthorized") {
    return <SessionRecovery returnPath="/accidents/reports/department-month" />;
  }
  if (report.status === "error") {
    return (
      <AccidentReportErrorState
        title="The department month report could not be loaded."
        retryHref="/accidents/reports/department-month"
      />
    );
  }
  return (
    <>
      <DepartmentMonthReportForm
        errorMessage={errorMessage}
        garage={garage}
        period={period}
        rawYear={rawYear}
        rawMonth={rawMonth}
        departmentNumber={departmentNumber}
      />
      <DepartmentMonthReportResults query={query} report={report.data} period={period} />
      <AccidentReportFooter clearHref="/accidents/reports/department-month" />
    </>
  );
}

export default function DepartmentMonthReportPage({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  return (
    <AccidentReportPageShell
      titleId="accident-department-month-title"
      title="Accident Report for a Department/Site, for a Month/Year"
      description="Review accident records by garage and calendar or legacy financial period."
      fallback={<AccidentReportLoadingState />}
    >
      <Suspense fallback={<AccidentReportLoadingState />}>
        <DepartmentMonthReportContent searchParams={searchParams} />
      </Suspense>
    </AccidentReportPageShell>
  );
}
