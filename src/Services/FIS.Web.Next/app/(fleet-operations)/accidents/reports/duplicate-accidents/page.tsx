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
  getAccidentDuplicateReport,
  type AccidentGarageReportMode,
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

function getMode(query: ReportQuery): { mode: AccidentGarageReportMode; invalid: boolean } {
  const rawMode = (getQueryValue(query, "garage", "mode", "Radio1") ?? "jhb").trim().toLowerCase();
  switch (rawMode) {
    case "jhb":
    case "radiojhb":
      return { mode: "jhb", invalid: false };
    case "pta":
    case "radiopta":
      return { mode: "pta", invalid: false };
    case "all":
    case "radioall":
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

function formatTime(value: string | null) {
  if (!value) {
    return "-";
  }

  const timeStart = value.indexOf("T");
  return timeStart >= 0 ? value.slice(timeStart + 1, timeStart + 6) : value.slice(0, 5);
}

function reportTitle(mode: AccidentGarageReportMode) {
  switch (mode) {
    case "jhb":
      return "Duplicate Accidents - JHB";
    case "pta":
      return "Duplicate Accidents - PTA";
    default:
      return "Duplicate Accidents - ALL";
  }
}

const duplicateAccidentsColumns: readonly VehicleTableColumn<AccidentVehicleReportRow>[] = [
  { key: "fleetNumber", label: "GG Number", render: (row) => valueOrDash(row.fleetNumber) },
  {
    key: "registrationNumber",
    label: "Prov Reg Number",
    render: (row) => valueOrDash(row.registrationNumber),
  },
  {
    key: "locationDescription",
    label: "Garage",
    render: (row) => valueOrDash(row.locationDescription),
  },
  { key: "accidentDate", label: "Accident Date", render: (row) => formatDate(row.accidentDate) },
  { key: "accidentTime", label: "Accident Time", render: (row) => formatTime(row.accidentTime) },
  {
    key: "accidentPlace",
    label: "Accident Place",
    render: (row) => valueOrDash(row.accidentPlace),
  },
  { key: "financialYear", label: "Fin year", render: (row) => valueOrDash(row.financialYear) },
  {
    key: "description",
    label: "Description of Accident",
    render: (row) => valueOrDash(row.description),
  },
  {
    key: "tripAuthority",
    label: "Trip Authority",
    render: (row) => valueOrDash(row.tripAuthority),
  },
  { key: "driverName", label: "Driver Name", render: (row) => valueOrDash(row.driverName) },
  {
    key: "driverEmployNumber",
    label: "Driver ID Number",
    render: (row) => valueOrDash(row.driverEmployNumber),
  },
  {
    key: "departmentNumber",
    label: "Driver Site",
    render: (row) => valueOrDash(row.departmentNumber),
  },
  {
    key: "transportOfficerName",
    label: "Transport Officer",
    render: (row) => valueOrDash(row.transportOfficerName),
  },
  {
    key: "transportOfficerTelephone",
    label: "Transport Officer Tel",
    render: (row) => valueOrDash(row.transportOfficerTelephone),
  },
  { key: "hqReference", label: "HQ Reference", render: (row) => valueOrDash(row.hqReference) },
  { key: "ggReference", label: "GG Reference", render: (row) => valueOrDash(row.ggReference) },
  { key: "caseNumber", label: "Case Number", render: (row) => valueOrDash(row.caseNumber) },
  {
    key: "costOfRepair",
    label: "GG Car Damage Amount",
    render: (row) => valueOrDash(row.costOfRepair),
  },
  {
    key: "damageDescription",
    label: "GG Car Damage Desc",
    render: (row) => valueOrDash(row.damageDescription),
  },
  { key: "death", label: "Death", render: (row) => valueOrDash(row.death) },
  { key: "injured", label: "Injured", render: (row) => valueOrDash(row.injured) },
  {
    key: "thirdPartyRegistration",
    label: "Private Party Regno",
    render: (row) => valueOrDash(row.thirdPartyRegistration),
  },
  {
    key: "thirdPartyOwner",
    label: "Third Party Owner",
    render: (row) => valueOrDash(row.thirdPartyOwner),
  },
  {
    key: "thirdPartyClaim",
    label: "Private Car Damage",
    render: (row) => valueOrDash(row.thirdPartyClaim),
  },
  {
    key: "claimAgainstDepartment",
    label: "Claim Against Dept",
    render: (row) => valueOrDash(row.claimAgainstDepartment),
  },
  { key: "notes", label: "Notes", render: (row) => valueOrDash(row.notes) },
];

function DuplicateAccidentsReportTable({
  query,
  report,
  mode,
}: {
  query: ReportQuery;
  report: AccidentReportPage<AccidentVehicleReportRow>;
  mode: AccidentGarageReportMode;
}) {
  const rows = report.items;
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="duplicate-accident-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="duplicate-accident-results-title">{reportTitle(mode)}</h2>
        </div>
        <span className="form-hint">{report.total} record(s)</span>
      </div>
      <div className="vehicle-table-wrapper">
        <VehicleTable
          caption={reportTitle(mode)}
          columns={duplicateAccidentsColumns}
          rows={rows}
          rowKey={(row) => row.accidentCode}
        />
      </div>
      <p className="vehicle-pagination-meta">Total Number: {report.total}</p>
      <AccidentReportPagination
        page={report.page}
        pageSize={report.pageSize}
        pathname="/accidents/reports/duplicate-accidents"
        query={query}
        total={report.total}
        totalPages={report.totalPages}
      />
    </section>
  );
}

function DuplicateAccidentsReportForm({
  errorMessage,
  mode,
}: {
  errorMessage: string | null;
  mode: AccidentGarageReportMode;
}) {
  return (
    <>
      <AccidentReportFormError message={errorMessage} />
      <form className="vehicle-status-maintenance-panel" method="get">
        <AccidentGarageRadioOptions name="garage" mode={mode} />
        <AccidentReportFormActions />
      </form>
    </>
  );
}

function DuplicateAccidentsReportResults({
  query,
  report,
  mode,
}: {
  query: ReportQuery;
  report: AccidentReportPage<AccidentVehicleReportRow> | null;
  mode: AccidentGarageReportMode;
}) {
  if (report === null) {
    return null;
  }

  const rows = report.items;
  return rows.length > 0 ? (
    <DuplicateAccidentsReportTable query={query} report={report} mode={mode} />
  ) : (
    <section className="vehicle-empty-state" aria-live="polite">
      <p className="eyebrow">No vehicles found</p>
      <h2>No duplicate accidents matched this garage selection.</h2>
      <p className="muted-copy">Choose another garage and submit again.</p>
    </section>
  );
}

async function DuplicateAccidentsReportContent({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  const authorization = await authorizeAccidentReport();
  if (authorization === "expired")
    return <SessionRecovery returnPath="/accidents/reports/duplicate-accidents" />;
  if (authorization === "unavailable") {
    return (
      <AccidentReportErrorState
        title="The duplicate accident report could not be loaded."
        retryHref="/accidents/reports/duplicate-accidents"
      />
    );
  }
  if (authorization === "forbidden") {
    return <AccidentReportAccessRestricted />;
  }

  const query = await searchParams;
  const { mode, invalid } = getMode(query);
  const shouldRun =
    getQueryValue(query, "run") === "1" ||
    getQueryValue(query, "garage", "mode", "Radio1") !== undefined;
  const errorMessage = invalid ? "Choose a valid garage for the duplicate report." : null;
  const { page, pageSize } = getAccidentReportPageState(query);
  const report = await loadAccidentReport({
    shouldRun,
    errorMessage,
    load: () => getAccidentDuplicateReport(mode, page, pageSize),
    context: "FIS duplicate accident report failed",
  });
  if (report.status === "unauthorized") {
    return <SessionRecovery returnPath="/accidents/reports/duplicate-accidents" />;
  }
  if (report.status === "error") {
    return (
      <AccidentReportErrorState
        title="The duplicate accident report could not be loaded."
        retryHref="/accidents/reports/duplicate-accidents"
      />
    );
  }
  return (
    <>
      <DuplicateAccidentsReportForm errorMessage={errorMessage} mode={mode} />
      <DuplicateAccidentsReportResults query={query} report={report.data} mode={mode} />
      <AccidentReportFooter clearHref="/accidents/reports/duplicate-accidents" />
    </>
  );
}

export default function DuplicateAccidentsReportPage({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  return (
    <AccidentReportPageShell
      titleId="duplicate-accident-title"
      title="REPORT ON ALL Duplicate ACCIDENT'S"
      description="Review duplicate accident records for JHB, PTA, or all garages."
      fallback={<AccidentReportLoadingState />}
    >
      <Suspense fallback={<AccidentReportLoadingState />}>
        <DuplicateAccidentsReportContent searchParams={searchParams} />
      </Suspense>
    </AccidentReportPageShell>
  );
}
