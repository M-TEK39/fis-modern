import type {
  VehicleEditFormData,
  VehicleEditOption,
} from "@/app/(fleet-operations)/vehicles/edit/vehicle-edit-types";

import { VehicleEditField } from "./vehicle-edit-form-ui";

function inputValue(value: number | null) {
  return value === null ? "" : String(value);
}

function optionLabel(code: number | null, label: string) {
  return code === null ? label : `${label} (${code})`;
}

export function VehicleEditAssignmentFields({
  locations,
  statuses,
  vehicle,
}: Readonly<{
  locations: VehicleEditOption[];
  statuses: VehicleEditOption[];
  vehicle: VehicleEditFormData;
}>) {
  const locationIsListed =
    vehicle.locationCode !== null &&
    locations.some((location) => location.code === vehicle.locationCode);
  const statusIsListed =
    vehicle.vehicleStatusCode !== null &&
    statuses.some((status) => status.code === vehicle.vehicleStatusCode);
  return (
    <section className="vehicle-form-section" aria-labelledby="vehicle-edit-location-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Assignment</p>
          <h2 id="vehicle-edit-location-title">Location and status</h2>
        </div>
      </div>
      <div className="vehicle-create-grid">
        <VehicleEditField id="locationCode" label="Location" required>
          <select
            id="locationCode"
            name="locationCode"
            defaultValue={inputValue(vehicle.locationCode)}
            required
          >
            <option value="">Select location...</option>
            {!locationIsListed && vehicle.locationCode !== null ? (
              <option value={vehicle.locationCode}>
                {optionLabel(vehicle.locationCode, "Current location")}
              </option>
            ) : null}
            {locations.map((location) => (
              <option key={location.code} value={location.code}>
                {location.label} ({location.code})
              </option>
            ))}
          </select>
        </VehicleEditField>
        <VehicleEditField id="vehicleStatusCode" label="Status" required>
          <select
            id="vehicleStatusCode"
            name="statusCode"
            defaultValue={inputValue(vehicle.vehicleStatusCode)}
            required
          >
            <option value="">Select status...</option>
            {!statusIsListed && vehicle.vehicleStatusCode !== null ? (
              <option value={vehicle.vehicleStatusCode}>
                {optionLabel(vehicle.vehicleStatusCode, "Current status")}
              </option>
            ) : null}
            {statuses.map((status) => (
              <option key={status.code} value={status.code}>
                {status.label} ({status.code})
              </option>
            ))}
          </select>
        </VehicleEditField>
      </div>
    </section>
  );
}
