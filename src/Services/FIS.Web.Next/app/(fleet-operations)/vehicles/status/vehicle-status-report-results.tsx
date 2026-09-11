import DataTableHeader from "@/components/ui/data-table-header";

import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationNext,
  PaginationPrevious,
} from "@/components/ui/pagination";
import type {
  VehicleStatusReport,
  VehicleStatusReportRow,
} from "@/app/(fleet-operations)/vehicles/status/status-types";

import {
  EXPORT_FIELDS,
  getMakeModel,
  getSiteLabel,
  getStatusLabel,
  type ExportField,
  valueOrDash,
} from "./vehicle-status-report-utils";

function VehicleStatusTable({
  report,
  onRemark,
}: Readonly<{
  report: VehicleStatusReport;
  onRemark: (row: VehicleStatusReportRow, resolve: boolean) => void;
}>) {
  return (
    <div className="table-wrapper">
      <table className="data-table">
        <DataTableHeader
          columns={[
            { key: "column-1", label: <>GG Number</> },
            { key: "column-2", label: <>Registration Number</> },
            { key: "column-3", label: <>Invoice Number</> },
            { key: "column-4", label: <>Make &amp; Model</> },
            { key: "column-5", label: <>Status</> },
            { key: "column-6", label: <>Site</> },
            { key: "column-7", label: <>Active Remark</> },
            { key: "column-8", label: <>Remark Category</> },
            { key: "column-9", label: <>Actions</> },
          ]}
        />
        <tbody>
          {report.rows.map((row) => (
            <tr key={row.vmfCode}>
              <td>{valueOrDash(row.fleetNumber)}</td>
              <td>{valueOrDash(row.registrationNumber)}</td>
              <td>{valueOrDash(row.invoiceNumber)}</td>
              <td>{getMakeModel(row)}</td>
              <td>{getStatusLabel(row)}</td>
              <td>{getSiteLabel(row)}</td>
              <td>{valueOrDash(row.remark?.text ?? null)}</td>
              <td>{valueOrDash(row.remark?.category ?? null)}</td>
              <td>
                <div className="table-actions">
                  <button
                    className="button button-secondary button-small"
                    type="button"
                    disabled={!report.remarksAvailable}
                    onClick={() => onRemark(row, false)}
                  >
                    Add Remark
                  </button>
                  {row.remark ? (
                    <button
                      className="button button-primary button-small"
                      type="button"
                      disabled={!report.remarksAvailable}
                      onClick={() => onRemark(row, true)}
                    >
                      Resolve
                    </button>
                  ) : null}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function CsvFieldSelector({
  selectedFields,
  onToggle,
}: Readonly<{
  selectedFields: Set<ExportField>;
  onToggle: (field: ExportField) => void;
}>) {
  return (
    <fieldset className="field-grid">
      <legend className="sr-only">CSV fields</legend>
      {EXPORT_FIELDS.map(([key, label]) => (
        <label className="vehicle-checkbox-label" key={key}>
          <input type="checkbox" checked={selectedFields.has(key)} onChange={() => onToggle(key)} />
          {label}
        </label>
      ))}
    </fieldset>
  );
}

function VehicleStatusPagination({
  page,
  pageSize,
  pending,
  totalPages,
  onPage,
}: Readonly<{
  page: number;
  pageSize: number;
  pending: boolean;
  totalPages: number;
  onPage: (page: number) => void;
}>) {
  if (totalPages <= 1) return null;
  return (
    <Pagination className="mt-4" aria-label="Vehicle status report pagination">
      <PaginationContent className="flex-wrap justify-center gap-2">
        <PaginationItem>
          <PaginationPrevious
            href="#"
            aria-disabled={pending || page <= 1}
            className={pending || page <= 1 ? "pointer-events-none opacity-50" : undefined}
            tabIndex={pending || page <= 1 ? -1 : undefined}
            onClick={(event) => {
              event.preventDefault();
              onPage(page - 1);
            }}
          />
        </PaginationItem>
        <PaginationItem>
          <span
            className="inline-flex h-9 items-center whitespace-nowrap px-2 text-sm font-medium text-muted-foreground"
            aria-live="polite"
          >
            Page {page} of {totalPages} ({pageSize} per page)
          </span>
        </PaginationItem>
        <PaginationItem>
          <PaginationNext
            href="#"
            aria-disabled={pending || page >= totalPages}
            className={pending || page >= totalPages ? "pointer-events-none opacity-50" : undefined}
            tabIndex={pending || page >= totalPages ? -1 : undefined}
            onClick={(event) => {
              event.preventDefault();
              onPage(page + 1);
            }}
          />
        </PaginationItem>
      </PaginationContent>
    </Pagination>
  );
}

export default function VehicleStatusReportResults({
  onDownload,
  onRemark,
  onPage,
  onToggleSelector,
  onToggleField,
  pending,
  report,
  selectedFields,
  showFieldSelector,
  visiblePage,
}: Readonly<{
  onDownload: () => void;
  onRemark: (row: VehicleStatusReportRow, resolve: boolean) => void;
  onPage: (page: number) => void;
  onToggleSelector: () => void;
  onToggleField: (field: ExportField) => void;
  pending: boolean;
  report: VehicleStatusReport;
  selectedFields: Set<ExportField>;
  showFieldSelector: boolean;
  visiblePage: number;
}>) {
  return (
    <section className="vehicle-form-section" aria-labelledby="vehicle-status-results-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Vehicle status report</p>
          <h2 id="vehicle-status-results-title">
            Showing {report.totalCount} vehicle{report.totalCount === 1 ? "" : "s"}
          </h2>
        </div>
        <div className="button-row">
          <button
            className="button button-secondary button-small"
            type="button"
            onClick={onToggleSelector}
            aria-expanded={showFieldSelector}
          >
            Select Fields
          </button>
          <button
            className="button button-secondary button-small"
            type="button"
            onClick={onDownload}
            disabled={selectedFields.size === 0}
          >
            Download current page CSV
          </button>
        </div>
      </div>
      {showFieldSelector ? (
        <CsvFieldSelector selectedFields={selectedFields} onToggle={onToggleField} />
      ) : null}
      <VehicleStatusTable report={report} onRemark={onRemark} />
      <VehicleStatusPagination
        page={visiblePage}
        pageSize={report.pageSize}
        pending={pending}
        totalPages={report.totalPages}
        onPage={onPage}
      />
    </section>
  );
}
