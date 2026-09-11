"use client";

import Link from "next/link";
import { useState, useTransition } from "react";

import {
  loadRecoveredVehicleAction,
  saveRecoveredVehicleAction,
  searchRecoveredVehiclesAction,
} from "@/app/(fleet-operations)/vehicles/recovered/actions";
import type {
  RecoveredVehicleDetails,
  RecoveredVehicleSearchMode,
  RecoveredVehicleSearchResult,
  RecoveredVehicleStatusOption,
} from "@/lib/api/vehicles/api-recovered-vehicles";

type RecoveredVehicleClientProps = {
  initialSearchTerm: string;
  initialMode: RecoveredVehicleSearchMode;
  initialMatches: RecoveredVehicleSearchResult[];
  initialDetails: RecoveredVehicleDetails | null;
};

function valueOrDash(value: string | null) {
  return value || "-";
}

function dateInputValue(value: string | null) {
  return value ? value.slice(0, 10) : "";
}

function getStatusLabel(vehicle: RecoveredVehicleSearchResult) {
  return `${valueOrDash(vehicle.fleetNumber)} / ${valueOrDash(vehicle.registrationNumber)} (${vehicle.vmfCode})`;
}

function availableStatusOptions(options: readonly RecoveredVehicleStatusOption[]) {
  return options.reduce<RecoveredVehicleStatusOption[]>((result, option) => {
    if (option.code !== 4) result.push(option);
    return result;
  }, []);
}

export default function RecoveredVehicleClient({
  initialSearchTerm,
  initialMode,
  initialMatches,
  initialDetails,
}: RecoveredVehicleClientProps) {
  const [searchMode, setSearchMode] = useState<RecoveredVehicleSearchMode>(initialMode);
  const [searchTerm, setSearchTerm] = useState(initialSearchTerm);
  const [matches, setMatches] = useState(initialMatches);
  const [selectedVmfCode, setSelectedVmfCode] = useState<number | null>(
    initialDetails?.vmfCode ?? null,
  );
  const [details, setDetails] = useState<RecoveredVehicleDetails | null>(initialDetails);
  const [recoveredFleetNumber, setRecoveredFleetNumber] = useState("");
  const [dateChanged, setDateChanged] = useState(() =>
    dateInputValue(initialDetails?.previousDateChanged ?? null),
  );
  const [newStatusCode, setNewStatusCode] = useState(
    initialDetails?.vehicleStatusCode === 10 ? 1 : (initialDetails?.vehicleStatusCode ?? 1),
  );
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isPending, startTransition] = useTransition();

  const handleSearch = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const formData = new FormData(event.currentTarget);
    setError(null);
    setMessage(null);
    setDetails(null);
    setSelectedVmfCode(null);

    startTransition(async () => {
      const result = await searchRecoveredVehiclesAction(formData);
      if (result.status === "error") {
        setMatches([]);
        setError(result.message ?? "The recovered vehicle search could not be completed.");
        return;
      }

      setMatches(result.matches ?? []);
      if ((result.matches ?? []).length === 0) {
        setError("No vehicle matched the entered GG/GP number.");
      }
    });
  };

  const handleVehicleSelected = (value: string) => {
    const vmfCode = Number(value);
    setSelectedVmfCode(Number.isInteger(vmfCode) && vmfCode > 0 ? vmfCode : null);
    setDetails(null);
    setError(null);
    setMessage(null);

    if (!Number.isInteger(vmfCode) || vmfCode < 1) {
      return;
    }

    const formData = new FormData();
    formData.set("vmfCode", String(vmfCode));
    startTransition(async () => {
      const result = await loadRecoveredVehicleAction(formData);
      if (result.status === "error" || !result.details) {
        setError(result.message ?? "The recovered vehicle could not be loaded.");
        return;
      }

      setDetails(result.details);
      setRecoveredFleetNumber("");
      setDateChanged(dateInputValue(result.details.previousDateChanged));
      setNewStatusCode(
        result.details.vehicleStatusCode === 10 ? 1 : result.details.vehicleStatusCode || 1,
      );
    });
  };

  const handleSave = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!details) {
      return;
    }

    const formData = new FormData(event.currentTarget);
    setError(null);
    setMessage(null);
    startTransition(async () => {
      const result = await saveRecoveredVehicleAction(formData);
      if (result.status === "error" || !result.details) {
        setError(result.message ?? "The recovered vehicle could not be updated.");
        return;
      }

      setDetails(result.details);
      setMessage(result.message ?? "Recovered vehicle updated successfully.");
      setMatches((current) =>
        current.map((vehicle) =>
          vehicle.vmfCode === result.details?.vmfCode
            ? { ...vehicle, renumberedTo: recoveredFleetNumber, vehicleStatusCode: 10 }
            : vehicle,
        ),
      );
    });
  };

  return (
    <>
      <section className="form-card" aria-labelledby="recovered-vehicle-search-title">
        <div className="form-card-header">
          <h2 id="recovered-vehicle-search-title">Find the stolen vehicle</h2>
          <p>Search by its GG or GP number before recording the recovered GG.</p>
        </div>
        <form className="form-card-body" onSubmit={handleSearch}>
          <fieldset className="form-row">
            <legend className="form-label">Number Type</legend>
            <label className="form-radio-label">
              <input
                type="radio"
                name="mode"
                value="GG"
                checked={searchMode === "GG"}
                onChange={() => setSearchMode("GG")}
              />
              GG
            </label>
            <label className="form-radio-label">
              <input
                type="radio"
                name="mode"
                value="GP"
                checked={searchMode === "GP"}
                onChange={() => setSearchMode("GP")}
              />
              GP
            </label>
          </fieldset>
          <div className="form-row">
            <label className="form-label" htmlFor="recovered-search">
              {searchMode === "GG" ? "GG Number" : "GP Number"}
            </label>
            <div className="fis-input-group-compact">
              <input
                id="recovered-search"
                className="form-input"
                name="search"
                value={searchTerm}
                onChange={(event) => setSearchTerm(event.target.value)}
                autoComplete="off"
              />
              <button
                className="button button-secondary button-small"
                type="submit"
                disabled={isPending}
              >
                {isPending ? "Finding..." : "Find"}
              </button>
            </div>
          </div>
          <div className="form-row">
            <label className="form-label" htmlFor="recovered-match">
              Matching vehicles
            </label>
            <select
              id="recovered-match"
              className="form-select"
              value={selectedVmfCode ?? ""}
              onChange={(event) => handleVehicleSelected(event.target.value)}
              disabled={matches.length === 0 || isPending}
            >
              <option value="">Select vehicle...</option>
              {matches.map((vehicle) => (
                <option key={vehicle.vmfCode} value={vehicle.vmfCode}>
                  {getStatusLabel(vehicle)}
                </option>
              ))}
            </select>
          </div>
        </form>
      </section>

      {error ? (
        <div className="notice notice-error" role="alert">
          {error}
        </div>
      ) : null}
      {message ? (
        <div className="notice notice-success" role="status">
          {message}
        </div>
      ) : null}

      {details ? (
        <section className="form-card fis-mt-1" aria-labelledby="recovered-vehicle-details-title">
          <div className="form-card-header">
            <h2 id="recovered-vehicle-details-title">Recovered Vehicle Details</h2>
            <p>Mark the original vehicle as stolen, then create its recovered vehicle record.</p>
          </div>
          <form className="form-card-body" onSubmit={handleSave}>
            <input type="hidden" name="vmfCode" value={details.vmfCode} />
            <input type="hidden" name="newStatusCode" value={newStatusCode} />
            <div className="form-grid">
              <div className="form-field">
                <label className="form-label" htmlFor="original-gg">
                  GG Number
                </label>
                <input
                  id="original-gg"
                  className="form-input"
                  value={valueOrDash(details.fleetNumber)}
                  readOnly
                />
              </div>
              <div className="form-field">
                <label className="form-label" htmlFor="original-registration">
                  Registration Number
                </label>
                <input
                  id="original-registration"
                  className="form-input"
                  value={valueOrDash(details.registrationNumber)}
                  readOnly
                />
              </div>
              <div className="form-field">
                <label className="form-label" htmlFor="recovered-gg">
                  Recovered (new) GG number
                </label>
                <input
                  id="recovered-gg"
                  className="form-input"
                  name="recoveredFleetNumber"
                  value={recoveredFleetNumber}
                  onChange={(event) => setRecoveredFleetNumber(event.target.value.toUpperCase())}
                  maxLength={8}
                  required
                />
              </div>
              <div className="form-field">
                <label className="form-label" htmlFor="date-changed">
                  Date changed
                </label>
                <input
                  id="date-changed"
                  className="form-input"
                  name="dateChanged"
                  type="date"
                  value={dateChanged}
                  onChange={(event) => setDateChanged(event.target.value)}
                  required
                />
              </div>
              <div className="form-field">
                <label className="form-label" htmlFor="recovered-status">
                  Status of recovered (new) GG number
                </label>
                <select
                  id="recovered-status"
                  className="form-select"
                  value={newStatusCode}
                  onChange={(event) => setNewStatusCode(Number(event.target.value))}
                >
                  {availableStatusOptions(details.statusOptions).map((status) => (
                    <option key={status.code} value={status.code}>
                      {status.description}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-field">
                <span className="form-label">Renumbered to</span>
                <span className="form-input" aria-label="Renumbered to">
                  {valueOrDash(details.renumberedTo)}
                </span>
              </div>
            </div>
            {details.previousFleetNumber ? (
              <p className="muted-copy">
                Last recorded recovered GG: {details.previousFleetNumber} on{" "}
                {dateInputValue(details.previousDateChanged)}
              </p>
            ) : null}
            <div className="form-actions">
              <button
                className="button button-primary"
                type="submit"
                disabled={isPending || Boolean(details.renumberedTo)}
              >
                {isPending ? "Saving..." : "Save"}
              </button>
              <Link className="button button-secondary" href="/vehicles">
                Back
              </Link>
            </div>
          </form>
        </section>
      ) : null}
    </>
  );
}
