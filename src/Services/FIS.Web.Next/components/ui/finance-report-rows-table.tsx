import type { ReactNode } from "react";

import type { FinanceRow } from "@/lib/api/finance/api-finance";

type FinanceReportRowsTableProps = Readonly<{
  columns: readonly string[];
  rows: readonly FinanceRow[];
  caption: string;
  formatValue: (row: FinanceRow, column: string) => ReactNode;
  rowKey: (row: FinanceRow) => string;
}>;

export default function FinanceReportRowsTable({
  columns,
  rows,
  caption,
  formatValue,
  rowKey,
}: FinanceReportRowsTableProps) {
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">{caption}</caption>
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
          {rows.map((row) => (
            <tr key={rowKey(row)}>
              {columns.map((column) => (
                <td key={column}>{formatValue(row, column)}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
