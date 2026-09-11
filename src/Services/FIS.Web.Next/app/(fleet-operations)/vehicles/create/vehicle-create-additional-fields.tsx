import type { VehicleCreateReferenceData } from "@/lib/api/vehicles/api-vehicle-create";

import { VehicleCreateField } from "./vehicle-create-form-ui";

export function VehicleCreateAdditionalFields({
  referenceData,
}: Readonly<{ referenceData: VehicleCreateReferenceData }>) {
  return (
    <>
      <section className="vehicle-form-section" aria-labelledby="vehicle-service-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Service information</p>
            <h2 id="vehicle-service-title">Authorization status</h2>
          </div>
        </div>
        <div className="vehicle-status-summary">
          <span className="vehicle-badge badge-warning">Awaiting Authorization</span>
          <span className="muted-copy">
            The vehicle will be available for authorization after submission.
          </span>
        </div>
        <input name="statusCode" type="hidden" value="0" readOnly />
      </section>
      <section className="vehicle-form-section" aria-labelledby="vehicle-notes-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Capture details</p>
            <h2 id="vehicle-notes-title">Condition and notes</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <fieldset className="field vehicle-fieldset">
            <legend>Damages?</legend>
            <label className="vehicle-checkbox-label">
              <input name="damageStatus" type="radio" value="N" defaultChecked /> No
            </label>
            <label className="vehicle-checkbox-label">
              <input name="damageStatus" type="radio" value="Y" /> Yes
            </label>
          </fieldset>
          <VehicleCreateField id="damagesComment" label="Damage details">
            <textarea id="damagesComment" name="damagesComment" rows={3} maxLength={355} />
          </VehicleCreateField>
          <VehicleCreateField id="fleetNotes" label="Fleet notes">
            <textarea id="fleetNotes" name="fleetNotes" rows={3} maxLength={255} />
          </VehicleCreateField>
          <VehicleCreateField id="comment" label="Capturer's comment" required>
            <textarea id="comment" name="comment" rows={3} maxLength={90} required />
          </VehicleCreateField>
        </div>
      </section>
      <section className="vehicle-form-section" aria-labelledby="vehicle-maintenance-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Optional legacy plan</p>
            <h2 id="vehicle-maintenance-title">Vehicle maintenance options</h2>
          </div>
        </div>
        <div className="vehicle-create-grid">
          <VehicleCreateField id="maintenanceTypeCode" label="Maintenance type">
            <select
              id="maintenanceTypeCode"
              name="maintenanceTypeCode"
              defaultValue=""
              disabled={referenceData.maintenanceTypes.length === 0}
            >
              <option value="">
                {referenceData.maintenanceTypes.length > 0
                  ? "No maintenance option"
                  : "No options available"}
              </option>
              {referenceData.maintenanceTypes.map((type) => (
                <option key={type.code} value={type.code}>
                  {type.name} ({type.code})
                </option>
              ))}
            </select>
          </VehicleCreateField>
          <VehicleCreateField id="maintenanceStartDate" label="Start date">
            <input id="maintenanceStartDate" name="maintenanceStartDate" type="date" />
          </VehicleCreateField>
          <VehicleCreateField id="maintenancePeriodMonths" label="Period (months)">
            <input
              id="maintenancePeriodMonths"
              name="maintenancePeriodMonths"
              type="number"
              min="0"
              step="1"
            />
          </VehicleCreateField>
          <VehicleCreateField id="maintenanceKilos" label="Kilos for period">
            <input id="maintenanceKilos" name="maintenanceKilos" type="number" min="0" step="1" />
          </VehicleCreateField>
          <VehicleCreateField id="maintenanceValue" label="Maintenance value">
            <input
              id="maintenanceValue"
              name="maintenanceValue"
              type="number"
              min="0"
              step="0.01"
            />
          </VehicleCreateField>
        </div>
      </section>
      <section className="vehicle-form-section" aria-labelledby="vehicle-extras-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Optional equipment</p>
            <h2 id="vehicle-extras-title">Extras</h2>
          </div>
        </div>
        {referenceData.extras.length > 0 ? (
          <div className="vehicle-checkbox-grid">
            {referenceData.extras.map((extra) => (
              <label className="vehicle-checkbox-label" key={extra.code}>
                <input name="extraCodes" type="checkbox" value={extra.code} />
                {extra.name}
              </label>
            ))}
          </div>
        ) : (
          <p className="muted-copy">No vehicle extras are configured.</p>
        )}
      </section>
    </>
  );
}
