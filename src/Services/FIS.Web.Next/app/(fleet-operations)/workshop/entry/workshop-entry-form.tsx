import Link from "next/link";

import { saveWorkshopAction } from "@/app/(fleet-operations)/workshop/actions";
import type { WorkshopRecord, WorkshopVehicle } from "@/lib/api/fleet-operations/api-workshop";

function valueOrEmpty(value: string | number | null | undefined) {
  return value === null || value === undefined ? "" : String(value);
}

function dateValue(value: string | null | undefined) {
  return value?.slice(0, 10) ?? "";
}

function timeValue(value: string | null | undefined) {
  return value?.slice(0, 5) ?? "";
}

export function WorkshopEntryForm({
  record,
  vehicles,
  returnPath,
}: Readonly<{
  record: WorkshopRecord | null;
  vehicles: WorkshopVehicle[];
  returnPath: string;
}>) {
  return (
    <form className="vehicle-status-maintenance-panel" action={saveWorkshopAction}>
      <input name="returnPath" type="hidden" value={returnPath} />
      {record ? <input name="wwCode" type="hidden" value={record.wwCode} /> : null}
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{record ? "Existing record" : "New record"}</p>
          <h2>
            {record ? `Modify Workshop Entry #${record.wwCode}` : "Capture a New Workshop Entry"}
          </h2>
        </div>
      </div>
      <div className="form-grid">
        <div className="form-field form-group-full">
          <label className="form-label" htmlFor="workshop-vehicle">
            Vehicle
          </label>
          <select
            className="form-select"
            id="workshop-vehicle"
            name="vmfCode"
            defaultValue={valueOrEmpty(record?.vmfCode)}
            required
          >
            <option value="">Select vehicle...</option>
            {vehicles.map((vehicle) => (
              <option key={vehicle.vmfCode} value={vehicle.vmfCode}>
                {vehicle.fleetNumber || "-"} / {vehicle.registrationNumber || "-"} (
                {vehicle.vmfCode})
              </option>
            ))}
          </select>
          {vehicles.length === 0 ? (
            <p className="muted-copy">Search for a vehicle first, then choose it here.</p>
          ) : null}
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="workshop-receive-date">
            Receive Date
          </label>
          <input
            className="form-input"
            id="workshop-receive-date"
            name="receiveDate"
            type="date"
            defaultValue={dateValue(record?.receiveDate) || new Date().toISOString().slice(0, 10)}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="workshop-receive-time">
            Receive Time
          </label>
          <input
            className="form-input"
            id="workshop-receive-time"
            name="receiveTime"
            type="time"
            defaultValue={timeValue(record?.receiveTime)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="workshop-complete-date">
            Complete Date
          </label>
          <input
            className="form-input"
            id="workshop-complete-date"
            name="completeDate"
            type="date"
            defaultValue={dateValue(record?.completeDate)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="workshop-complete-time">
            Complete Time
          </label>
          <input
            className="form-input"
            id="workshop-complete-time"
            name="completeTime"
            type="time"
            defaultValue={timeValue(record?.completeTime)}
          />
        </div>
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          {record ? "Update" : "Save"}
        </button>
        <Link className="button button-secondary" href="/workshop/entry">
          Cancel
        </Link>
      </div>
    </form>
  );
}
