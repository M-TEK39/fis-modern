"use client";

import type { Dispatch, SetStateAction } from "react";

import type { VehicleStatusActionState } from "./actions";
import StatusMaintenanceSearchSubmitButton from "./status-maintenance-search-submit-button";

export default function StatusMaintenanceSearchPanel({
  searchAction,
  searchMode,
  setSearchMode,
  initialSearchTerm,
  searchState,
}: Readonly<{
  searchAction: (payload: FormData) => void;
  searchMode: string;
  setSearchMode: Dispatch<SetStateAction<string>>;
  initialSearchTerm: string;
  searchState: VehicleStatusActionState;
}>) {
  return (
    <section className="vehicle-form-section" aria-labelledby="status-search-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Vehicle status</p>
          <h2 id="status-search-title">Select the vehicle for status management</h2>
        </div>
      </div>
      <form action={searchAction} className="status-maintenance-search-form">
        <div
          className="status-maintenance-search-modes"
          role="radiogroup"
          aria-label="Vehicle search mode"
        >
          <label className="vehicle-checkbox-label">
            <input
              type="radio"
              name="searchMode"
              value="GG"
              checked={searchMode === "GG"}
              onChange={() => setSearchMode("GG")}
            />
            GG
          </label>
          <label className="vehicle-checkbox-label">
            <input
              type="radio"
              name="searchMode"
              value="GP"
              checked={searchMode === "GP"}
              onChange={() => setSearchMode("GP")}
            />
            GP
          </label>
        </div>
        <div className="field status-maintenance-search-field">
          <label htmlFor="statusSearchTerm">GG number or GP number</label>
          <input
            id="statusSearchTerm"
            name="searchTerm"
            type="search"
            defaultValue={initialSearchTerm}
            placeholder={searchMode === "GP" ? "Enter a GP number" : "Enter a GG number"}
            autoComplete="off"
            required
          />
        </div>
        <StatusMaintenanceSearchSubmitButton />
      </form>
      {searchState.status === "error" && searchState.message ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>{searchState.message}</span>
        </div>
      ) : null}
      {searchState.status === "success" && searchState.message ? (
        <p className="muted-copy" role="status">
          {searchState.message}
        </p>
      ) : null}
    </section>
  );
}
