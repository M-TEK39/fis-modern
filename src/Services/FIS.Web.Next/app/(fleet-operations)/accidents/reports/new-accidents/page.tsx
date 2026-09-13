import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { Suspense } from "react";
import {
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
  getAccidentNewAccidentsReport,
  type AccidentNewAccidentReportMode,
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

function getMode(query: ReportQuery): { mode: AccidentNewAccidentReportMode; invalid: boolean } {
  const rawMode = (getQueryValue(query, "mode", "Radio1") ?? "").trim().toLowerCase();
  switch (rawMode) {
    case "call":
    case "radiocal":
      return { mode: "call", invalid: false };
    case "garage":
    case "radionew":
      return { mode: "garage", invalid: false };
    case "confirm":
    case "radiocon":
      return { mode: "confirm", invalid: false };
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
  return value?.slice(0, 10) ?? "-";
}

function formatTime(value: string | null) {
  if (!value) {
    return "-";
  }

  const timeStart = value.indexOf("T");
  return timeStart >= 0 ? value.slice(timeStart + 1, timeStart + 6) : value.slice(0, 5);
}

function reportTitle(mode: AccidentNewAccidentReportMode) {
  switch (mode) {
    case "call":
      return "New Accidents Reported by Call Centre";
    case "garage":
      return "New Accidents Reported to HQ by Garage";
    case "confirm":
      return "Confirmation of Receipt by HQ";
    default:
      return "Both New Accident Notifications";
  }
}

const newAccidentReportColumns: readonly VehicleTableColumn<AccidentVehicleReportRow>[] = [
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
  { key: "accidentTime", label: "Accid Time", render: (row) => formatTime(row.accidentTime) },
  { key: "accidentPlace", label: "Accid Place", render: (row) => valueOrDash(row.accidentPlace) },
  { key: "financialYear", label: "Fin Year", render: (row) => valueOrDash(row.financialYear) },
  { key: "dateUpdated", label: "Date Updated", render: (row) => formatDate(row.dateUpdated) },
  {
    key: "notifiedGarage",
    label: "Notify Garage",
    render: (row) => valueOrDash(row.notifiedGarage),
  },
  {
    key: "notifiedGarageDate",
    label: "Notify Garage Date",
    render: (row) => formatDate(row.notifiedGarageDate),
  },
  {
    key: "description",
    label: "Accident Description",
    render: (row) => valueOrDash(row.description),
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
  { key: "caseNumber", label: "Case Num", render: (row) => valueOrDash(row.caseNumber) },
  { key: "costOfRepair", label: "GG Car Damage", render: (row) => valueOrDash(row.costOfRepair) },
  {
    key: "damageDescription",
    label: "GG Car Damage",
    render: (row) => valueOrDash(row.damageDescription),
  },
  { key: "driverFault", label: "Driver Fault", render: (row) => valueOrDash(row.driverFault) },
  { key: "death", label: "Death", render: (row) => valueOrDash(row.death) },
  { key: "injured", label: "Injured", render: (row) => valueOrDash(row.injured) },
  {
    key: "thirdPartyRegistration",
    label: "Private Party Regno",
    render: (row) => valueOrDash(row.thirdPartyRegistration),
  },
  {
    key: "thirdPartyOwner",
    label: "Private Party",
    render: (row) => valueOrDash(row.thirdPartyOwner),
  },
  {
    key: "thirdPartyClaim",
    label: "Priv Car Damage",
    render: (row) => valueOrDash(row.thirdPartyClaim),
  },
  {
    key: "fileCloseDate",
    label: "File Close Date",
    render: (row) => formatDate(row.fileCloseDate),
  },
  { key: "notes", label: "Notes", render: (row) => valueOrDash(row.notes) },
];

function NewAccidentReportTable({
  query,
  report,
  mode,
}: {
  query: ReportQuery;
  report: AccidentReportPage<AccidentVehicleReportRow>;
  mode: AccidentNewAccidentReportMode;
}) {
  const rows = report.items;
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="new-accident-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="new-accident-results-title">{reportTitle(mode)}</h2>
        </div>
        <span className="form-hint">{report.total} record(s)</span>
      </div>
      <div className="vehicle-table-wrapper">
        <VehicleTable
          caption={reportTitle(mode)}
          columns={newAccidentReportColumns}
          rows={rows}
          rowKey={(row) => row.accidentCode}
        />
      </div>
      <p className="vehicle-pagination-meta">Total Number: {report.total}</p>
      <AccidentReportPagination
        page={report.page}
        pageSize={report.pageSize}
        pathname="/accidents/reports/new-accidents"
        query={query}
        total={report.total}
        totalPages={report.totalPages}
      />
    </section>
  );
}

function NewAccidentsReportForm({
  errorMessage,
  mode,
}: {
  errorMessage: string | null;
  mode: AccidentNewAccidentReportMode;
}) {
  return (
    <>
      <AccidentReportFormError message={errorMessage} />
      <form className="vehicle-status-maintenance-panel" method="get">
        <fieldset className="vehicle-search-options">
          <legend>Report on</legend>
          <label className="vehicle-checkbox-label">
            <input name="mode" type="radio" value="call" defaultChecked={mode === "call"} /> New
            accidents reported by Call Centre
          </label>
          <label className="vehicle-checkbox-label">
            <input name="mode" type="radio" value="garage" defaultChecked={mode === "garage"} /> New
            accidents reported to HQ by Garage
          </label>
          <label className="vehicle-checkbox-label">
            <input name="mode" type="radio" value="confirm" defaultChecked={mode === "confirm"} />{" "}
            Confirmation of receipt by HQ
          </label>
          <label className="vehicle-checkbox-label">
            <input name="mode" type="radio" value="all" defaultChecked={mode === "all"} /> Both
          </label>
        </fieldset>
        <AccidentReportFormActions />
      </form>
    </>
  );
}

function NewAccidentsReportResults({
  query,
  report,
  mode,
}: {
  query: ReportQuery;
  report: AccidentReportPage<AccidentVehicleReportRow> | null;
  mode: AccidentNewAccidentReportMode;
}) {
  if (report === null) {
    return null;
  }

  const rows = report.items;
  return rows.length > 0 ? (
    <NewAccidentReportTable query={query} report={report} mode={mode} />
  ) : (
    <section className="vehicle-empty-state" aria-live="polite">
      <p className="eyebrow">No accidents found</p>
      <h2>No accidents matched this notification status.</h2>
      <p className="muted-copy">Choose another report type and submit again.</p>
    </section>
  );
}

async function NewAccidentsReportContent({ searchParams }: { searchParams: Promise<ReportQuery> }) {
  const authorization = await authorizeAccidentReport();
  if (authorization === "expired")
    return <SessionRecovery returnPath="/accidents/reports/new-accidents" />;
  if (authorization === "unavailable") {
    return (
      <AccidentReportErrorState
        title="The new accident report could not be loaded."
        retryHref="/accidents/reports/new-accidents"
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
  const errorMessage = invalid ? "Mode must be all, call centre, garage, or confirmation." : null;
  const { page, pageSize } = getAccidentReportPageState(query);
  const report = await loadAccidentReport({
    shouldRun,
    errorMessage,
    load: () => getAccidentNewAccidentsReport(mode, page, pageSize),
    context: "FIS new accident report failed",
  });
  if (report.status === "unauthorized") {
    return <SessionRecovery returnPath="/accidents/reports/new-accidents" />;
  }
  if (report.status === "error") {
    return (
      <AccidentReportErrorState
        title="The new accident report could not be loaded."
        retryHref="/accidents/reports/new-accidents"
      />
    );
  }
  return (
    <>
      <NewAccidentsReportForm errorMessage={errorMessage} mode={mode} />
      <NewAccidentsReportResults query={query} report={report.data} mode={mode} />
      <AccidentReportFooter clearHref="/accidents/reports/new-accidents" />
    </>
  );
}

export default function NewAccidentsReportPage({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  return (
    <AccidentReportPageShell
      titleId="new-accident-title"
      title="Report on New Accidents"
      description="Review accident notifications by their legacy HQ status flag."
      fallback={<AccidentReportLoadingState />}
    >
      <Suspense fallback={<AccidentReportLoadingState />}>
        <NewAccidentsReportContent searchParams={searchParams} />
      </Suspense>
    </AccidentReportPageShell>
  );
}
