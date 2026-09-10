import Link from "next/link";

import type { FinanceRow } from "@/lib/api/finance/api-finance";
import type { FinanceReport } from "@/lib/api/finance/api-finance-reports";

type Query = Record<string, string | string[] | undefined>;

function value(row: FinanceRow, column: string) {
  const item = row[column];
  return item === null || item === undefined || item === "" ? "-" : String(item);
}

function pageHref(basePath: string, query: Query, page: number) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    const item = Array.isArray(value) ? value[0] : value;
    if (item !== undefined && key !== "page") params.set(key, item);
  }
  params.set("page", String(page));
  return `${basePath}?${params.toString()}`;
}

export function FinanceReportTable({
  report,
  basePath,
  query,
  page = 1,
}: Readonly<{ report: FinanceReport; basePath: string; query: Query; page?: number }>) {
  const pageSize = 12;
  const columns = Array.from(new Set(report.rows.flatMap((row) => Object.keys(row))));
  const totalPages = Math.max(1, Math.ceil(report.rows.length / pageSize));
  const currentPage = Math.min(Math.max(page, 1), totalPages);
  const rows = report.rows.slice((currentPage - 1) * pageSize, currentPage * pageSize);
  if (report.rows.length === 0)
    return (
      <div className="vehicle-empty-state">
        <p>No report rows found for the selected parameters.</p>
      </div>
    );
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="finance-report-results">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{report.rows.length} record(s)</p>
          <h2 id="finance-report-results">{report.title}</h2>
        </div>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">{report.title}</caption>
          <thead>
            <tr>
              {columns.map((column) => (
                <th key={column} scope="col">
                  {column.replaceAll("_", " ")}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row, index) => (
              <tr key={`${report.title}-${index}`}>
                {columns.map((column) => (
                  <td key={column}>{value(row, column)}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {totalPages > 1 ? (
        <nav className="vehicle-pagination" aria-label="Report result pages">
          {currentPage > 1 ? (
            <Link
              className="vehicle-pagination-button"
              href={pageHref(basePath, query, currentPage - 1)}
            >
              Previous
            </Link>
          ) : (
            <span className="vehicle-pagination-button vehicle-pagination-disabled">Previous</span>
          )}
          <span>
            Page {currentPage} of {totalPages}
          </span>
          {currentPage < totalPages ? (
            <Link
              className="vehicle-pagination-button"
              href={pageHref(basePath, query, currentPage + 1)}
            >
              Next
            </Link>
          ) : (
            <span className="vehicle-pagination-button vehicle-pagination-disabled">Next</span>
          )}
        </nav>
      ) : null}
    </section>
  );
}
