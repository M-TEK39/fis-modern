"use client";

import Link from "next/link";
import { useActionState, useEffect, useRef, useState } from "react";

import {
  createVehicleAction,
  searchVehicleAction,
  type CreateVehicleActionState,
  type SearchVehicleActionState,
} from "@/app/(fleet-operations)/vehicles/create/actions";
import type {
  VehicleCreateReferenceData,
  VehicleSearchResult,
} from "@/lib/api/vehicles/api-vehicle-create";

import { VehicleCreateAdditionalFields } from "./vehicle-create-additional-fields";
import { VehicleCreateIdentityFields } from "./vehicle-create-identity-fields";
import { VehicleCreateSubmitButton } from "./vehicle-create-form-ui";

const INITIAL_ACTION_STATE: CreateVehicleActionState = { status: "idle" };
const INITIAL_SEARCH_STATE: SearchVehicleActionState = { status: "idle", results: [] };
const COLOURS = ["Black", "Blue", "Green", "Red", "Yellow", "White"];

type VehicleCreateClientProps = {
  referenceData: VehicleCreateReferenceData;
  today: string;
};

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

function QuickSearch({
  state,
  formAction,
}: Readonly<{
  state: SearchVehicleActionState;
  formAction: (payload: FormData) => void;
}>) {
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
          <button className="button button-secondary" type="submit" name="intent" value="recall">
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
    INITIAL_ACTION_STATE,
  );
  const [searchState, searchAction] = useActionState<SearchVehicleActionState, FormData>(
    searchVehicleAction,
    INITIAL_SEARCH_STATE,
  );
  const formRef = useRef<HTMLFormElement>(null);
  const [selectedMakeCode, setSelectedMakeCode] = useState(0);
  const [selectedModelCode, setSelectedModelCode] = useState(0);
  const [colourSelection, setColourSelection] = useState("");
  const [selectedColour, setSelectedColour] = useState("");
  const recall = searchState.recall;

  useEffect(() => {
    if (!recall) {
      return;
    }

    const model = referenceData.models.find((item) => item.code === recall.modelCode);
    setSelectedMakeCode(model?.makeCode ?? 0);
    setSelectedModelCode(recall.modelCode ?? 0);
    const colour = recall.colour?.trim() ?? "";
    if (colour && COLOURS.includes(colour)) {
      setColourSelection(colour);
      setSelectedColour(colour);
    } else if (colour) {
      setColourSelection("Other");
      setSelectedColour(colour);
    }
  }, [recall, referenceData.models]);

  function clearForm() {
    formRef.current?.reset();
    setSelectedMakeCode(0);
    setSelectedModelCode(0);
    setColourSelection("");
    setSelectedColour("");
  }

  return (
    <div className="vehicle-create-form">
      <QuickSearch state={searchState} formAction={searchAction} />
      <form ref={formRef} action={formAction} key={recall?.chassisNumber ?? "new-capture"}>
        {state.status === "error" && state.message ? (
          <div className="notice notice-error" role="alert">
            <span aria-hidden="true">!</span>
            <span>{state.message}</span>
          </div>
        ) : null}
        <div className="notice notice-info" role="note">
          <span aria-hidden="true">i</span>
          <span>
            Enter the available GG number in the legacy format (for example GVN001G). This capture
            is saved to the pre-vehicle authorization workflow, including its legacy purchase,
            site, damage, notes, extras, and maintenance details.
          </span>
        </div>
        <VehicleCreateIdentityFields
          colourSelection={colourSelection}
          defaults={recall}
          referenceData={referenceData}
          selectedColour={selectedColour}
          selectedMakeCode={selectedMakeCode}
          selectedModelCode={selectedModelCode}
          today={today}
          onColourChange={(selection, colour) => {
            setColourSelection(selection);
            setSelectedColour(colour);
          }}
          onMakeChange={(makeCode) => {
            setSelectedMakeCode(makeCode);
            setSelectedModelCode(0);
          }}
          onModelChange={setSelectedModelCode}
        />
        <VehicleCreateAdditionalFields defaults={recall} referenceData={referenceData} />
        <div className="vehicle-create-actions">
          <Link className="button button-secondary" href="/vehicles">
            Cancel
          </Link>
          <button className="button button-secondary" type="button" onClick={clearForm}>
            Clear values
          </button>
          <VehicleCreateSubmitButton />
        </div>
      </form>
    </div>
  );
}
