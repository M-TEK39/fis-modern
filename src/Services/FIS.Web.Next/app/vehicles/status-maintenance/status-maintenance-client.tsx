"use client";

import Link from "next/link";
import { useActionState, useMemo, useState } from "react";
import { useFormStatus } from "react-dom";

import {
  changeVehicleStatusAction,
  searchVehicleStatusAction,
  type VehicleStatusActionState,
} from "@/app/vehicles/status-maintenance/actions";
import type {
  VehicleStatusOption,
  VehicleStatusSite,
  VehicleStatusVehicle,
} from "@/lib/api-vehicle-status";

const SOLD_STATUS_CODE = 5;
const STOLEN_STATUS_CODE = 4;
const SEARCH_PAGE_SIZE = 12;

const initialSearchState: VehicleStatusActionState = {
  status: "idle",
  results: [],
};

type StatusMaintenanceClientProps = {
  initialSearchTerm: string;
  initialVehicle: VehicleStatusVehicle | null;
  initialSites: VehicleStatusSite[];
  initialUpdated: boolean;
  initialReturnUrl: string;
  statusOptions: readonly VehicleStatusOption[];
};

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function formatDate(value: string | null | undefined) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value.slice(0, 10);
  }

  return `${date.getUTCFullYear()}/${String(date.getUTCMonth() + 1).padStart(2, "0")}/${String(date.getUTCDate()).padStart(2, "0")}`;
}

function formatDateTime(value: string | null | undefined) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value.slice(0, 16).replace("T", " ");
  }

  return `${formatDate(value)} ${String(date.getUTCHours()).padStart(2, "0")}:${String(date.getUTCMinutes()).padStart(2, "0")}`;
}

function todayInputValue() {
  return new Date().toISOString().slice(0, 10);
}

function SearchSubmitButton() {
  const { pending } = useFormStatus();

  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Searching..." : "Submit"}
    </button>
  );
}

function StatusSubmitButton() {
  const { pending } = useFormStatus();

  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Updating..." : "Update Status"}
    </button>
  );
}

function vehicleLink(vmfCode: number, returnUrl: string) {
  const query = new URLSearchParams({ vmfCode: String(vmfCode) });
  if (returnUrl) {
    query.set("returnUrl", returnUrl);
  }

  return `/vehicles/status-maintenance?${query.toString()}`;
}

function VehicleDetails({ vehicle }: Readonly<{ vehicle: VehicleStatusVehicle }>) {
  const rows = [
    ["GG Number", vehicle.fleetNumber],
    ["Registration Number", vehicle.registrationNumber],
    ["Make & Model", vehicle.modelName],
    ["Year Manufactured", vehicle.yearManufactured],
    ["Colour", vehicle.colour],
    ["VIN/Chassis Number", vehicle.chassisNumber],
    ["Engine Number", vehicle.engineNumber],
    ["Hired From", vehicle.hiredFrom],
    ["Hire Type", vehicle.typeName],
    ["Location", vehicle.locationDescription || vehicle.locationCode],
    ["Current Status", vehicle.statusDescription || vehicle.statusCode],
  ] as const;

  return (
    <section className="status-maintenance-panel" aria-labelledby="vehicle-information-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Selected vehicle</p>
          <h2 id="vehicle-information-title">Vehicle Information</h2>
        </div>
      </div>
      <dl className="status-maintenance-details">
        {rows.map(([label, value]) => (
          <div key={label}>
            <dt>{label}</dt>
            <dd>{valueOrDash(value)}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}

function VehicleHistory({ vehicle }: Readonly<{ vehicle: VehicleStatusVehicle }>) {
  return (
    <section className="status-maintenance-panel" aria-labelledby="vehicle-history-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Status record</p>
          <h2 id="vehicle-history-title">Vehicle Status History</h2>
        </div>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table status-maintenance-table">
          <thead>
            <tr>
              <th>Status</th>
              <th>Start Date</th>
              <th>End Date</th>
              <th>Capture Date</th>
              <th>Odometer</th>
              <th>User Name</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td>{valueOrDash(vehicle.statusDescription || vehicle.statusCode)}</td>
              <td>{formatDate(vehicle.statusDate)}</td>
              <td>-</td>
              <td>{formatDateTime(vehicle.statusDate)}</td>
              <td>{valueOrDash(vehicle.currentOdo)}</td>
              <td>API current record</td>
            </tr>
          </tbody>
        </table>
      </div>
      <p className="status-maintenance-note" role="note">
        The current C# API exposes the current status but not the full legacy status-history query.
        This row is a current-record snapshot, not a complete history.
      </p>
    </section>
  );
}

function SearchResults({
  results,
  returnUrl,
}: Readonly<{ results: VehicleStatusVehicle[]; returnUrl: string }>) {
  const [page, setPage] = useState(1);
  const totalPages = Math.max(1, Math.ceil(results.length / SEARCH_PAGE_SIZE));
  const visibleResults = results.slice((page - 1) * SEARCH_PAGE_SIZE, page * SEARCH_PAGE_SIZE);

  if (results.length === 0) {
    return null;
  }

  return (
    <section className="status-maintenance-panel" aria-labelledby="status-search-results-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Vehicle search</p>
          <h2 id="status-search-results-title">Select Vehicle ({results.length})</h2>
        </div>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table status-maintenance-table">
          <thead>
            <tr>
              <th>GG Number</th>
              <th>Registration</th>
              <th>Current Status</th>
              <th>Status Date</th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>
            {visibleResults.map((vehicle) => (
              <tr key={vehicle.vmfCode}>
                <td>{valueOrDash(vehicle.fleetNumber)}</td>
                <td>{valueOrDash(vehicle.registrationNumber)}</td>
                <td>{valueOrDash(vehicle.statusDescription || vehicle.statusCode)}</td>
                <td>{formatDate(vehicle.statusDate)}</td>
                <td>
                  <Link
                    className="button button-secondary button-small"
                    href={vehicleLink(vehicle.vmfCode, returnUrl)}
                  >
                    Manage status
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {totalPages > 1 ? (
        <nav className="vehicle-pagination" aria-label="Vehicle status search results">
          <button
            className="vehicle-pagination-button"
            type="button"
            disabled={page <= 1}
            onClick={() => setPage((current) => Math.max(1, current - 1))}
          >
            Previous
          </button>
          <span className="vehicle-pagination-meta" aria-live="polite">
            Page {page} of {totalPages}
          </span>
          <button
            className="vehicle-pagination-button"
            type="button"
            disabled={page >= totalPages}
            onClick={() => setPage((current) => Math.min(totalPages, current + 1))}
          >
            Next
          </button>
        </nav>
      ) : null}
    </section>
  );
}

export default function StatusMaintenanceClient({
  initialSearchTerm,
  initialVehicle,
  initialSites,
  initialUpdated,
  initialReturnUrl,
  statusOptions,
}: StatusMaintenanceClientProps) {
  const [searchState, searchAction] = useActionState(searchVehicleStatusAction, initialSearchState);
  const [statusState, statusAction] = useActionState(changeVehicleStatusAction, {
    status: "idle",
  } satisfies VehicleStatusActionState);
  const [searchMode, setSearchMode] = useState("GG");
  const [selectedStatusCode, setSelectedStatusCode] = useState<number | null>(null);
  const [effectiveDate] = useState(todayInputValue);
  const [showSoldFields, setShowSoldFields] = useState(false);
  const [showStolenSite, setShowStolenSite] = useState(false);

  const nextStatuses = useMemo(
    () => statusOptions.filter((status) => status.code !== initialVehicle?.statusCode),
    [initialVehicle?.statusCode, statusOptions],
  );

  function selectStatus(code: number) {
    setSelectedStatusCode(code);
    setShowSoldFields(code === SOLD_STATUS_CODE);
    setShowStolenSite(code === STOLEN_STATUS_CODE);
  }

  return (
    <div className="vehicle-create-form status-maintenance-content">
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
          <SearchSubmitButton />
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

      <SearchResults
        key={(searchState.results ?? []).map((vehicle) => vehicle.vmfCode).join(",")}
        results={searchState.results ?? []}
        returnUrl={initialReturnUrl}
      />

      {initialUpdated ? (
        <div className="notice notice-success" role="status">
          Vehicle status updated successfully.
        </div>
      ) : null}

      {initialVehicle ? (
        <div className="status-maintenance-workspace">
          <div className="status-maintenance-column">
            <VehicleDetails vehicle={initialVehicle} />
            <VehicleHistory vehicle={initialVehicle} />
          </div>

          <div className="status-maintenance-column">
            <section className="status-maintenance-panel" aria-labelledby="next-statuses-title">
              <div className="vehicle-form-section-header">
                <div>
                  <p className="eyebrow">Status transition</p>
                  <h2 id="next-statuses-title">Next Statuses Available</h2>
                </div>
              </div>
              {nextStatuses.length === 0 ? (
                <p className="status-maintenance-note">
                  No follow-on statuses found in the status catalog.
                </p>
              ) : (
                <form action={statusAction} className="status-maintenance-form">
                  <input type="hidden" name="vmfCode" value={initialVehicle.vmfCode} />
                  <input type="hidden" name="currentStatusCode" value={initialVehicle.statusCode} />
                  <input type="hidden" name="returnUrl" value={initialReturnUrl} />
                  <div className="status-maintenance-radio-list">
                    {nextStatuses.map((status) => (
                      <label key={status.code}>
                        <input
                          type="radio"
                          name="newStatusCode"
                          value={status.code}
                          checked={selectedStatusCode === status.code}
                          onChange={() => selectStatus(status.code)}
                        />
                        {status.description}
                      </label>
                    ))}
                  </div>

                  <div className="field">
                    <label htmlFor="effectiveDate">Effective From</label>
                    <input
                      id="effectiveDate"
                      name="effectiveDate"
                      type="date"
                      defaultValue={effectiveDate}
                      required
                    />
                  </div>

                  <div className="field">
                    <label htmlFor="endOdometer">Odometer</label>
                    <input
                      id="endOdometer"
                      name="endOdometer"
                      type="number"
                      min="0"
                      defaultValue={initialVehicle.currentOdo ?? undefined}
                    />
                  </div>

                  <div className="field">
                    <label htmlFor="comments">Comments</label>
                    <textarea id="comments" name="comments" rows={4} maxLength={2000} />
                  </div>

                  {showStolenSite ? (
                    <div className="field">
                      <label htmlFor="siteCode">Book Under Site</label>
                      <select
                        id="siteCode"
                        name="siteCode"
                        defaultValue={initialVehicle.siteCode ?? ""}
                        required
                      >
                        <option value="">Select site</option>
                        {initialSites.map((site) => (
                          <option key={site.code} value={site.code}>
                            {site.description} ({site.code})
                          </option>
                        ))}
                      </select>
                    </div>
                  ) : null}

                  {showSoldFields ? (
                    <div className="status-maintenance-sold-fields">
                      <h3>Sold Information</h3>
                      <p className="status-maintenance-note" role="note">
                        Sold fields are required by the legacy flow, but the current C# status
                        endpoint cannot persist them. The update is blocked until that API contract
                        is extended.
                      </p>
                      <div className="field">
                        <label htmlFor="soldAmount">Sold Amount</label>
                        <input
                          id="soldAmount"
                          name="soldAmount"
                          type="number"
                          min="0"
                          step="0.01"
                        />
                      </div>
                      <div className="field">
                        <label htmlFor="soldDate">Sold Date</label>
                        <input id="soldDate" name="soldDate" type="date" />
                      </div>
                      <div className="field">
                        <label htmlFor="soldTo">Sold To</label>
                        <input id="soldTo" name="soldTo" type="text" maxLength={60} />
                      </div>
                    </div>
                  ) : null}

                  <p className="status-maintenance-note" role="note">
                    The current endpoint persists the status and effective date, and applies its
                    stolen-vehicle site side effect. Odometer and general comments are not persisted
                    by this API contract.
                  </p>

                  {statusState.status === "error" && statusState.message ? (
                    <div className="notice notice-error" role="alert">
                      <span aria-hidden="true">!</span>
                      <span>{statusState.message}</span>
                    </div>
                  ) : null}
                  <div className="button-row">
                    <StatusSubmitButton />
                    <Link
                      className="button button-secondary"
                      href={initialReturnUrl || "/vehicles"}
                    >
                      Return to Previous Page
                    </Link>
                  </div>
                </form>
              )}
            </section>
          </div>
        </div>
      ) : null}
    </div>
  );
}
