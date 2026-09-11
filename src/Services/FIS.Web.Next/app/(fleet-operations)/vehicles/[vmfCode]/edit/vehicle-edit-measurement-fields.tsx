import type { VehicleEditFormData } from "@/app/(fleet-operations)/vehicles/edit/vehicle-edit-types";

import { VehicleEditField } from "./vehicle-edit-form-ui";

function inputValue(value: number | null) {
  return value === null ? "" : String(value);
}

export function VehicleEditMeasurementFields({
  vehicle,
}: Readonly<{ vehicle: VehicleEditFormData }>) {
  return (
    <section className="vehicle-form-section" aria-labelledby="vehicle-edit-measurements-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Vehicle measurements</p>
          <h2 id="vehicle-edit-measurements-title">Odometer and specifications</h2>
        </div>
      </div>
      <div className="vehicle-create-grid">
        <VehicleEditField id="takeOnOdo" label="Take-on odometer (KM)" required>
          <input
            id="takeOnOdo"
            name="takeOnOdo"
            type="number"
            min={0}
            step={1}
            defaultValue={inputValue(vehicle.takeOnOdo)}
            required
          />
        </VehicleEditField>
        <VehicleEditField id="currentOdo" label="Current odometer (KM)" required>
          <input
            id="currentOdo"
            name="currentOdo"
            type="number"
            min={0}
            step={1}
            defaultValue={inputValue(vehicle.currentOdo)}
            required
          />
        </VehicleEditField>
        <VehicleEditField id="tare" label="Tare (kg)" required>
          <input
            id="tare"
            name="tare"
            type="number"
            min={0}
            step={1}
            defaultValue={inputValue(vehicle.tare)}
            required
          />
        </VehicleEditField>
        <VehicleEditField id="gvm" label="GVM (kg)">
          <input
            id="gvm"
            name="gvm"
            type="number"
            min={0}
            step={1}
            defaultValue={inputValue(vehicle.gvm)}
          />
        </VehicleEditField>
      </div>
    </section>
  );
}
