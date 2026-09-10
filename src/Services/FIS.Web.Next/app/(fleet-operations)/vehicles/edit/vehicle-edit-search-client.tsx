"use client";

import Link from "next/link";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import type {
  VehicleEditSearchAction,
  VehicleEditSearchActionState,
  VehicleEditSearchResult,
} from "@/app/(fleet-operations)/vehicles/edit/vehicle-edit-types";

const initialSearchState: VehicleEditSearchActionState = {
  status: "idle",
  results: [],
};

type VehicleEditSearchClientProps = {
  initialQuery?: string;
  initialState?: VehicleEditSearchActionState;
  searchAction: VehicleEditSearchAction;
};

function SearchSubmitButton() {
  const { pending } = useFormStatus();

  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Searching..." : "Search"}
    </button>
  );
}

function SearchResult({ result }: Readonly<{ result: VehicleEditSearchResult }>) {
  const identifyingDetails = [
    result.registrationNumber,
    result.chassisNumber,
    result.engineNumber,
    result.invoiceNumber,
  ]
    .filter(Boolean)
    .join(" · ");

  return (
    <div className="vehicle-search-result">
      <strong>{result.fleetNumber || `Vehicle ${result.vmfCode}`}</strong>
      <span>{identifyingDetails || "No additional identifying details"}</span>
      <Link className="button button-secondary" href={`/vehicles/${result.vmfCode}/edit`}>
        Edit vehicle
      </Link>
    </div>
  );
}

export default function VehicleEditSearchClient({
  initialQuery = "",
  initialState = initialSearchState,
  searchAction,
}: VehicleEditSearchClientProps) {
  const [state, formAction] = useActionState<VehicleEditSearchActionState, FormData>(
    searchAction,
    initialState,
  );

  return (
    <div className="vehicle-create-form">
      <section className="vehicle-form-section" aria-labelledby="vehicle-edit-search-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Vehicle Master maintenance</p>
            <h2 id="vehicle-edit-search-title">Find a vehicle to edit</h2>
          </div>
        </div>

        <form action={formAction} className="vehicle-quick-search-form">
          <div className="field">
            <label htmlFor="vehicleEditSearch">GG number or GP number</label>
            <input
              id="vehicleEditSearch"
              name="searchTerm"
              type="search"
              defaultValue={initialQuery}
              placeholder="Enter a GG or GP number"
              autoComplete="off"
              required
            />
          </div>
          <div className="vehicle-create-actions vehicle-quick-search-actions">
            <Link className="button button-secondary" href="/vehicles/edit">
              Clear
            </Link>
            <SearchSubmitButton />
          </div>
        </form>

        {state.status === "error" && state.message ? (
          <div className="notice notice-error" role="alert">
            <span aria-hidden="true">!</span>
            <span>{state.message}</span>
          </div>
        ) : null}

        {state.status === "success" && state.message ? (
          <p className="muted-copy" role="status">
            {state.message}
          </p>
        ) : null}

        {state.status === "success" && state.results.length === 0 ? (
          <div className="vehicle-empty-state" role="status">
            <p className="eyebrow">No matching vehicles</p>
            <p>Check the GG or GP number and search again.</p>
          </div>
        ) : null}

        {state.results.length > 0 ? (
          <div className="vehicle-search-results" aria-label="Vehicle search results">
            {state.results.map((result) => (
              <SearchResult key={result.vmfCode} result={result} />
            ))}
          </div>
        ) : null}
      </section>
    </div>
  );
}
