import Link from "next/link";

import ReportResultsPanel from "@/components/ui/report-results-panel";
import FinanceReportRowsTable from "@/components/ui/finance-report-rows-table";
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
  printOrientation,
}: Readonly<{
  report: FinanceReport;
  basePath: string;
  query: Query;
  page?: number;
  printOrientation?: "portrait" | "landscape";
}>) {
  const pageSize = 12;
  const columns = Array.from(new Set(report.rows.flatMap((row) => Object.keys(row))));
  const totalPages = Math.max(1, Math.ceil(report.rows.length / pageSize));
  const currentPage = Math.min(Math.max(page, 1), totalPages);
  const rows = report.rows.slice((currentPage - 1) * pageSize, currentPage * pageSize);
  const resolvedPrintOrientation =
    printOrientation ?? (columns.length > 6 ? "landscape" : "portrait");
  const columnsPerPrintPage = resolvedPrintOrientation === "landscape" ? 8 : 6;
  const printColumnGroups = Array.from(
    { length: Math.ceil(columns.length / columnsPerPrintPage) },
    (_, index) => columns.slice(index * columnsPerPrintPage, (index + 1) * columnsPerPrintPage),
  );
  if (report.rows.length === 0)
    return (
      <div className="vehicle-empty-state">
        <p>No report rows found for the selected parameters.</p>
      </div>
    );
  return (
    <ReportResultsPanel
      headingId="finance-report-results"
      eyebrow={`${report.rows.length} record(s)`}
      heading={report.title}
      letterheadTitle={report.title}
      widePrintLayout
      printOrientation={resolvedPrintOrientation}
    >
      <FinanceReportRowsTable
        columns={columns}
        rows={rows}
        caption={report.title}
        formatValue={value}
        rowKey={(row) =>
          `${report.title}-${columns.map((column) => String(row[column] ?? "")).join("|")}`
        }
      />
      <div className="report-print-table-groups">
        {printColumnGroups.map((group, groupIndex) => (
          <section className="report-print-table-group" key={`print-${groupIndex}`}>
            {printColumnGroups.length > 1 ? (
              <h2>
                {report.title} — fields {groupIndex * columnsPerPrintPage + 1}–
                {groupIndex * columnsPerPrintPage + group.length} of {columns.length}
              </h2>
            ) : null}
            <table className="report-print-table">
              <thead>
                <tr>
                  {group.map((column) => (
                    <th key={column} scope="col">
                      {column.replaceAll("_", " ")}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {report.rows.map((row, rowIndex) => (
                  <tr key={`${groupIndex}-${rowIndex}`}>
                    {group.map((column) => (
                      <td key={column}>{value(row, column)}</td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </section>
        ))}
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
    </ReportResultsPanel>
  );
}
