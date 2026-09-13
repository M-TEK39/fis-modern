import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { Suspense } from "react";
import {
  AccidentReportAccessRestricted,
  AccidentReportErrorState,
  AccidentReportFooter,
  AccidentReportFormActions,
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
  getAccidentDriverReport,
  type AccidentReportPage,
  type AccidentDriverReportMode,
  type AccidentDriverReportRow,
} from "@/lib/api/fleet-operations/api-accidents";
type DriverReportPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getMode(value: string | undefined): AccidentDriverReportMode {
  return value === "id" || value === "Radioid" ? "id" : "name";
}

function valueOrDash(value: string | number | null) {
  return value === null || value === "" ? "-" : String(value);
}

const driverReportColumns: readonly VehicleTableColumn<AccidentDriverReportRow>[] = [
  {
    key: "registrationNumber",
    label: "Registration Number",
    render: (row) => valueOrDash(row.registrationNumber),
  },
  { key: "fleetNumber", label: "Fleet Number", render: (row) => valueOrDash(row.fleetNumber) },
  { key: "driverName", label: "Driver Name", render: (row) => valueOrDash(row.driverName) },
  {
    key: "driverEmployNumber",
    label: "ID Number",
    render: (row) => valueOrDash(row.driverEmployNumber),
  },
  {
    key: "accidentDate",
    label: "Accident Date",
    render: (row) => row.accidentDate?.slice(0, 10) ?? "-",
  },
  {
    key: "departmentNumber",
    label: "Dept/Site Number",
    render: (row) => valueOrDash(row.departmentNumber),
  },
  {
    key: "siteDescription",
    label: "Department/Site Desciption",
    render: (row) => valueOrDash(row.siteDescription),
  },
  { key: "costOfRepair", label: "Damage Amount", render: (row) => valueOrDash(row.costOfRepair) },
];

function DriverReportForm({
  mode,
  searchTerm,
}: {
  mode: AccidentDriverReportMode;
  searchTerm: string;
}) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <fieldset className="vehicle-search-options">
        <legend>Search by</legend>
        <label className="vehicle-checkbox-label">
          <input type="radio" name="mode" value="name" defaultChecked={mode === "name"} /> Driver
          name
        </label>
        <label className="vehicle-checkbox-label">
          <input type="radio" name="mode" value="id" defaultChecked={mode === "id"} /> ID number
        </label>
      </fieldset>
      <div className="field">
        <label htmlFor="driver-report-search">
          {mode === "name" ? "Driver name" : "ID number"}
        </label>
        <input
          id="driver-report-search"
          name="searchTerm"
          maxLength={20}
          defaultValue={searchTerm}
          required
        />
      </div>
      <AccidentReportFormActions submitLabel="SUBMIT" />
    </form>
  );
}

function DriverReportResults({
  query,
  report,
}: {
  query: Record<string, string | string[] | undefined>;
  report: AccidentReportPage<AccidentDriverReportRow> | null;
}) {
  if (report === null) {
    return null;
  }

  const rows = report.items;
  if (rows.length === 0) {
    return (
      <div className="vehicle-empty-state">
        <p className="eyebrow">No accidents found</p>
        <h2>No accidents matched this driver search.</h2>
        <p className="muted-copy">Try a different driver name or ID number.</p>
      </div>
    );
  }

  return (
    <>
      <div className="vehicle-table-wrapper" aria-live="polite">
        <VehicleTable
          caption="Accident report by driver name or ID number"
          columns={driverReportColumns}
          rows={rows}
          rowKey={(row) =>
            [
              row.registrationNumber,
              row.fleetNumber,
              row.driverEmployNumber,
              row.accidentDate,
              row.departmentNumber,
            ].join("|")
          }
        />
      </div>
      <p className="vehicle-pagination-meta">Total Number: {report.total}</p>
      <AccidentReportPagination
        page={report.page}
        pageSize={report.pageSize}
        pathname="/accidents/reports/driver"
        query={query}
        total={report.total}
        totalPages={report.totalPages}
      />
    </>
  );
}

async function DriverReportContent({ searchParams }: DriverReportPageProps) {
  const authorization = await authorizeAccidentReport();
  if (authorization === "expired")
    return <SessionRecovery returnPath="/accidents/reports/driver" />;
  if (authorization === "unavailable") {
    return (
      <AccidentReportErrorState
        title="The driver report could not be loaded."
        retryHref="/accidents/reports/driver"
      />
    );
  }
  if (authorization === "forbidden") {
    return <AccidentReportAccessRestricted />;
  }

  const query = await searchParams;
  const mode = getMode(getQueryValue(query.mode) ?? getQueryValue(query.Radio1));
  const searchTerm = (
    getQueryValue(query.searchTerm) ??
    getQueryValue(query.txtDname) ??
    ""
  ).trim();
  const shouldRun = getQueryValue(query.run) === "1";
  const { page, pageSize } = getAccidentReportPageState(query);
  const report = await loadAccidentReport({
    shouldRun: shouldRun && Boolean(searchTerm),
    errorMessage: null,
    load: () => getAccidentDriverReport(searchTerm, mode, page, pageSize),
    context: "FIS accident driver report failed",
  });
  if (report.status === "unauthorized") {
    return <SessionRecovery returnPath="/accidents/reports/driver" />;
  }
  if (report.status === "error") {
    return (
      <AccidentReportErrorState
        title="The driver report could not be loaded."
        retryHref="/accidents/reports/driver"
      />
    );
  }
  return (
    <>
      <DriverReportForm mode={mode} searchTerm={searchTerm} />
      <DriverReportResults query={query} report={report.data} />

      <AccidentReportFooter />
    </>
  );
}

export default function AccidentDriverReportPage({ searchParams }: DriverReportPageProps) {
  return (
    <AccidentReportPageShell
      titleId="driver-report-title"
      title="Accident Report By Driver Name Or ID Number"
      description="Search by the beginning of a driver name or ID number."
      fallback={<AccidentReportLoadingState />}
    >
      <Suspense fallback={<AccidentReportLoadingState />}>
        <DriverReportContent searchParams={searchParams} />
      </Suspense>
    </AccidentReportPageShell>
  );
}
