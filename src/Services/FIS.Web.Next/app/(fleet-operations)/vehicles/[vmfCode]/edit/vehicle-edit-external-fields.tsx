import type { VehicleEditFormData } from "@/app/(fleet-operations)/vehicles/edit/vehicle-edit-types";

import { VehicleEditField } from "./vehicle-edit-form-ui";

export function VehicleEditExternalFields({ vehicle }: Readonly<{ vehicle: VehicleEditFormData }>) {
  return (
    <section className="vehicle-form-section" aria-labelledby="vehicle-edit-identifiers-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">External identifiers</p>
          <h2 id="vehicle-edit-identifiers-title">IFMS and NATIS numbers</h2>
        </div>
      </div>
      <div className="vehicle-create-grid">
        <VehicleEditField id="ifmsVehicleRegisterNumber" label="IFMS vehicle register number">
          <input
            id="ifmsVehicleRegisterNumber"
            name="ifmsVehicleRegisterNumber"
            type="text"
            maxLength={50}
            defaultValue={vehicle.ifmsVehicleRegisterNumber}
            autoComplete="off"
          />
        </VehicleEditField>
        <VehicleEditField id="natisModelNumber" label="NATIS model number">
          <input
            id="natisModelNumber"
            name="natisModelNumber"
            type="text"
            maxLength={50}
            defaultValue={vehicle.natisModelNumber}
            autoComplete="off"
          />
        </VehicleEditField>
      </div>
    </section>
  );
}
