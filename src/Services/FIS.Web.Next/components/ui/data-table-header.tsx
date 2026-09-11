import type { ReactNode } from "react";

export type DataTableHeaderColumn = Readonly<{
  key: string;
  label: ReactNode;
}>;

export default function DataTableHeader({
  columns,
}: Readonly<{ columns: readonly DataTableHeaderColumn[] }>) {
  return (
    <thead>
      <tr>
        {columns.map((column) => (
          <th key={column.key} scope="col">
            {column.label}
          </th>
        ))}
      </tr>
    </thead>
  );
}
