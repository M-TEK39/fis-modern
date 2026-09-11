import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { Suspense } from "react";
import {
  AccidentReportAccessRestricted,
  AccidentReportErrorState,
  AccidentReportFooter,
  AccidentReportFormActions,
  AccidentReportLoadingState,
  AccidentReportPageShell,
  AccidentVehicleSearchFieldsWithMode,
} from "@/app/(fleet-operations)/accidents/reports/_report-components";
import {
  authorizeAccidentReport,
  loadAccidentReport,
} from "@/app/(fleet-operations)/accidents/reports/_report-runtime";
import {
  getAccidentVehicleReport,
  type AccidentVehicleReportMode,
  type AccidentVehicleReportRow,
} from "@/lib/api/fleet-operations/api-accidents";
import VehicleReportResult from "@/app/(fleet-operations)/accidents/reports/vehicle-report-result";

type OneVehicleReportPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getMode(value: string | undefined): AccidentVehicleReportMode {
  return value === "fleet" || value === "gg" || value === "Radiogg" ? "fleet" : "registration";
}

function OneVehicleReportForm({
  mode,
  searchTerm,
}: {
  mode: AccidentVehicleReportMode;
  searchTerm: string;
}) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <AccidentVehicleSearchFieldsWithMode
        inputId="one-vehicle-report-search"
        mode={mode}
        searchTerm={searchTerm}
      />
      <AccidentReportFormActions submitLabel="SUBMIT" />
    </form>
  );
}

function OneVehicleReportResults({ rows }: { rows: AccidentVehicleReportRow[] | null }) {
  return rows !== null ? (
    rows.length === 0 ? (
      <div className="vehicle-empty-state">
        <p className="eyebrow">No vehicles found</p>
        <h2>No accidents matched this vehicle number.</h2>
        <p className="muted-copy">Try another GP or GG number.</p>
      </div>
    ) : (
      <section aria-live="polite" aria-labelledby="vehicle-report-results-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Report results</p>
            <h2 id="vehicle-report-results-title">Accidents found: {rows.length}</h2>
          </div>
        </div>
        {rows.map((row, index) => (
          <VehicleReportResult key={row.accidentCode} row={row} index={index} />
        ))}
      </section>
    )
  ) : null;
}

async function OneVehicleReportContent({ searchParams }: OneVehicleReportPageProps) {
  const authorization = await authorizeAccidentReport();
  if (authorization === "expired")
    return <SessionRecovery returnPath="/accidents/reports/one-vehicle" />;
  if (authorization === "unavailable") {
    return (
      <AccidentReportErrorState
        title="The vehicle accident report could not be loaded."
        retryHref="/accidents/reports/one-vehicle"
      />
    );
  }
  if (authorization === "forbidden") {
    return <AccidentReportAccessRestricted />;
  }

  const query = await searchParams;
  const mode = getMode(getQueryValue(query.mode) ?? getQueryValue(query.Radio1));
  const searchTerm = (getQueryValue(query.searchTerm) ?? getQueryValue(query.xnumber) ?? "").trim();
  const shouldRun = getQueryValue(query.run) === "1" || searchTerm.length > 0;
  const report = await loadAccidentReport({
    shouldRun: shouldRun && Boolean(searchTerm),
    errorMessage: null,
    load: () => getAccidentVehicleReport(searchTerm, mode),
    context: "FIS accident vehicle report failed",
  });
  if (report.status === "unauthorized") {
    return <SessionRecovery returnPath="/accidents/reports/one-vehicle" />;
  }
  if (report.status === "error") {
    return (
      <AccidentReportErrorState
        title="The vehicle accident report could not be loaded."
        retryHref="/accidents/reports/one-vehicle"
      />
    );
  }
  const rows = report.data;

  return (
    <>
      <OneVehicleReportForm mode={mode} searchTerm={searchTerm} />
      <OneVehicleReportResults rows={rows} />

      <AccidentReportFooter />
    </>
  );
}

export default function OneVehicleAccidentReportPage({ searchParams }: OneVehicleReportPageProps) {
  return (
    <AccidentReportPageShell
      titleId="one-vehicle-report-title"
      title="One Vehicle Accidents"
      description="Search for all accident details by an exact GP or GG number."
      fallback={<AccidentReportLoadingState />}
    >
      <Suspense fallback={<AccidentReportLoadingState />}>
        <OneVehicleReportContent searchParams={searchParams} />
      </Suspense>
    </AccidentReportPageShell>
  );
}
