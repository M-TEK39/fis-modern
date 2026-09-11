import type {
  VehicleEditFormData,
  VehicleEditModelOption,
  VehicleEditOption,
  VehicleEditUpdateActionState,
} from "@/app/(fleet-operations)/vehicles/edit/vehicle-edit-types";

import { VehicleEditField, VehicleEditFieldError } from "./vehicle-edit-form-ui";

function inputValue(value: number | null) {
  return value === null ? "" : String(value);
}

function optionLabel(code: number | null, label: string) {
  return code === null ? label : `${label} (${code})`;
}

export function VehicleEditIdentityFields({
  models,
  referenceDataTypes,
  state,
  typeCode,
  vehicle,
  onTypeCodeChange,
}: Readonly<{
  models: VehicleEditModelOption[];
  referenceDataTypes: VehicleEditOption[];
  state: VehicleEditUpdateActionState;
  typeCode: string;
  vehicle: VehicleEditFormData;
  onTypeCodeChange: (value: string) => void;
}>) {
  return (
    <section className="vehicle-form-section" aria-labelledby="vehicle-edit-identity-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Vehicle Master maintenance</p>
          <h2 id="vehicle-edit-identity-title">Vehicle information</h2>
        </div>
        <span className="vehicle-required-note">* Required</span>
      </div>
      <div className="vehicle-create-grid">
        <VehicleEditField id="fleetNumberDisplay" label="Current GG number">
          <input
            id="fleetNumberDisplay"
            type="text"
            value={vehicle.fleetNumber}
            readOnly
            aria-readonly="true"
          />
        </VehicleEditField>
        <VehicleEditField id="registrationNumber" label="Current GP number" required>
          <input
            id="registrationNumber"
            name="registrationNumber"
            type="text"
            defaultValue={vehicle.registrationNumber}
            minLength={6}
            maxLength={8}
            autoComplete="off"
            required
          />
          <VehicleEditFieldError message={state.fieldErrors?.registrationNumber} />
        </VehicleEditField>
        <VehicleEditField id="modelCode" label="Model" required>
          <select
            id="modelCode"
            name="modelCode"
            defaultValue={inputValue(vehicle.modelCode)}
            required
            onChange={(event) => {
              const selected = models.find((model) => model.code === Number(event.target.value));
              if (selected?.typeCode !== null && selected?.typeCode !== undefined) {
                onTypeCodeChange(String(selected.typeCode));
              }
            }}
          >
            <option value="">Select model...</option>
            {vehicle.modelCode !== null &&
            !models.some((model) => model.code === vehicle.modelCode) ? (
              <option value={vehicle.modelCode}>
                {optionLabel(vehicle.modelCode, "Current model")}
              </option>
            ) : null}
            {models.map((model) => (
              <option key={model.code} value={model.code}>
                {model.name} ({model.code})
              </option>
            ))}
          </select>
        </VehicleEditField>
        <VehicleEditField id="typeCode" label="Type" required>
          <select
            id="typeCode"
            name="typeCode"
            value={typeCode}
            onChange={(event) => onTypeCodeChange(event.target.value)}
            required
          >
            <option value="">Select type...</option>
            {!referenceDataTypes.some((type) => String(type.code) === typeCode) &&
            vehicle.typeCode !== null ? (
              <option value={vehicle.typeCode}>
                {optionLabel(vehicle.typeCode, "Current type")}
              </option>
            ) : null}
            {referenceDataTypes.map((type) => (
              <option key={type.code} value={type.code}>
                {type.label} ({type.code})
              </option>
            ))}
          </select>
        </VehicleEditField>
        <VehicleEditField id="colour" label="Colour" required>
          <input id="colour" name="colour" type="text" defaultValue={vehicle.colour} required />
          <VehicleEditFieldError message={state.fieldErrors?.colour} />
        </VehicleEditField>
        <VehicleEditField id="yearManufactured" label="Year manufactured" required>
          <input
            id="yearManufactured"
            name="yearManufactured"
            type="number"
            min={1900}
            max={9999}
            step={1}
            defaultValue={inputValue(vehicle.yearManufactured)}
            required
          />
        </VehicleEditField>
        <VehicleEditField id="chassisNumber" label="VIN / chassis number" required>
          <input
            id="chassisNumber"
            name="chassisNumber"
            type="text"
            defaultValue={vehicle.chassisNumber}
            autoComplete="off"
            required
          />
          <VehicleEditFieldError message={state.fieldErrors?.chassisNumber} />
        </VehicleEditField>
        <VehicleEditField id="engineNumber" label="Engine number" required>
          <input
            id="engineNumber"
            name="engineNumber"
            type="text"
            defaultValue={vehicle.engineNumber}
            autoComplete="off"
            required
          />
          <VehicleEditFieldError message={state.fieldErrors?.engineNumber} />
        </VehicleEditField>
      </div>
    </section>
  );
}
