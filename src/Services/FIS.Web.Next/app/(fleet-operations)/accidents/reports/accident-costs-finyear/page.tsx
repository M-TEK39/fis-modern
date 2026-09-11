import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { Suspense } from "react";
import {
  AccidentReportAccessRestricted,
  AccidentReportErrorState,
  AccidentReportFooter,
  AccidentReportFormActions,
  AccidentReportFormError,
  AccidentReportPageShell,
  AccidentReportLoadingState,
} from "@/app/(fleet-operations)/accidents/reports/_report-components";
import {
  authorizeAccidentReport,
  loadAccidentReport,
} from "@/app/(fleet-operations)/accidents/reports/_report-runtime";
import VehicleTable, {
  type VehicleTableColumn,
} from "@/app/(fleet-operations)/accidents/vehicle-table";
import {
  getAccidentCostsFinancialYearReport,
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

function valueOrDash(value: string | number | null) {
  return value === null || value === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) ?? "-";
}

function formatAmount(value: number) {
  return value.toFixed(2);
}

const accidentCostsColumns: readonly VehicleTableColumn<AccidentVehicleReportRow>[] = [
  {
    key: "registrationNumber",
    label: "Prov Reg Number",
    render: (row) => valueOrDash(row.registrationNumber),
  },
  { key: "fleetNumber", label: "GG Number", render: (row) => valueOrDash(row.fleetNumber) },
  { key: "financialYear", label: "Fin Year", render: (row) => valueOrDash(row.financialYear) },
  { key: "accidentDate", label: "Accid Date", render: (row) => formatDate(row.accidentDate) },
  { key: "reportedDate", label: "Date Reported", render: (row) => formatDate(row.reportedDate) },
  { key: "siteDescription", label: "Dept/Site", render: (row) => valueOrDash(row.siteDescription) },
  { key: "hqReference", label: "HQ Ref", render: (row) => valueOrDash(row.hqReference) },
  { key: "ggReference", label: "GG Ref", render: (row) => valueOrDash(row.ggReference) },
  {
    key: "costOfRepair",
    label: "GG cost of Damages",
    render: (row) => valueOrDash(row.costOfRepair),
  },
  {
    key: "writeOffAmount",
    label: "Value Written Off",
    render: (row) => valueOrDash(row.writeOffAmount),
  },
  {
    key: "insuranceClaim",
    label: "Third Party Claim ?",
    render: (row) => valueOrDash(row.insuranceClaim),
  },
  {
    key: "thirdPartyClaim",
    label: "Third Party Damages",
    render: (row) => valueOrDash(row.thirdPartyClaim),
  },
  {
    key: "claimAgainstDepartment",
    label: "Third Party Claim Amount",
    render: (row) => valueOrDash(row.claimAgainstDepartment),
  },
  {
    key: "fileCloseDate",
    label: "File Close Date",
    render: (row) => formatDate(row.fileCloseDate),
  },
  { key: "notes", label: "Notes", render: (row) => valueOrDash(row.notes) },
];

function AccidentCostsReportTable({
  rows,
  financialYear,
}: {
  rows: AccidentVehicleReportRow[];
  financialYear: string;
}) {
  const totalRepairCost = rows.reduce((total, row) => total + (row.costOfRepair ?? 0), 0);

  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="accident-costs-results-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Report results</p>
          <h2 id="accident-costs-results-title">Accident Costs for {financialYear}</h2>
        </div>
        <span className="form-hint">{rows.length} record(s)</span>
      </div>
      <div className="vehicle-table-wrapper">
        <VehicleTable
          caption={`Accident costs report for financial year ${financialYear}`}
          columns={accidentCostsColumns}
          rows={rows}
          rowKey={(row) => row.accidentCode}
        />
      </div>
      <div className="vehicle-pagination-meta">
        <span>Total Number: {rows.length}</span>
        <span>Total GG Cost of damaged: R {formatAmount(totalRepairCost)}</span>
      </div>
    </section>
  );
}

function AccidentCostsReportForm({
  errorMessage,
  rawFinancialYear,
}: {
  errorMessage: string | null;
  rawFinancialYear: string;
}) {
  return (
    <>
      <AccidentReportFormError message={errorMessage} />
      <form className="vehicle-status-maintenance-panel" method="get">
        <div className="form-grid">
          <div className="field">
            <label htmlFor="accident-costs-finyear-year">Financial Year</label>
            <input
              id="accident-costs-finyear-year"
              name="financialYear"
              maxLength={5}
              placeholder="04/05"
              defaultValue={rawFinancialYear}
              required
            />
          </div>
        </div>
        <p className="form-hint">
          Enter the legacy five-character financial-year code, for example 04/05.
        </p>
        <AccidentReportFormActions />
      </form>
    </>
  );
}

function AccidentCostsReportResults({
  rows,
  financialYear,
}: {
  rows: AccidentVehicleReportRow[] | null;
  financialYear: string;
}) {
  return (
    <>
      {rows ? (
        rows.length > 0 ? (
          <AccidentCostsReportTable rows={rows} financialYear={financialYear} />
        ) : (
          <section className="vehicle-empty-state" aria-live="polite">
            <p className="eyebrow">No accidents found</p>
            <h2>No accident costs matched the selected financial year.</h2>
            <p className="muted-copy">Update the financial year and submit again.</p>
          </section>
        )
      ) : null}
    </>
  );
}

async function AccidentCostsReportContent({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  const authorization = await authorizeAccidentReport();
  if (authorization === "expired")
    return <SessionRecovery returnPath="/accidents/reports/accident-costs-finyear" />;
  if (authorization === "unavailable") {
    return (
      <AccidentReportErrorState
        title="The accident costs report could not be loaded."
        retryHref="/accidents/reports/accident-costs-finyear"
      />
    );
  }
  if (authorization === "forbidden") {
    return <AccidentReportAccessRestricted />;
  }

  const query = await searchParams;
  const rawFinancialYear = getQueryValue(query, "financialYear", "FINY") ?? "";
  const financialYear = rawFinancialYear.trim();
  const shouldRun =
    getQueryValue(query, "run") === "1" ||
    ["financialYear", "FINY"].some((key) => getQueryValue(query, key) !== undefined);
  const errorMessage =
    shouldRun && (financialYear.length === 0 || financialYear.length > 5)
      ? "Enter a financial year between 1 and 5 characters, for example 04/05."
      : null;

  const report = await loadAccidentReport({
    shouldRun,
    errorMessage,
    load: () => getAccidentCostsFinancialYearReport(financialYear),
    context: "FIS accident costs financial year report failed",
  });
  if (report.status === "unauthorized") {
    return <SessionRecovery returnPath="/accidents/reports/accident-costs-finyear" />;
  }
  if (report.status === "error") {
    return (
      <AccidentReportErrorState
        title="The accident costs report could not be loaded."
        retryHref="/accidents/reports/accident-costs-finyear"
      />
    );
  }
  const rows = report.data;

  return (
    <>
      <AccidentCostsReportForm errorMessage={errorMessage} rawFinancialYear={rawFinancialYear} />
      <AccidentCostsReportResults rows={rows} financialYear={financialYear} />
      <AccidentReportFooter clearHref="/accidents/reports/accident-costs-finyear" />
    </>
  );
}

export default function AccidentCostsFinancialYearReportPage({
  searchParams,
}: {
  searchParams: Promise<ReportQuery>;
}) {
  return (
    <AccidentReportPageShell
      titleId="accident-costs-finyear-title"
      title="Report on Accident Costs for a Financial Year"
      description="Review damage, claims, written-off values, and closure information for a financial year."
      fallback={<AccidentReportLoadingState />}
    >
      <Suspense fallback={<AccidentReportLoadingState />}>
        <AccidentCostsReportContent searchParams={searchParams} />
      </Suspense>
    </AccidentReportPageShell>
  );
}
