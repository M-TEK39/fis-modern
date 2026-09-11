import Link from "next/link";
import { Suspense } from "react";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  AccidentReportAccessRestricted,
  AccidentReportErrorState,
  AccidentReportFooter,
  AccidentReportFormActions,
  AccidentReportFormError,
  AccidentReportLoadingState,
  AccidentReportPageShell,
  AccidentVehicleSearchFieldsWithMode,
} from "@/app/(fleet-operations)/accidents/reports/_report-components";
import {
  authorizeAccidentReport,
  loadAccidentReport,
} from "@/app/(fleet-operations)/accidents/reports/_report-runtime";
import VehicleTable, {
  type VehicleTableColumn,
} from "@/app/(fleet-operations)/accidents/vehicle-table";
import {
  getAccidentOutstandingDocumentLookup,
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

function valueOrDash(value: string | null) {
  return value?.trim() || "-";
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function getOutstandingDocumentsColumns(
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
          href={`/accidents/reports/outstanding-docs/letter?accidentCode=${encodeURIComponent(row.accidentCode)}`}
        >
          Report
        </Link>
      ),
    },
  ];
}

function LookupTable({
  rows,
  mode,
}: {
  rows: AccidentOutstandingDocumentLookupRow[];
  mode: AccidentVehicleReportMode;
}) {
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-live="polite"
      aria-labelledby="outstanding-documents-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="outstanding-documents-results-title">Accidents found</h2>
        </div>
        <span className="form-hint">{rows.length} record(s)</span>
      </div>
      <div className="vehicle-table-wrapper">
        <VehicleTable
          caption="Accidents found for the selected vehicle number"
          columns={getOutstandingDocumentsColumns(mode)}
          rows={rows}
          rowKey={(row) => row.accidentCode}
        />
      </div>
    </section>
  );
}

function OutstandingDocumentsForm({
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
          inputId="outstanding-documents-search"
          mode={mode}
          searchTerm={searchTerm}
        />
        <AccidentReportFormActions />
      </form>
    </>
  );
}

function OutstandingDocumentsResults({
  rows,
  mode,
}: {
  rows: AccidentOutstandingDocumentLookupRow[] | null;
  mode: AccidentVehicleReportMode;
}) {
  return rows !== null ? (
    rows.length > 0 ? (
      <LookupTable rows={rows} mode={mode} />
    ) : (
      <section className="vehicle-empty-state" aria-live="polite">
        <p className="eyebrow">Vehicle not found</p>
        <h2>This vehicle number does not exist.</h2>
        <p className="muted-copy">Try another GP or GG number.</p>
      </section>
    )
  ) : null;
}

async function OutstandingDocumentsContent({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  const authorization = await authorizeAccidentReport();
  if (authorization === "expired")
    return <SessionRecovery returnPath="/accidents/reports/outstanding-docs" />;
  if (authorization === "unavailable") {
    return (
      <AccidentReportErrorState
        title="The outstanding document lookup could not be loaded."
        retryHref="/accidents/reports/outstanding-docs"
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

  const report = await loadAccidentReport({
    shouldRun,
    errorMessage,
    load: () => getAccidentOutstandingDocumentLookup(searchTerm, mode),
    context: "FIS outstanding accident document lookup failed",
  });
  if (report.status === "unauthorized") {
    return <SessionRecovery returnPath="/accidents/reports/outstanding-docs" />;
  }
  if (report.status === "error") {
    return (
      <AccidentReportErrorState
        title="The outstanding document lookup could not be loaded."
        retryHref="/accidents/reports/outstanding-docs"
      />
    );
  }
  const rows = report.data;

  return (
    <>
      <OutstandingDocumentsForm errorMessage={errorMessage} mode={mode} searchTerm={searchTerm} />
      <OutstandingDocumentsResults rows={rows} mode={mode} />
      <AccidentReportFooter clearHref="/accidents/reports/outstanding-docs" />
    </>
  );
}

export default function OutstandingDocumentsPage({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  return (
    <AccidentReportPageShell
      titleId="outstanding-documents-title"
      title="LETTER for Outstanding Accident documents"
      description="Find an accident by GP or GG number and print the outstanding-document letter."
      fallback={<AccidentReportLoadingState />}
    >
      <Suspense fallback={<AccidentReportLoadingState />}>
        <OutstandingDocumentsContent searchParams={searchParams} />
      </Suspense>
    </AccidentReportPageShell>
  );
}
