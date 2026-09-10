"use client";

import Link from "next/link";
import { useActionState, useState, type ReactNode } from "react";
import { useFormStatus } from "react-dom";

import type {
  VehicleEditFormData,
  VehicleEditReferenceData,
  VehicleEditUpdateAction,
  VehicleEditUpdateActionState,
} from "@/app/(fleet-operations)/vehicles/edit/vehicle-edit-types";

const initialUpdateState: VehicleEditUpdateActionState = { status: "idle" };

type VehicleEditFormClientProps = {
  vehicle: VehicleEditFormData;
  referenceData: VehicleEditReferenceData;
  updateAction: VehicleEditUpdateAction;
  cancelHref?: string;
  isLoading?: boolean;
};

function Field({
  id,
  label,
  required = false,
  children,
}: Readonly<{ id: string; label: string; required?: boolean; children: ReactNode }>) {
  return (
    <div className="field">
      <label htmlFor={id}>
        {label} {required ? <span aria-hidden="true">*</span> : null}
        {required ? <span className="sr-only"> required</span> : null}
      </label>
      {children}
    </div>
  );
}

function inputValue(value: number | null) {
  return value === null ? "" : String(value);
}

function optionLabel(code: number | null, label: string) {
  return code === null ? label : `${label} (${code})`;
}

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Updating..." : "Update vehicle"}
    </button>
  );
}

function FieldError({ message }: Readonly<{ message?: string }>) {
  return message ? (
    <span className="muted-copy" role="alert">
      {message}
    </span>
  ) : null;
}

export default function VehicleEditFormClient({
  vehicle,
  referenceData,
  updateAction,
  cancelHref = "/vehicles/edit",
  isLoading = false,
}: VehicleEditFormClientProps) {
  const [state, formAction] = useActionState<VehicleEditUpdateActionState, FormData>(
    updateAction,
    initialUpdateState,
  );
  const [typeCode, setTypeCode] = useState(inputValue(vehicle.typeCode));

  if (isLoading) {
    return (
      <div className="loading-card" aria-busy="true">
        <span className="spinner" aria-hidden="true" />
        <p>Loading vehicle details...</p>
      </div>
    );
  }

  const models = referenceData.models;
  const modelIsListed =
    vehicle.modelCode !== null && models.some((model) => model.code === vehicle.modelCode);
  const statusIsListed =
    vehicle.vehicleStatusCode !== null &&
    referenceData.statuses.some((status) => status.code === vehicle.vehicleStatusCode);
  const locationIsListed =
    vehicle.locationCode !== null &&
    referenceData.locations.some((location) => location.code === vehicle.locationCode);

  return (
    <div className="vehicle-create-form">
      <div className="notice notice-info" role="note">
        <span aria-hidden="true">i</span>
        <span>
          Only fields persisted by the current Vehicle API are available on this edit form.
        </span>
      </div>

      {state.status === "error" && state.message ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>{state.message}</span>
        </div>
      ) : null}

      {state.status === "success" && state.message ? (
        <div className="notice notice-success" role="status">
          <span aria-hidden="true">✓</span>
          <span>{state.message}</span>
        </div>
      ) : null}

      <form action={formAction}>
        <input type="hidden" name="vmfCode" value={vehicle.vmfCode} readOnly />
        <input type="hidden" name="fleetNumber" value={vehicle.fleetNumber} readOnly />

        <section className="vehicle-form-section" aria-labelledby="vehicle-edit-identity-title">
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Vehicle Master maintenance</p>
              <h2 id="vehicle-edit-identity-title">Vehicle information</h2>
            </div>
            <span className="vehicle-required-note">* Required</span>
          </div>

          <div className="vehicle-create-grid">
            <Field id="fleetNumberDisplay" label="Current GG number">
              <input
                id="fleetNumberDisplay"
                type="text"
                value={vehicle.fleetNumber}
                readOnly
                aria-readonly="true"
              />
            </Field>

            <Field id="registrationNumber" label="Current GP number" required>
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
              <FieldError message={state.fieldErrors?.registrationNumber} />
            </Field>

            <Field id="modelCode" label="Model" required>
              <select
                id="modelCode"
                name="modelCode"
                defaultValue={inputValue(vehicle.modelCode)}
                required
                onChange={(event) => {
                  const selected = models.find(
                    (model) => model.code === Number(event.target.value),
                  );
                  if (selected?.typeCode !== null && selected?.typeCode !== undefined) {
                    setTypeCode(String(selected.typeCode));
                  }
                }}
              >
                <option value="">Select model...</option>
                {!modelIsListed && vehicle.modelCode !== null ? (
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
            </Field>

            <Field id="typeCode" label="Type" required>
              <select
                id="typeCode"
                name="typeCode"
                value={typeCode}
                onChange={(event) => setTypeCode(event.target.value)}
                required
              >
                <option value="">Select type...</option>
                {!referenceData.types.some((type) => String(type.code) === typeCode) &&
                vehicle.typeCode !== null ? (
                  <option value={vehicle.typeCode}>
                    {optionLabel(vehicle.typeCode, "Current type")}
                  </option>
                ) : null}
                {referenceData.types.map((type) => (
                  <option key={type.code} value={type.code}>
                    {type.label} ({type.code})
                  </option>
                ))}
              </select>
            </Field>

            <Field id="colour" label="Colour" required>
              <input id="colour" name="colour" type="text" defaultValue={vehicle.colour} required />
              <FieldError message={state.fieldErrors?.colour} />
            </Field>

            <Field id="yearManufactured" label="Year manufactured" required>
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
            </Field>

            <Field id="chassisNumber" label="VIN / chassis number" required>
              <input
                id="chassisNumber"
                name="chassisNumber"
                type="text"
                defaultValue={vehicle.chassisNumber}
                autoComplete="off"
                required
              />
              <FieldError message={state.fieldErrors?.chassisNumber} />
            </Field>

            <Field id="engineNumber" label="Engine number" required>
              <input
                id="engineNumber"
                name="engineNumber"
                type="text"
                defaultValue={vehicle.engineNumber}
                autoComplete="off"
                required
              />
              <FieldError message={state.fieldErrors?.engineNumber} />
            </Field>
          </div>
        </section>

        <section className="vehicle-form-section" aria-labelledby="vehicle-edit-location-title">
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Assignment</p>
              <h2 id="vehicle-edit-location-title">Location and status</h2>
            </div>
          </div>

          <div className="vehicle-create-grid">
            <Field id="locationCode" label="Location" required>
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
                {referenceData.locations.map((location) => (
                  <option key={location.code} value={location.code}>
                    {location.label} ({location.code})
                  </option>
                ))}
              </select>
            </Field>

            <Field id="vehicleStatusCode" label="Status" required>
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
                {referenceData.statuses.map((status) => (
                  <option key={status.code} value={status.code}>
                    {status.label} ({status.code})
                  </option>
                ))}
              </select>
            </Field>
          </div>
        </section>

        <section className="vehicle-form-section" aria-labelledby="vehicle-edit-measurements-title">
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Vehicle measurements</p>
              <h2 id="vehicle-edit-measurements-title">Odometer and specifications</h2>
            </div>
          </div>

          <div className="vehicle-create-grid">
            <Field id="takeOnOdo" label="Take-on odometer (KM)" required>
              <input
                id="takeOnOdo"
                name="takeOnOdo"
                type="number"
                min={0}
                step={1}
                defaultValue={inputValue(vehicle.takeOnOdo)}
                required
              />
            </Field>

            <Field id="currentOdo" label="Current odometer (KM)" required>
              <input
                id="currentOdo"
                name="currentOdo"
                type="number"
                min={0}
                step={1}
                defaultValue={inputValue(vehicle.currentOdo)}
                required
              />
            </Field>

            <Field id="tare" label="Tare (kg)" required>
              <input
                id="tare"
                name="tare"
                type="number"
                min={0}
                step={1}
                defaultValue={inputValue(vehicle.tare)}
                required
              />
            </Field>

            <Field id="gvm" label="GVM (kg)">
              <input
                id="gvm"
                name="gvm"
                type="number"
                min={0}
                step={1}
                defaultValue={inputValue(vehicle.gvm)}
              />
            </Field>
          </div>
        </section>

        <section className="vehicle-form-section" aria-labelledby="vehicle-edit-identifiers-title">
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">External identifiers</p>
              <h2 id="vehicle-edit-identifiers-title">IFMS and NATIS numbers</h2>
            </div>
          </div>

          <div className="vehicle-create-grid">
            <Field id="ifmsVehicleRegisterNumber" label="IFMS vehicle register number">
              <input
                id="ifmsVehicleRegisterNumber"
                name="ifmsVehicleRegisterNumber"
                type="text"
                maxLength={50}
                defaultValue={vehicle.ifmsVehicleRegisterNumber}
                autoComplete="off"
              />
            </Field>

            <Field id="natisModelNumber" label="NATIS model number">
              <input
                id="natisModelNumber"
                name="natisModelNumber"
                type="text"
                maxLength={50}
                defaultValue={vehicle.natisModelNumber}
                autoComplete="off"
              />
            </Field>
          </div>
        </section>

        <section className="vehicle-form-section" aria-labelledby="vehicle-edit-tariff-title">
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Optional calculation</p>
              <h2 id="vehicle-edit-tariff-title">Tariff components</h2>
            </div>
          </div>

          <label className="vehicle-checkbox-label" htmlFor="recalculateTariff">
            <input id="recalculateTariff" name="recalculateTariff" type="checkbox" value="true" />
            Recalculate vehicle tariff components on submit
          </label>
        </section>

        <div className="vehicle-create-actions">
          <Link className="button button-secondary" href={cancelHref}>
            Cancel
          </Link>
          <SubmitButton />
        </div>
      </form>
    </div>
  );
}
