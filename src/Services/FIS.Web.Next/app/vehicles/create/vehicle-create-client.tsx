"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { useActionState, useMemo, useRef, useState } from "react";
import { useFormStatus } from "react-dom";

import {
  createVehicleAction,
  searchVehicleAction,
  type CreateVehicleActionState,
  type SearchVehicleActionState,
} from "@/app/vehicles/create/actions";
import type { VehicleCreateReferenceData, VehicleSearchResult } from "@/lib/api-vehicle-create";

const initialActionState: CreateVehicleActionState = { status: "idle" };
const initialSearchStatus: SearchVehicleActionState = { status: "idle", results: [] };

type VehicleCreateClientProps = {
  referenceData: VehicleCreateReferenceData;
  today: string;
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

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Submitting..." : "Submit for authorization"}
    </button>
  );
}

function SearchResults({ results }: Readonly<{ results: VehicleSearchResult[] }>) {
  return (
    <div className="vehicle-search-results" aria-live="polite">
      {results.map((result) => (
        <div className="vehicle-search-result" key={result.vmfCode}>
          <strong>{result.fleetNumber || `Vehicle ${result.vmfCode}`}</strong>
          <span>
            {[
              result.registrationNumber,
              result.chassisNumber,
              result.engineNumber,
              result.invoiceNumber,
            ]
              .filter(Boolean)
              .join(" · ") || "No additional identifying details"}
          </span>
        </div>
      ))}
    </div>
  );
}

function QuickSearch() {
  const [state, formAction] = useActionState<SearchVehicleActionState, FormData>(
    searchVehicleAction,
    initialSearchStatus,
  );

  return (
    <section className="vehicle-form-section" aria-labelledby="vehicle-search-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Legacy capture utility</p>
          <h2 id="vehicle-search-title">Quick search / recall</h2>
        </div>
      </div>
      <form action={formAction} className="vehicle-quick-search-form">
        <div className="field">
          <label htmlFor="quickSearch">VIN / engine / GG / invoice number</label>
          <input
            id="quickSearch"
            name="quickSearch"
            type="search"
            placeholder="Enter an identifier"
            autoComplete="off"
          />
        </div>
        <div className="vehicle-create-actions vehicle-quick-search-actions">
          <button className="button button-secondary" type="submit" name="intent" value="reset">
            Reset search
          </button>
          <button
            className="button button-secondary"
            type="button"
            disabled
            title="Pending capture recall is not supported by the current API"
          >
            Recall pending capture
          </button>
          <button className="button button-primary" type="submit" name="intent" value="search">
            Search
          </button>
        </div>
      </form>
      {state.status === "error" && state.message ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>{state.message}</span>
        </div>
      ) : null}
      {state.status === "success" && state.message ? (
        <p className="muted-copy">{state.message}</p>
      ) : null}
      {state.results.length > 0 ? <SearchResults results={state.results} /> : null}
    </section>
  );
}

export default function VehicleCreateClient({ referenceData, today }: VehicleCreateClientProps) {
  const [state, formAction] = useActionState<CreateVehicleActionState, FormData>(
    createVehicleAction,
    initialActionState,
  );
  const formRef = useRef<HTMLFormElement>(null);
  const [selectedMakeCode, setSelectedMakeCode] = useState(0);
  const [selectedModelCode, setSelectedModelCode] = useState(0);
  const [colourSelection, setColourSelection] = useState("");
  const [selectedColour, setSelectedColour] = useState("");

  const availableModels = useMemo(
    () => referenceData.models.filter((model) => model.makeCode === selectedMakeCode),
    [referenceData.models, selectedMakeCode],
  );
  function clearForm() {
    formRef.current?.reset();
    setSelectedMakeCode(0);
    setSelectedModelCode(0);
    setColourSelection("");
    setSelectedColour("");
  }

  return (
    <div className="vehicle-create-form">
      <QuickSearch />

      <form ref={formRef} action={formAction}>
        {state.status === "error" && state.message ? (
          <div className="notice notice-error" role="alert">
            <span aria-hidden="true">!</span>
            <span>{state.message}</span>
          </div>
        ) : null}

        <div className="notice notice-info" role="note">
          <span aria-hidden="true">i</span>
          <span>
            GG numbers are allocated during authorization when a free number is available. This
            capture is saved to the pre-vehicle authorization workflow, including its legacy
            purchase, site, damage, notes, extras, and maintenance details.
          </span>
        </div>

        <section className="vehicle-form-section" aria-labelledby="vehicle-identity-title">
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Capture sequence</p>
              <h2 id="vehicle-identity-title">Vehicle information</h2>
            </div>
            <span className="vehicle-required-note">* Required</span>
          </div>

          <div className="vehicle-create-grid">
            <Field id="fleetNumber" label="Current GG number">
              <input
                id="fleetNumber"
                name="fleetNumber"
                type="text"
                autoComplete="off"
                maxLength={20}
              />
            </Field>

            <Field id="replacedGgNumber" label="Replace GG number">
              <input
                id="replacedGgNumber"
                name="replacedGgNumber"
                type="text"
                autoComplete="off"
                maxLength={20}
              />
            </Field>

            <Field id="locationCode" label="Location" required>
              <select id="locationCode" name="locationCode" defaultValue="" required>
                <option value="">Select location...</option>
                {referenceData.locations.map((location) => (
                  <option key={location.code} value={location.code}>
                    {location.name} ({location.code})
                  </option>
                ))}
              </select>
            </Field>

            <Field id="gpNumber" label="GP number">
              <input id="gpNumber" name="gpNumber" type="text" autoComplete="off" maxLength={9} />
            </Field>

            <Field id="yearManufactured" label="Year manufactured" required>
              <input
                id="yearManufactured"
                name="yearManufactured"
                type="number"
                min="1900"
                max="9999"
                required
              />
            </Field>

            <Field id="makeCode" label="Make" required>
              <select
                id="makeCode"
                name="makeCode"
                value={selectedMakeCode || ""}
                required
                onChange={(event) => {
                  setSelectedMakeCode(Number(event.target.value) || 0);
                  setSelectedModelCode(0);
                }}
              >
                <option value="">Select make...</option>
                {referenceData.makes.map((make) => (
                  <option key={make.code} value={make.code}>
                    {make.name} ({make.code})
                  </option>
                ))}
              </select>
            </Field>

            <Field id="modelCode" label="Model" required>
              <select
                id="modelCode"
                name="modelCode"
                value={selectedModelCode || ""}
                disabled={selectedMakeCode <= 0}
                required
                onChange={(event) => setSelectedModelCode(Number(event.target.value) || 0)}
              >
                <option value="">
                  {selectedMakeCode > 0 ? "Select model..." : "Select make first..."}
                </option>
                {availableModels.map((model) => (
                  <option key={model.code} value={model.code}>
                    {model.name} ({model.code})
                  </option>
                ))}
              </select>
            </Field>

            <Field id="colourPreset" label="Colour" required>
              <select
                id="colourPreset"
                name="colourPreset"
                value={colourSelection}
                required
                onChange={(event) => {
                  setColourSelection(event.target.value);
                  setSelectedColour(event.target.value === "Other" ? "" : event.target.value);
                }}
              >
                <option value="">Select colour...</option>
                <option value="Other">Other</option>
                {["Black", "Blue", "Green", "Red", "Yellow", "White"].map((colour) => (
                  <option key={colour} value={colour}>
                    {colour}
                  </option>
                ))}
              </select>
              <label className="sr-only" htmlFor="colour">
                Specify colour
              </label>
              <input
                id="colour"
                name="colour"
                type="text"
                maxLength={15}
                value={selectedColour}
                readOnly={colourSelection !== "Other"}
                placeholder="Select a colour or choose Other"
                required
                onChange={(event) => setSelectedColour(event.target.value)}
              />
            </Field>

            <Field id="engineNumber" label="Engine number" required>
              <input
                id="engineNumber"
                name="engineNumber"
                type="text"
                autoComplete="off"
                maxLength={60}
                required
              />
            </Field>

            <Field id="chassisNumber" label="VIN / chassis number" required>
              <input
                id="chassisNumber"
                name="chassisNumber"
                type="text"
                autoComplete="off"
                maxLength={60}
                required
              />
            </Field>

            <Field id="takeOnDate" label="Take-on date" required>
              <input id="takeOnDate" name="takeOnDate" type="date" defaultValue={today} required />
            </Field>

            <Field id="takeOnOdo" label="Take-on odometer (KM)" required>
              <input
                id="takeOnOdo"
                name="takeOnOdo"
                type="number"
                min="0"
                max="999999"
                step="1"
                required
              />
            </Field>

            <Field id="typeCode" label="Hire type" required>
              <select id="typeCode" name="typeCode" defaultValue="" required>
                <option value="">Select hire type...</option>
                {referenceData.types.map((type) => (
                  <option key={type.code} value={type.code}>
                    {type.name} ({type.code})
                  </option>
                ))}
              </select>
            </Field>

            <Field id="sourceCode" label="Hired from" required>
              <select id="sourceCode" name="sourceCode" defaultValue="" required>
                <option value="">Select source...</option>
                {referenceData.sources.map((source) => (
                  <option key={source.code} value={source.code}>
                    {source.name} ({source.code})
                  </option>
                ))}
              </select>
            </Field>

            <Field id="purchaseDate" label="Purchase date" required>
              <input
                id="purchaseDate"
                name="purchaseDate"
                type="date"
                defaultValue={today}
                required
              />
            </Field>

            <Field id="purchaseAmount" label="Purchase amount (R)" required>
              <input
                id="purchaseAmount"
                name="purchaseAmount"
                type="number"
                min="5000"
                max="9999999"
                step="0.01"
                required
              />
            </Field>

            <Field id="purchaseFrom" label="Purchased from" required>
              <input
                id="purchaseFrom"
                name="purchaseFrom"
                type="text"
                autoComplete="organization"
                maxLength={60}
                required
              />
            </Field>

            <Field id="invoiceNumber" label="Invoice number">
              <input
                id="invoiceNumber"
                name="invoiceNumber"
                type="text"
                autoComplete="off"
                maxLength={60}
              />
            </Field>

            <Field id="siteCode" label="Site allocation" required>
              <select id="siteCode" name="siteCode" defaultValue="" required>
                <option value="">Select site...</option>
                {referenceData.sites.map((site) => (
                  <option key={site.code} value={site.code}>
                    {site.name} ({site.code})
                  </option>
                ))}
              </select>
            </Field>
          </div>
        </section>

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
            <Field id="damagesComment" label="Damage details">
              <textarea id="damagesComment" name="damagesComment" rows={3} maxLength={355} />
            </Field>
            <Field id="fleetNotes" label="Fleet notes">
              <textarea id="fleetNotes" name="fleetNotes" rows={3} maxLength={255} />
            </Field>
            <Field id="comment" label="Capturer's comment" required>
              <textarea id="comment" name="comment" rows={3} maxLength={90} required />
            </Field>
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
            <Field id="maintenanceTypeCode" label="Maintenance type">
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
            </Field>
            <Field id="maintenanceStartDate" label="Start date">
              <input id="maintenanceStartDate" name="maintenanceStartDate" type="date" />
            </Field>
            <Field id="maintenancePeriodMonths" label="Period (months)">
              <input
                id="maintenancePeriodMonths"
                name="maintenancePeriodMonths"
                type="number"
                min="0"
                step="1"
              />
            </Field>
            <Field id="maintenanceKilos" label="Kilos for period">
              <input id="maintenanceKilos" name="maintenanceKilos" type="number" min="0" step="1" />
            </Field>
            <Field id="maintenanceValue" label="Maintenance value">
              <input
                id="maintenanceValue"
                name="maintenanceValue"
                type="number"
                min="0"
                step="0.01"
              />
            </Field>
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

        <div className="vehicle-create-actions">
          <Link className="button button-secondary" href="/vehicles">
            Cancel
          </Link>
          <button className="button button-secondary" type="button" onClick={clearForm}>
            Clear values
          </button>
          <SubmitButton />
        </div>
      </form>
    </div>
  );
}
