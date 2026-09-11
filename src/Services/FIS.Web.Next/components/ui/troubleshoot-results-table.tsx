import type { TroubleshootLogEntry } from "@/lib/api/fleet-operations/api-troubleshoot";

import DataTableHeader from "@/components/ui/data-table-header";

function valueOrDash(value: string | null) {
  return value === null || value.trim() === "" ? "-" : value;
}

function dateValue(value: string | null) {
  return value?.slice(0, 10) || "-";
}

export default function TroubleshootResultsTable({
  rows,
  caption,
}: Readonly<{ rows: readonly TroubleshootLogEntry[]; caption: string }>) {
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">{caption}</caption>
        <DataTableHeader
          columns={[
            { key: "vehicle", label: "Vehicle" },
            { key: "description", label: "Description" },
            { key: "status", label: "Status" },
            { key: "logged-date", label: "Logged date" },
            { key: "logged-by", label: "Logged by" },
          ]}
        />
        <tbody>
          {rows.map((row) => (
            <tr key={row.id}>
              <td>{valueOrDash(row.vehicleIdentifier)}</td>
              <td>{valueOrDash(row.problemDescription)}</td>
              <td>{valueOrDash(row.status)}</td>
              <td>{dateValue(row.loggedDate)}</td>
              <td>{valueOrDash(row.loggedBy)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
