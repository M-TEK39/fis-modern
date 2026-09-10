"use client";

import Link from "next/link";
import { useActionState, useState } from "react";
import { useFormStatus } from "react-dom";

import type { DemoVehicleActionState } from "@/app/(fleet-operations)/vehicles/demo/actions";
import type { MakeRecord } from "@/lib/api/reference-data/api-makes";
import type { ModelRecord } from "@/lib/api/reference-data/api-models";
import type { SiteRecord } from "@/lib/api/reference-data/api-sites";

export type DemoVehicleFormValues = {
  demoVehicleCode?: number;
  ggNumber: string;
  registrationNumber: string;
  modelDescription: string;
  siteCode: string;
  yearManufactured: string;
  bankCode: string;
  colour: string;
  tank: string;
  engineNumber: string;
  chassisNumber: string;
};

type DemoVehicleAction = (
  previousState: DemoVehicleActionState,
  formData: FormData,
) => Promise<DemoVehicleActionState>;

type DemoVehicleFormProps = {
  action: DemoVehicleAction;
  mode: "create" | "update";
  initialValues: DemoVehicleFormValues;
  sites: SiteRecord[];
  makes: MakeRecord[];
  models: ModelRecord[];
  returnPath: string;
};

const initialState: DemoVehicleActionState = { status: "idle" };

function inputValue(value: string | number | null | undefined) {
  return value === null || value === undefined ? "" : String(value);
}

function SubmitButton({ mode }: Readonly<{ mode: "create" | "update" }>) {
  const { pending } = useFormStatus();
  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Saving..." : mode === "create" ? "Add Demo Vehicle" : "Update Demo Vehicle"}
    </button>
  );
}

export default function DemoVehicleForm({
  action,
  mode,
  initialValues,
  sites,
  makes,
  models,
  returnPath,
}: DemoVehicleFormProps) {
  const [state, formAction] = useActionState(action, initialState);
  const initialMake = models.find(
    (model) =>
      model.modelDescription.localeCompare(initialValues.modelDescription, undefined, {
        sensitivity: "base",
      }) === 0,
  )?.makeCode;
  const [selectedMakeCode, setSelectedMakeCode] = useState(initialMake ? String(initialMake) : "");
  const matchingModels = models.filter(
    (model) => !selectedMakeCode || model.makeCode === Number(selectedMakeCode),
  );
  return (
    <form action={formAction} className="vehicle-create-form">
      {state.status === "error" && state.message ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>{state.message}</span>
        </div>
      ) : null}
      {state.status === "success" && state.message ? (
        <div className="notice notice-success" role="status">
          {state.message}
        </div>
      ) : null}
      {mode === "update" ? (
        <input
          name="demoVehicleCode"
          type="hidden"
          value={inputValue(initialValues.demoVehicleCode)}
          readOnly
        />
      ) : null}
      <section className="vehicle-form-section" aria-labelledby="demo-vehicle-details-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy demo vehicle record</p>
            <h2 id="demo-vehicle-details-title">Vehicle details</h2>
          </div>
          <span className="vehicle-required-note">* Required</span>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="demo-gg-number">GG number</label>
            <input
              id="demo-gg-number"
              name="ggNumber"
              type="text"
              maxLength={7}
              defaultValue={initialValues.ggNumber}
            />
          </div>
          <div className="field">
            <label htmlFor="demo-registration-number">
              Prov. Registration number <span aria-hidden="true">*</span>
              <span className="sr-only"> required</span>
            </label>
            <input
              id="demo-registration-number"
              name="registrationNumber"
              type="text"
              maxLength={8}
              defaultValue={initialValues.registrationNumber}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="demo-make">Make</label>
            <select
              id="demo-make"
              value={selectedMakeCode}
              onChange={(event) => setSelectedMakeCode(event.target.value)}
            >
              <option value="">All makes</option>
              {makes
                .slice()
                .sort((a, b) => a.makeDescription.localeCompare(b.makeDescription))
                .map((make) => (
                  <option key={make.makeCode} value={make.makeCode}>
                    {make.makeDescription} ({make.makeCode})
                  </option>
                ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="demo-model-description">
              Make &amp; Model <span aria-hidden="true">*</span>
              <span className="sr-only"> required</span>
            </label>
            <input
              id="demo-model-description"
              name="modelDescription"
              type="text"
              list="demo-model-options"
              maxLength={100}
              defaultValue={initialValues.modelDescription}
              required
            />
            <datalist id="demo-model-options">
              {matchingModels.map((model) => (
                <option key={model.modelCode} value={model.modelDescription}>
                  {model.makeDescription ?? ""}
                </option>
              ))}
            </datalist>
          </div>
          <div className="field">
            <label htmlFor="demo-site-code">Site</label>
            <select id="demo-site-code" name="siteCode" defaultValue={initialValues.siteCode}>
              <option value="">Select...</option>
              {sites
                .slice()
                .sort((a, b) => (a.departmentNumber ?? "").localeCompare(b.departmentNumber ?? ""))
                .map((site) => (
                  <option key={site.siteCode} value={site.siteCode}>
                    {inputValue(site.departmentNumber) || "-"} :{" "}
                    {inputValue(site.description) || "-"}
                  </option>
                ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="demo-year-manufactured">Year Manufactured</label>
            <input
              id="demo-year-manufactured"
              name="yearManufactured"
              type="number"
              defaultValue={initialValues.yearManufactured}
            />
          </div>
          <div className="field">
            <label htmlFor="demo-bank-code">Bank code</label>
            <input
              id="demo-bank-code"
              name="bankCode"
              type="text"
              maxLength={12}
              defaultValue={initialValues.bankCode}
            />
          </div>
          <div className="field">
            <label htmlFor="demo-colour">Colour</label>
            <input
              id="demo-colour"
              name="colour"
              type="text"
              maxLength={25}
              defaultValue={initialValues.colour}
            />
          </div>
          <div className="field">
            <label htmlFor="demo-tank">Tank Capacity</label>
            <input id="demo-tank" name="tank" type="number" defaultValue={initialValues.tank} />
          </div>
          <div className="field">
            <label htmlFor="demo-engine-number">Engine number</label>
            <input
              id="demo-engine-number"
              name="engineNumber"
              type="text"
              maxLength={50}
              defaultValue={initialValues.engineNumber}
            />
          </div>
          <div className="field">
            <label htmlFor="demo-chassis-number">Chassis number</label>
            <input
              id="demo-chassis-number"
              name="chassisNumber"
              type="text"
              maxLength={50}
              defaultValue={initialValues.chassisNumber}
            />
          </div>
        </div>
        <p className="muted-copy">
          The model field keeps the original free-text behavior and offers existing models as
          suggestions.
        </p>
      </section>
      <div className="button-row">
        <SubmitButton mode={mode} />
        <Link className="button button-secondary" href={returnPath}>
          Cancel
        </Link>
      </div>
    </form>
  );
}
