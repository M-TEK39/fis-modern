import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { Suspense } from "react";
import {
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
  getAccidentAllReport,
  type AccidentAllReportDateMode,
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

function getMode(query: ReportQuery): { mode: AccidentAllReportDateMode; invalid: boolean } {
  const rawMode = (getQueryValue(query, "mode", "Radio1") ?? "").trim().toLowerCase();
  switch (rawMode) {
    case "2002-current":
    case "radionou":
    case "current":
      return { mode: "2002-current", invalid: false };
    case "1999-2001":
    case "radioou":
    case "middle":
      return { mode: "1999-2001", invalid: false };
    case "before-1999":
    case "radiobou":
    case "before":
      return { mode: "before-1999", invalid: false };
    case "":
      return { mode: "2002-current", invalid: false };
    default:
      return { mode: "2002-current", invalid: true };
  }
}

function valueOrDash(value: string | number | null) {
  return value === null || value === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) ?? "-";
}

function reportTitle(mode: AccidentAllReportDateMode) {
  switch (mode) {
    case "before-1999":
      return "Accidents Before 1999";
    case "1999-2001":
      return "Accidents From 1999 to 2001";
    default:
      return "Accidents From 2002 to Current";
  }
}

const allAccidentsColumns: readonly VehicleTableColumn<AccidentVehicleReportRow>[] = [
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
  {
    key: "description",
    label: "Accident Description",
    render: (row) => valueOrDash(row.description),
  },
  {
    key: "accidentTypeDescription",
    label: "Accident Category",
    render: (row) => valueOrDash(row.accidentTypeDescription),
  },
  { key: "tripAuthority", label: "Trip Auth", render: (row) => valueOrDash(row.tripAuthority) },
  { key: "driverName", label: "Driver Name", render: (row) => valueOrDash(row.driverName) },
  {
    key: "driverEmployNumber",
    label: "ID Number",
    render: (row) => valueOrDash(row.driverEmployNumber),
  },
  { key: "departmentNumber", label: "Site", render: (row) => valueOrDash(row.departmentNumber) },
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
  { key: "saReference", label: "SA Ref", render: (row) => valueOrDash(row.saReference) },
  { key: "caseNumber", label: "Case Num", render: (row) => valueOrDash(row.caseNumber) },
  { key: "costOfRepair", label: "GG Car Damage", render: (row) => valueOrDash(row.costOfRepair) },
  {
    key: "damageDescription",
    label: "GG Car Damage",
    render: (row) => valueOrDash(row.damageDescription),
  },
  {
    key: "thirdPartyRegistration",
    label: "Private Party Regno",
    render: (row) => valueOrDash(row.thirdPartyRegistration),
  },
];

function AllAccidentsReportTable({
  rows,
  mode,
}: {
  rows: AccidentVehicleReportRow[];
  mode: AccidentAllReportDateMode;
}) {
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="all-accident-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="all-accident-results-title">{reportTitle(mode)}</h2>
        </div>
        <span className="form-hint">{rows.length} record(s)</span>
      </div>
      <div className="vehicle-table-wrapper">
        <VehicleTable
          caption={reportTitle(mode)}
          columns={allAccidentsColumns}
          rows={rows}
          rowKey={(row) => row.accidentCode}
        />
      </div>
      <p className="vehicle-pagination-meta">Total Number: {rows.length}</p>
    </section>
  );
}

function AllAccidentsReportForm({
  mode,
  errorMessage,
}: {
  mode: AccidentAllReportDateMode;
  errorMessage: string | null;
}) {
  return (
    <>
      <AccidentReportFormError message={errorMessage} />
      <form className="vehicle-status-maintenance-panel" method="get">
        <fieldset className="vehicle-search-options">
          <legend>Accident Date</legend>
          <label className="vehicle-checkbox-label">
            <input
              name="mode"
              type="radio"
              value="2002-current"
              defaultChecked={mode === "2002-current"}
            />{" "}
            2002 - Current
          </label>
          <label className="vehicle-checkbox-label">
            <input
              name="mode"
              type="radio"
              value="1999-2001"
              defaultChecked={mode === "1999-2001"}
            />{" "}
            1999 - 2001
          </label>
          <label className="vehicle-checkbox-label">
            <input
              name="mode"
              type="radio"
              value="before-1999"
              defaultChecked={mode === "before-1999"}
            />{" "}
            Before 1999
          </label>
        </fieldset>
        <AccidentReportFormActions />
      </form>
    </>
  );
}

function AllAccidentsReportResults({
  rows,
  mode,
}: {
  rows: AccidentVehicleReportRow[] | null;
  mode: AccidentAllReportDateMode;
}) {
  return (
    <>
      {rows ? (
        rows.length > 0 ? (
          <AllAccidentsReportTable rows={rows} mode={mode} />
        ) : (
          <section className="vehicle-empty-state" aria-live="polite">
            <p className="eyebrow">No accidents found</p>
            <h2>No accidents matched the selected date range.</h2>
            <p className="muted-copy">Choose another date range and submit again.</p>
          </section>
        )
      ) : null}
    </>
  );
}

async function AllAccidentsReportContent({ searchParams }: { searchParams: Promise<ReportQuery> }) {
  const authorization = await authorizeAccidentReport();
  if (authorization === "expired") return <SessionRecovery returnPath="/accidents/reports/all" />;
  if (authorization === "unavailable") {
    return (
      <AccidentReportErrorState
        title="The all-accidents report could not be loaded."
        retryHref="/accidents/reports/all"
      />
    );
  }
  if (authorization === "forbidden") {
    return <AccidentReportAccessRestricted />;
  }

  const query = await searchParams;
  const { mode, invalid } = getMode(query);
  const shouldRun =
    getQueryValue(query, "run") === "1" || getQueryValue(query, "mode", "Radio1") !== undefined;
  const errorMessage = invalid ? "Choose a valid accident date range." : null;
  const report = await loadAccidentReport({
    shouldRun,
    errorMessage,
    load: () => getAccidentAllReport(mode),
    context: "FIS all accident report failed",
  });
  if (report.status === "unauthorized") {
    return <SessionRecovery returnPath="/accidents/reports/all" />;
  }
  if (report.status === "error") {
    return (
      <AccidentReportErrorState
        title="The all-accidents report could not be loaded."
        retryHref="/accidents/reports/all"
      />
    );
  }
  const rows = report.data;

  return (
    <>
      <AllAccidentsReportForm mode={mode} errorMessage={errorMessage} />
      <AllAccidentsReportResults rows={rows} mode={mode} />
      <AccidentReportFooter clearHref="/accidents/reports/all" />
    </>
  );
}

export default function AllAccidentsReportPage({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  return (
    <AccidentReportPageShell
      titleId="all-accident-title"
      title="All Accidents - All Detail"
      description="Review accident records by the date bands used in the legacy report."
      fallback={<AccidentReportLoadingState />}
    >
      <Suspense fallback={<AccidentReportLoadingState />}>
        <AllAccidentsReportContent searchParams={searchParams} />
      </Suspense>
    </AccidentReportPageShell>
  );
}
