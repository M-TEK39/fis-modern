"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import type { ModelActionState } from "@/app/validation-data/models/actions";
import type { ModelOption, ModelRecord, ModelReferenceData } from "@/lib/api-models";

type ModelAction = (
  previousState: ModelActionState,
  formData: FormData,
) => Promise<ModelActionState>;

type ModelFormProps = {
  action: ModelAction;
  model: ModelRecord;
  mode: "create" | "update";
  referenceData: ModelReferenceData;
};

const initialState: ModelActionState = { status: "idle" };

function inputValue(value: string | number | null | undefined) {
  return value === null || value === undefined ? "" : String(value);
}

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

function SelectField({
  id,
  label,
  value,
  options,
  required = false,
  optionalLabel,
}: Readonly<{
  id: string;
  label: string;
  value: number | null;
  options: ModelOption[];
  required?: boolean;
  optionalLabel?: string;
}>) {
  return (
    <Field id={id} label={label} required={required}>
      <select id={id} name={id} defaultValue={inputValue(value)} required={required}>
        {!required ? (
          <option value="">{optionalLabel ?? "Not specified"}</option>
        ) : (
          <option value="">Select {label.toLowerCase()}</option>
        )}
        {options.map((option) => (
          <option key={option.code} value={option.code}>
            {option.description} ({option.code})
          </option>
        ))}
      </select>
    </Field>
  );
}

function SubmitButton({ mode }: Readonly<{ mode: "create" | "update" }>) {
  const { pending } = useFormStatus();
  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Saving..." : mode === "create" ? "Add Model" : "Update Model"}
    </button>
  );
}

export default function ModelForm({ action, model, mode, referenceData }: ModelFormProps) {
  const [state, formAction] = useActionState(action, initialState);

  return (
    <form action={formAction} className="vehicle-create-form">
      {state.status === "error" && state.message ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>{state.message}</span>
        </div>
      ) : null}
      {mode === "update" ? (
        <input name="modelCode" type="hidden" value={model.modelCode} readOnly />
      ) : null}

      <section className="vehicle-form-section" aria-labelledby="model-details-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy vehicle validation</p>
            <h2 id="model-details-title">Model identity and references</h2>
          </div>
          <span className="vehicle-required-note">* Required</span>
        </div>
        <div className="field-grid">
          <Field id="modelDescription" label="Model description" required>
            <input
              id="modelDescription"
              name="modelDescription"
              type="text"
              maxLength={60}
              defaultValue={model.modelDescription}
              required
            />
          </Field>
          <SelectField
            id="makeCode"
            label="Make"
            value={model.makeCode}
            options={referenceData.makes}
            required
          />
          <SelectField
            id="fuelTypeCode"
            label="Fuel type"
            value={model.fuelTypeCode}
            options={referenceData.fuelTypes}
            required
          />
          <SelectField
            id="licenceFeeCode"
            label="Licence fee"
            value={model.licenceFeeCode}
            options={referenceData.licenceFees}
            required
          />
          <SelectField
            id="licenceCode"
            label="Driver licence"
            value={model.licenceCode}
            options={referenceData.driverLicences}
            required
          />
          <SelectField
            id="classCode"
            label="Class"
            value={model.classCode}
            options={referenceData.classes}
            required
          />
          <SelectField
            id="unitOfMeasureCode"
            label="Unit of measure"
            value={model.unitOfMeasureCode}
            options={referenceData.units}
            required
          />
          <SelectField
            id="maintenanceTriggerCode"
            label="Maintenance trigger"
            value={model.maintenanceTriggerCode}
            options={referenceData.maintenanceTriggers}
            optionalLabel="No maintenance trigger"
          />
          <SelectField
            id="typeCode"
            label="Vehicle type"
            value={model.typeCode}
            options={referenceData.types}
            optionalLabel="Not specified in legacy schema"
          />
        </div>
      </section>

      <section className="vehicle-form-section" aria-labelledby="model-specifications-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy model fields</p>
            <h2 id="model-specifications-title">Engine and operating specifications</h2>
          </div>
        </div>
        <div className="field-grid">
          <Field id="engineType" label="Engine type">
            <input
              id="engineType"
              name="engineType"
              type="text"
              maxLength={30}
              defaultValue={inputValue(model.engineType)}
            />
          </Field>
          <Field id="engineCapacity" label="Engine capacity" required>
            <input
              id="engineCapacity"
              name="engineCapacity"
              type="number"
              min={0}
              max={32767}
              defaultValue={inputValue(model.engineCapacity)}
              required
            />
          </Field>
          <Field id="ratedPower" label="Rated power (kW)" required>
            <input
              id="ratedPower"
              name="ratedPower"
              type="number"
              min={0}
              max={32767}
              defaultValue={inputValue(model.ratedPower)}
              required
            />
          </Field>
          <Field id="fuelTankCapacity" label="Fuel tank capacity" required>
            <input
              id="fuelTankCapacity"
              name="fuelTankCapacity"
              type="number"
              min={0}
              max={32767}
              defaultValue={inputValue(model.fuelTankCapacity)}
              required
            />
          </Field>
          <Field id="targetConsumption" label="Target consumption" required>
            <input
              id="targetConsumption"
              name="targetConsumption"
              type="number"
              min={0}
              step="0.01"
              defaultValue={inputValue(model.targetConsumption)}
              required
            />
          </Field>
          <Field id="targetTyreLife" label="Target tyre life" required>
            <input
              id="targetTyreLife"
              name="targetTyreLife"
              type="number"
              min={0}
              defaultValue={inputValue(model.targetTyreLife)}
              required
            />
          </Field>
          <Field id="serviceInterval" label="Service interval" required>
            <input
              id="serviceInterval"
              name="serviceInterval"
              type="number"
              min={0}
              defaultValue={inputValue(model.serviceInterval)}
              required
            />
          </Field>
          <Field id="vemmCode" label="VEMM code" required>
            <input
              id="vemmCode"
              name="vemmCode"
              type="text"
              minLength={7}
              maxLength={20}
              defaultValue={inputValue(model.vemmCode)}
              required
            />
          </Field>
          <Field id="gvm" label="GVM" required>
            <input
              id="gvm"
              name="gvm"
              type="number"
              min={0}
              defaultValue={inputValue(model.gvm)}
              required
            />
          </Field>
          <Field id="transmission" label="Transmission" required>
            <select
              id="transmission"
              name="transmission"
              defaultValue={model.transmission ?? "M"}
              required
            >
              <option value="M">Manual</option>
              <option value="A">Automatic</option>
            </select>
          </Field>
          <Field id="wesbankKilosPerLitre" label="Wesbank kilos per litre">
            <input
              id="wesbankKilosPerLitre"
              name="wesbankKilosPerLitre"
              type="number"
              min={0}
              step="0.01"
              defaultValue={inputValue(model.wesbankKilosPerLitre)}
            />
          </Field>
        </div>
      </section>

      <div className="button-row">
        <SubmitButton mode={mode} />
        <Link className="button button-secondary" href="/Validation/MNT_model.aspx">
          Cancel
        </Link>
      </div>
    </form>
  );
}
