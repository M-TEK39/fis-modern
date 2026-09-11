import DataTableHeader from "@/components/ui/data-table-header";

import type { VehicleStatusVehicle } from "@/lib/api/vehicles/api-vehicle-status";

import { formatDate, formatDateTime, valueOrDash } from "./status-maintenance-utils";

export default function StatusMaintenanceVehicleHistory({
  vehicle,
}: Readonly<{ vehicle: VehicleStatusVehicle }>) {
  return (
    <section className="status-maintenance-panel" aria-labelledby="vehicle-history-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Status record</p>
          <h2 id="vehicle-history-title">Vehicle Status History</h2>
        </div>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table status-maintenance-table">
          <DataTableHeader
            columns={[
              { key: "column-1", label: <>Status</> },
              { key: "column-2", label: <>Start Date</> },
              { key: "column-3", label: <>End Date</> },
              { key: "column-4", label: <>Capture Date</> },
              { key: "column-5", label: <>Odometer</> },
              { key: "column-6", label: <>User Name</> },
            ]}
          />
          <tbody>
            <tr>
              <td>{valueOrDash(vehicle.statusDescription || vehicle.statusCode)}</td>
              <td>{formatDate(vehicle.statusDate)}</td>
              <td>-</td>
              <td>{formatDateTime(vehicle.statusDate)}</td>
              <td>{valueOrDash(vehicle.currentOdo)}</td>
              <td>API current record</td>
            </tr>
          </tbody>
        </table>
      </div>
      <p className="status-maintenance-note" role="note">
        The current C# API exposes the current status but not the full legacy status-history query.
        This row is a current-record snapshot, not a complete history.
      </p>
    </section>
  );
}
