import type { VehicleEditVehicle } from "@/lib/api/vehicles/api-vehicle-edit";

import type { AssetVerificationInitial } from "./details-types";

export function VehicleContextFields({
  initial,
  vehicle,
}: Readonly<{ initial: AssetVerificationInitial; vehicle: VehicleEditVehicle }>) {
  return (
    <>
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Vehicle context</p>
          <h2>Read-only vehicle and contract details</h2>
        </div>
      </div>
      <div className="form-grid">
        <div className="form-field">
          <label className="form-label" htmlFor="asset-verification-department">
            Department Name
          </label>
          <input
            className="form-input"
            id="asset-verification-department"
            readOnly
            value={initial.departmentName}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="asset-verification-site">
            Site Name
          </label>
          <input
            className="form-input"
            id="asset-verification-site"
            readOnly
            value={initial.siteName}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="asset-verification-reg">
            Vehicle Reg. No
          </label>
          <input
            className="form-input"
            id="asset-verification-reg"
            readOnly
            value={vehicle.registrationNumber ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="asset-verification-gg">
            GG No
          </label>
          <input
            className="form-input"
            id="asset-verification-gg"
            readOnly
            value={vehicle.fleetNumber ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="asset-verification-model">
            Vehicle Model
          </label>
          <input
            className="form-input"
            id="asset-verification-model"
            readOnly
            value={initial.vehicleModel}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="asset-verification-colour">
            Vehicle Colour
          </label>
          <input
            className="form-input"
            id="asset-verification-colour"
            readOnly
            value={initial.vehicleColour}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="asset-verification-engine">
            Engine Number
          </label>
          <input
            className="form-input"
            id="asset-verification-engine"
            readOnly
            value={initial.vehicleEngineNumber}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="asset-verification-chassis">
            Chassis Number
          </label>
          <input
            className="form-input"
            id="asset-verification-chassis"
            readOnly
            value={initial.vehicleChassisNumber}
          />
        </div>
      </div>
    </>
  );
}
