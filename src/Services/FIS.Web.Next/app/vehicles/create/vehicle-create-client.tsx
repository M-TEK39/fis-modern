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
            {[result.registrationNumber, result.chassisNumber, result.engineNumber, result.invoiceNumber]
              .filter(Boolean)
              .join(" · ") || "No additional identifying details"}
          </span>
        </div>
      ))}
    </div>
  );
}

function QuickSearch() {
  const [state, formAction] = useActionState<SearchVehicleActionState, FormData>(searchVehicleAction, initialSearchStatus);

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
          <button className="button button-secondary" type="button" disabled title="Pending capture recall is not supported by the current API">
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
      {state.status === "success" && state.message ? <p className="muted-copy">{state.message}</p> : null}
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

  const availableModels = useMemo(
    () => referenceData.models.filter((model) => model.makeCode === selectedMakeCode),
    [referenceData.models, selectedMakeCode],
  );
  const selectedModel = referenceData.models.find((model) => model.code === selectedModelCode);

  function clearForm() {
    formRef.current?.reset();
    setSelectedMakeCode(0);
    setSelectedModelCode(0);
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
          This Next.js slice persists the fields supported by the current vehicle API. Site allocation, purchased-from and
          invoice details, damages, fleet notes, capturer comments, extras, and maintenance options are deferred until the
          API supports their persistence.
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
          <Field id="fleetNumber" label="Current GG number" required>
            <input id="fleetNumber" name="fleetNumber" type="text" autoComplete="off" required />
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

          <Field id="registrationNumber" label="GP number" required>
            <input id="registrationNumber" name="registrationNumber" type="text" autoComplete="off" required />
          </Field>

          <Field id="yearManufactured" label="Year manufactured" required>
            <input id="yearManufactured" name="yearManufactured" type="number" min="1900" max="9999" required />
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
              <option value="">{selectedMakeCode > 0 ? "Select model..." : "Select make first..."}</option>
              {availableModels.map((model) => (
                <option key={model.code} value={model.code}>
                  {model.name} ({model.code})
                </option>
              ))}
            </select>
            <input name="typeCode" type="hidden" value={selectedModel?.typeCode || 1} readOnly />
          </Field>

          <Field id="colour" label="Colour" required>
            <input id="colour" name="colour" type="text" required />
          </Field>

          <Field id="engineNumber" label="Engine number" required>
            <input id="engineNumber" name="engineNumber" type="text" autoComplete="off" required />
          </Field>

          <Field id="chassisNumber" label="VIN / chassis number" required>
            <input id="chassisNumber" name="chassisNumber" type="text" autoComplete="off" required />
          </Field>

          <Field id="takeOnDate" label="Take-on date" required>
            <input id="takeOnDate" name="takeOnDate" type="date" defaultValue={today} required />
          </Field>

          <Field id="takeOnOdo" label="Take-on odometer (KM)" required>
            <input id="takeOnOdo" name="takeOnOdo" type="number" min="0" step="1" required />
          </Field>

          <Field id="tare" label="Tare (kg)" required>
            <input id="tare" name="tare" type="number" min="0" step="1" required />
          </Field>

          <Field id="gvm" label="GVM (kg)">
            <input id="gvm" name="gvm" type="number" min="0" step="1" />
          </Field>

          <Field id="purchaseDate" label="Purchase date" required>
            <input id="purchaseDate" name="purchaseDate" type="date" defaultValue={today} required />
          </Field>

          <Field id="purchaseAmount" label="Purchase amount (R)" required>
            <input id="purchaseAmount" name="purchaseAmount" type="number" min="0" step="0.01" required />
          </Field>

          <Field id="ifmsVehicleRegisterNumber" label="IFMS vehicle register number">
            <input id="ifmsVehicleRegisterNumber" name="ifmsVehicleRegisterNumber" type="text" autoComplete="off" />
          </Field>

          <Field id="natisModelNumber" label="NATIS model number">
            <input id="natisModelNumber" name="natisModelNumber" type="text" autoComplete="off" />
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
          <span className="muted-copy">The vehicle will be available for authorization after submission.</span>
        </div>
        <input name="statusCode" type="hidden" value="1" readOnly />
      </section>

      <section className="vehicle-form-section" aria-labelledby="vehicle-tariff-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Optional calculation</p>
            <h2 id="vehicle-tariff-title">Tariff components</h2>
          </div>
        </div>
        <label className="vehicle-checkbox-label">
          <input name="recalculateTariff" type="checkbox" />
          Recalculate vehicle tariff components on submit
        </label>
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
