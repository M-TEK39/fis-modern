import type { ReactNode } from "react";

import DataTableHeader from "@/components/ui/data-table-header";

type ReportColumn = Readonly<{
  key: string;
  header: ReactNode;
}>;

type ReportRowsTableProps = Readonly<{
  columns: readonly ReportColumn[];
  rows: readonly Record<string, string | null>[];
  caption: string;
}>;

function valueOrDash(value: string | null | undefined) {
  return value === null || value === undefined || value === "" ? "-" : value;
}

export default function ReportRowsTable({ columns, rows, caption }: ReportRowsTableProps) {
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">{caption}</caption>
        <DataTableHeader
          columns={columns.map((column) => ({ key: column.key, label: column.header }))}
        />
        <tbody>
          {rows.map((row, index) => (
            <tr key={columns.map((column) => String(row[column.key] ?? index)).join("|")}>
              {columns.map((column) => (
                <td key={column.key}>{valueOrDash(row[column.key])}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
