import Link from "next/link";

import {
  AccidentReportAccessRestricted,
  AccidentReportErrorState,
  AccidentReportFooter,
  AccidentReportFormActions,
  AccidentReportFormError,
  AccidentReportLoadingState,
  AccidentReportPagination,
  AccidentReportPageShell,
  AccidentVehicleSearchFieldsWithMode,
} from "@/app/(fleet-operations)/accidents/reports/_report-components";
import {
  authorizeAccidentReport,
  getAccidentReportPageState,
  loadAccidentReport,
} from "@/app/(fleet-operations)/accidents/reports/_report-runtime";
import InspectionLetter from "@/app/(fleet-operations)/accidents/reports/inspection/_letter";
import VehicleTable, {
  type VehicleTableColumn,
} from "@/app/(fleet-operations)/accidents/vehicle-table";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  AccidentApiError,
  getAccidentInspectionLetterLookup,
  getAccidentInspectionLetterReport,
  type AccidentReportPage,
  type AccidentOutstandingDocumentLookupRow,
  type AccidentVehicleReportMode,
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

function getMode(value: string | undefined): AccidentVehicleReportMode {
  const normalized = value?.trim().toLowerCase();
  return normalized === "fleet" || normalized === "gg" || normalized === "radiogg"
    ? "fleet"
    : "registration";
}

function positiveInteger(value: string | undefined) {
  return value && /^\d+$/.test(value) && Number(value) > 0 ? Number(value) : null;
}

function valueOrDash(value: string | null) {
  return value?.trim() || "-";
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function getInspectionLookupColumns(
  mode: AccidentVehicleReportMode,
): readonly VehicleTableColumn<AccidentOutstandingDocumentLookupRow>[] {
  return [
    {
      key: "vehicleNumber",
      label: "Vehicle Number",
      render: (row) => valueOrDash(mode === "fleet" ? row.fleetNumber : row.registrationNumber),
    },
    {
      key: "ggReference",
      label: "Refer Number (GMT Number)",
      render: (row) => valueOrDash(row.ggReference),
    },
    { key: "accidentDate", label: "Accident Date", render: (row) => formatDate(row.accidentDate) },
    {
      key: "action",
      label: "Action",
      render: (row) => (
        <Link
          className="button button-secondary button-small"
          href={`/accidents/reports/inspection/letter?accidentCode=${encodeURIComponent(row.accidentCode)}`}
        >
          Report
        </Link>
      ),
    },
  ];
}

export const InspectionLoadingState = AccidentReportLoadingState;

function LookupTable({
  query,
  report,
  rows,
  mode,
}: {
  query: ReportQuery;
  report: AccidentReportPage<AccidentOutstandingDocumentLookupRow>;
  rows: AccidentOutstandingDocumentLookupRow[];
  mode: AccidentVehicleReportMode;
}) {
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-live="polite"
      aria-labelledby="inspection-letter-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="inspection-letter-results-title">Accidents found</h2>
        </div>
        <span className="form-hint">{report.total} record(s)</span>
      </div>
      <AccidentReportPagination
        page={report.page}
        pageSize={report.pageSize}
        pathname="/accidents/reports/inspection"
        query={query}
        total={report.total}
        totalPages={report.totalPages}
      />
      <div className="vehicle-table-wrapper">
        <VehicleTable
          caption="Accidents found for the selected vehicle number"
          columns={getInspectionLookupColumns(mode)}
          rows={rows}
          rowKey={(row) => row.accidentCode}
        />
      </div>
    </section>
  );
}

function InspectionLookupForm({
  errorMessage,
  mode,
  searchTerm,
}: {
  errorMessage: string | null;
  mode: AccidentVehicleReportMode;
  searchTerm: string;
}) {
  return (
    <>
      <AccidentReportFormError message={errorMessage} />
      <form className="vehicle-status-maintenance-panel" method="get">
        <AccidentVehicleSearchFieldsWithMode
          inputId="inspection-letter-search"
          mode={mode}
          searchTerm={searchTerm}
        />
        <AccidentReportFormActions />
      </form>
    </>
  );
}

function InspectionLookupResults({
  query,
  report,
  mode,
}: {
  query: ReportQuery;
  report: AccidentReportPage<AccidentOutstandingDocumentLookupRow> | null;
  mode: AccidentVehicleReportMode;
}) {
  if (report === null) {
    return null;
  }

  const rows = report.items;
  return rows.length > 0 ? (
    <LookupTable query={query} report={report} rows={rows} mode={mode} />
  ) : (
    <section className="vehicle-empty-state" aria-live="polite">
      <p className="eyebrow">Vehicle not found</p>
      <h2>This vehicle number does not exist.</h2>
      <p className="muted-copy">Try another GP or GG number.</p>
    </section>
  );
}

async function InspectionLookupContent({ searchParams }: { searchParams: Promise<ReportQuery> }) {
  const authorization = await authorizeAccidentReport();
  if (authorization === "expired")
    return <SessionRecovery returnPath="/accidents/reports/inspection" />;
  if (authorization === "unavailable") {
    return (
      <AccidentReportErrorState
        title="The inspection letter lookup could not be loaded."
        retryHref="/accidents/reports/inspection"
      />
    );
  }
  if (authorization === "forbidden") {
    return <AccidentReportAccessRestricted />;
  }

  const query = await searchParams;
  const rawMode = getQueryValue(query, "mode", "Radio1");
  const mode = getMode(rawMode);
  const searchTerm = (getQueryValue(query, "searchTerm", "xggnum") ?? "").trim();
  const shouldRun =
    getQueryValue(query, "run") === "1" ||
    searchTerm.length > 0 ||
    getQueryValue(query, "mode", "Radio1") !== undefined;
  const errorMessage =
    shouldRun && searchTerm.length === 0
      ? "Enter a GP or GG number."
      : searchTerm.length > 8
        ? "Vehicle numbers can contain no more than 8 characters."
        : null;
  const { page, pageSize } = getAccidentReportPageState(query);

  const report = await loadAccidentReport({
    shouldRun,
    errorMessage,
    load: () => getAccidentInspectionLetterLookup(searchTerm, mode, page, pageSize),
    context: "FIS inspection letter lookup failed",
  });
  if (report.status === "unauthorized") {
    return <SessionRecovery returnPath="/accidents/reports/inspection" />;
  }
  if (report.status === "error") {
    return (
      <AccidentReportErrorState
        title="The inspection letter lookup could not be loaded."
        retryHref="/accidents/reports/inspection"
      />
    );
  }
  return (
    <>
      <InspectionLookupForm errorMessage={errorMessage} mode={mode} searchTerm={searchTerm} />
      <InspectionLookupResults query={query} report={report.data} mode={mode} />
      <AccidentReportFooter clearHref="/accidents/reports/inspection" />
    </>
  );
}

export async function InspectionLetterContent({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  const authorization = await authorizeAccidentReport();
  if (authorization === "expired")
    return <SessionRecovery returnPath="/accidents/reports/inspection/letter" />;
  if (authorization === "unavailable") {
    return (
      <AccidentReportErrorState
        title="The inspection letter could not be loaded."
        retryHref="/accidents/reports/inspection"
        description={null}
      />
    );
  }
  if (authorization === "forbidden") {
    return (
      <AccidentReportAccessRestricted message="You do not have permission to print inspection letters." />
    );
  }

  const query = await searchParams;
  const accidentCode = positiveInteger(getQueryValue(query, "accidentCode", "Code", "code"));
  if (!accidentCode)
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Accident not selected</p>
        <h2>Choose an accident from the inspection letter lookup.</h2>
        <Link className="button button-secondary" href="/accidents/reports/inspection">
          Inspection letter lookup
        </Link>
      </section>
    );

  try {
    const report = await getAccidentInspectionLetterReport(accidentCode);
    return <InspectionLetter report={report} />;
  } catch (error) {
    if (error instanceof AccidentApiError && error.reason === "unauthorized")
      return (
        <SessionRecovery
          returnPath={`/accidents/reports/inspection/letter?accidentCode=${accidentCode}`}
        />
      );
    const notFound = error instanceof AccidentApiError && error.reason === "not-found";
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">{notFound ? "Record not found" : "Report unavailable"}</p>
        <h2>
          {notFound
            ? `Accident #${accidentCode} could not be found.`
            : "The inspection letter could not be loaded."}
        </h2>
        <p className="muted-copy">Return to the lookup and choose another accident.</p>
        <Link className="button button-secondary" href="/accidents/reports/inspection">
          Inspection letter lookup
        </Link>
      </section>
    );
  }
}

export default function InspectionLetterLookupPage({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  return (
    <AccidentReportPageShell
      titleId="inspection-letter-page-title"
      title="LETTER for Inspection"
      description="Find an accident by GP or GG number and print the inspection letter."
      fallback={<AccidentReportLoadingState />}
    >
      <InspectionLookupContent searchParams={searchParams} />
    </AccidentReportPageShell>
  );
}
