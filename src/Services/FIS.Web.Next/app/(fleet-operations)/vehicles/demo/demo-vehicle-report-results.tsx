import DataTableHeader from "@/components/ui/data-table-header";

import type { DemoVehicleReportState } from "./use-demo-vehicle-report";

function valueOrDash(value: string | number | null) {
  return value === null || value === "" ? "-" : String(value);
}

export default function DemoVehicleReportResults({
  loaded,
  rows,
  page,
  total,
  totalPages,
  error,
  isPending,
  loadReport,
}: DemoVehicleReportState & {
  isPending: boolean;
  loadReport: (requestedPage: number) => void;
}) {
  if (error)
    return (
      <div className="notice notice-error" role="alert">
        {error}
      </div>
    );

  if (loaded && total === 0)
    return (
      <div className="vehicle-empty-state">
        <p className="eyebrow">No demo vehicles found</p>
        <p>No demo vehicles are available for this report.</p>
      </div>
    );

  if (rows.length === 0) return null;

  return (
    <>
      <div className="vehicle-overview-header">
        <p className="muted-copy" aria-live="polite">
          {total} demo vehicle{total === 1 ? "" : "s"}
        </p>
      </div>
      <div className="vehicle-table-wrapper" aria-busy={isPending}>
        <table className="vehicle-table">
          <caption className="sr-only">All demo vehicles</caption>
          <DataTableHeader
            columns={[
              { key: "column-1", label: <>GG Number</> },
              { key: "column-2", label: <>Reg Number</> },
              { key: "column-3", label: <>Description</> },
              { key: "column-4", label: <>Year Model</> },
              { key: "column-5", label: <>Site</> },
              { key: "column-6", label: <>Bank code</> },
              { key: "column-7", label: <>Tank</> },
              { key: "column-8", label: <>Engine Number</> },
              { key: "column-9", label: <>Chassis Number</> },
            ]}
          />
          <tbody>
            {rows.map((row) => (
              <tr key={row.demoVehicleCode}>
                <td>{valueOrDash(row.ggNumber)}</td>
                <td>{valueOrDash(row.registrationNumber)}</td>
                <td>{valueOrDash(row.modelDescription)}</td>
                <td>{valueOrDash(row.yearManufactured)}</td>
                <td>{valueOrDash(row.siteDescription)}</td>
                <td>{valueOrDash(row.bankCode)}</td>
                <td>{valueOrDash(row.tank)}</td>
                <td>{valueOrDash(row.engineNumber)}</td>
                <td>{valueOrDash(row.chassisNumber)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {totalPages > 1 ? (
        <nav className="vehicle-pagination" aria-label="Demo vehicle report pagination">
          <button
            className={`vehicle-pagination-button${page <= 1 || isPending ? " vehicle-pagination-disabled" : ""}`}
            type="button"
            onClick={() => loadReport(page - 1)}
            disabled={isPending || page <= 1}
          >
            Previous
          </button>
          <span className="vehicle-pagination-meta" aria-live="polite">
            Page {page} of {totalPages}
          </span>
          <button
            className={`vehicle-pagination-button${page >= totalPages || isPending ? " vehicle-pagination-disabled" : ""}`}
            type="button"
            onClick={() => loadReport(page + 1)}
            disabled={isPending || page >= totalPages}
          >
            Next
          </button>
        </nav>
      ) : null}
    </>
  );
}
