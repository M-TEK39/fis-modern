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
  getAccidentDepartmentFinancialYearReport,
  type AccidentDepartmentFinancialYearGarageMode,
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

function valueOrDash(value: string | number | null) {
  return value === null || value === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) ?? "-";
}

function formatAmount(value: number) {
  return value.toFixed(2);
}

const departmentFinancialYearColumns: readonly VehicleTableColumn<AccidentVehicleReportRow>[] = [
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
  { key: "financialYear", label: "Fin Year", render: (row) => valueOrDash(row.financialYear) },
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

function DepartmentFinancialYearReportTable({
  rows,
  total,
  financialYear,
}: {
  rows: AccidentVehicleReportRow[];
  total: number;
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
        <span className="form-hint">{total} record(s)</span>
      </div>
      <div className="vehicle-table-wrapper">
        <VehicleTable
          caption={`Department or site accident report for book or financial year ${financialYear}`}
          columns={departmentFinancialYearColumns}
          rows={rows}
          rowKey={(row) => row.accidentCode}
        />
      </div>
      <div className="vehicle-pagination-meta">
        <span>Total Number: {total}</span>
        <span>Total Cost of Repairs: R {formatAmount(totalRepairCost)}</span>
      </div>
    </section>
  );
}

function DepartmentFinancialYearReportForm({
  garage,
  rawFinancialYear,
  departmentNumber,
  errorMessage,
}: {
  garage: string;
  rawFinancialYear: string;
  departmentNumber: string;
  errorMessage: string | null;
}) {
  return (
    <>
      <AccidentReportFormError message={errorMessage} />
      <form className="vehicle-status-maintenance-panel" method="get">
        <AccidentGarageRadioOptions name="garage" mode={garage} />
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
        <AccidentReportFormActions />
      </form>
    </>
  );
}

function DepartmentFinancialYearReportResults({
  query,
  report,
  financialYear,
}: {
  query: ReportQuery;
  report: AccidentReportPage<AccidentVehicleReportRow> | null;
  financialYear: string;
}) {
  if (report === null) {
    return null;
  }

  const rows = report.items;
  return rows.length > 0 ? (
    <>
      <DepartmentFinancialYearReportTable
        rows={rows}
        total={report.total}
        financialYear={financialYear}
      />
      <AccidentReportPagination
        page={report.page}
        pageSize={report.pageSize}
        pathname="/accidents/reports/department-finyear"
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
        Update the garage, book / financial year, or department/site and submit again.
      </p>
    </section>
  );
}

async function DepartmentFinancialYearReportContent({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  const authorization = await authorizeAccidentReport();
  if (authorization === "expired")
    return <SessionRecovery returnPath="/accidents/reports/department-finyear" />;
  if (authorization === "unavailable") {
    return (
      <AccidentReportErrorState
        title="The department financial year report could not be loaded."
        retryHref="/accidents/reports/department-finyear"
      />
    );
  }
  if (authorization === "forbidden") {
    return <AccidentReportAccessRestricted />;
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
  const { page, pageSize } = getAccidentReportPageState(query);

  const report = await loadAccidentReport({
    shouldRun,
    errorMessage,
    load: () =>
      getAccidentDepartmentFinancialYearReport(
        departmentNumber,
        garage,
        financialYear,
        page,
        pageSize,
      ),
    context: "FIS accident department financial year report failed",
  });
  if (report.status === "unauthorized") {
    return <SessionRecovery returnPath="/accidents/reports/department-finyear" />;
  }
  if (report.status === "error") {
    return (
      <AccidentReportErrorState
        title="The department financial year report could not be loaded."
        retryHref="/accidents/reports/department-finyear"
      />
    );
  }
  return (
    <>
      <DepartmentFinancialYearReportForm
        garage={garage}
        rawFinancialYear={rawFinancialYear}
        departmentNumber={departmentNumber}
        errorMessage={errorMessage}
      />
      <DepartmentFinancialYearReportResults
        query={query}
        report={report.data}
        financialYear={financialYear}
      />
      <AccidentReportFooter clearHref="/accidents/reports/department-finyear" />
    </>
  );
}

export default function DepartmentFinancialYearReportPage({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  return (
    <AccidentReportPageShell
      titleId="accident-department-finyear-title"
      title="Accident Report for a Department/Site, for a Book / Financial Year"
      description="Review accident records by garage, book / financial year, and department or site."
      fallback={<AccidentReportLoadingState />}
    >
      <Suspense fallback={<AccidentReportLoadingState />}>
        <DepartmentFinancialYearReportContent searchParams={searchParams} />
      </Suspense>
    </AccidentReportPageShell>
  );
}
