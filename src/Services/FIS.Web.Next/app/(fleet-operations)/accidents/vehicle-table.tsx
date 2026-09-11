import type { Key, ReactNode } from "react";

import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

export type VehicleTableColumn<Row> = {
  key: string;
  label: string;
  render: (row: Row) => ReactNode;
};

type VehicleTableProps<Row> = {
  caption: string;
  columns: readonly VehicleTableColumn<Row>[];
  rows: readonly Row[];
  rowKey: (row: Row) => Key;
};

export default function VehicleTable<Row>({
  caption,
  columns,
  rows,
  rowKey,
}: Readonly<VehicleTableProps<Row>>) {
  return (
    <Table className="vehicle-table">
      <caption className="sr-only">{caption}</caption>
      <TableHeader>
        <TableRow>
          {columns.map((column) => (
            <TableHead key={column.key} scope="col">
              {column.label}
            </TableHead>
          ))}
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((row) => (
          <TableRow key={rowKey(row)}>
            {columns.map((column) => (
              <TableCell key={column.key}>{column.render(row)}</TableCell>
            ))}
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
