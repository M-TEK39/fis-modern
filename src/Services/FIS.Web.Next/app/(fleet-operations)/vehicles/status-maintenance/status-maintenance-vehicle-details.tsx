import type { VehicleStatusVehicle } from "@/lib/api/vehicles/api-vehicle-status";

import { valueOrDash, vehicleInformationRows } from "./status-maintenance-utils";

export default function StatusMaintenanceVehicleDetails({
  vehicle,
}: Readonly<{ vehicle: VehicleStatusVehicle }>) {
  return (
    <section className="status-maintenance-panel" aria-labelledby="vehicle-information-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Selected vehicle</p>
          <h2 id="vehicle-information-title">Vehicle Information</h2>
        </div>
      </div>
      <dl className="status-maintenance-details">
        {vehicleInformationRows(vehicle).map(([label, value]) => (
          <div key={label}>
            <dt>{label}</dt>
            <dd>{valueOrDash(value)}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}
