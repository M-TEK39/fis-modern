"use client";

import Link from "next/link";

import type { VehicleStatusActionState } from "./actions";
import StatusMaintenanceStatusSubmitButton from "./status-maintenance-status-submit-button";
import type {
  VehicleStatusOption,
  VehicleStatusSite,
  VehicleStatusVehicle,
} from "@/lib/api/vehicles/api-vehicle-status";

export default function StatusMaintenanceStatusForm({
  vehicle,
  sites,
  returnUrl,
  statusOptions,
  selectedStatusCode,
  onStatusChange,
  effectiveDate,
  showStolenSite,
  showSoldFields,
  statusAction,
  statusState,
}: Readonly<{
  vehicle: VehicleStatusVehicle;
  sites: VehicleStatusSite[];
  returnUrl: string;
  statusOptions: readonly VehicleStatusOption[];
  selectedStatusCode: number | null;
  onStatusChange: (code: number) => void;
  effectiveDate: string;
  showStolenSite: boolean;
  showSoldFields: boolean;
  statusAction: (payload: FormData) => void;
  statusState: VehicleStatusActionState;
}>) {
  if (statusOptions.length === 0) {
    return (
      <p className="status-maintenance-note">No follow-on statuses found in the status catalog.</p>
    );
  }

  return (
    <form action={statusAction} className="status-maintenance-form">
      <input type="hidden" name="vmfCode" value={vehicle.vmfCode} />
      <input type="hidden" name="currentStatusCode" value={vehicle.statusCode} />
      <input type="hidden" name="returnUrl" value={returnUrl} />
      <div className="status-maintenance-radio-list">
        {statusOptions.map((status) => (
          <label key={status.code}>
            <input
              type="radio"
              name="newStatusCode"
              value={status.code}
              checked={selectedStatusCode === status.code}
              onChange={() => onStatusChange(status.code)}
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
          defaultValue={vehicle.currentOdo ?? undefined}
        />
      </div>

      <div className="field">
        <label htmlFor="comments">Comments</label>
        <textarea id="comments" name="comments" rows={4} maxLength={2000} />
      </div>

      {showStolenSite ? (
        <div className="field">
          <label htmlFor="siteCode">Book Under Site</label>
          <select id="siteCode" name="siteCode" defaultValue={vehicle.siteCode ?? ""} required>
            <option value="">Select site</option>
            {sites.map((site) => (
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
            Sold fields are required by the legacy flow, but the current C# status endpoint cannot
            persist them. The update is blocked until that API contract is extended.
          </p>
          <div className="field">
            <label htmlFor="soldAmount">Sold Amount</label>
            <input id="soldAmount" name="soldAmount" type="number" min="0" step="0.01" />
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
        The current endpoint persists the status and effective date, and applies its stolen-vehicle
        site side effect. Odometer and general comments are not persisted by this API contract.
      </p>

      {statusState.status === "error" && statusState.message ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>{statusState.message}</span>
        </div>
      ) : null}
      <div className="button-row">
        <StatusMaintenanceStatusSubmitButton />
        <Link className="button button-secondary" href={returnUrl || "/vehicles"}>
          Return to Previous Page
        </Link>
      </div>
    </form>
  );
}
