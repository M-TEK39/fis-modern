import type { VehicleStatusReportRow } from "@/app/(fleet-operations)/vehicles/status/status-types";

import { getVehicleLabel } from "./vehicle-status-report-utils";

export default function VehicleStatusRemarkForm({
  onCancel,
  onSubmit,
  pending,
  resolveMode,
  row,
}: Readonly<{
  onCancel: () => void;
  onSubmit: (formData: FormData) => void;
  pending: boolean;
  resolveMode: boolean;
  row: VehicleStatusReportRow;
}>) {
  return (
    <section className="vehicle-form-section" aria-labelledby="vehicle-status-remark-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Vehicle remark</p>
          <h2 id="vehicle-status-remark-title">
            {resolveMode ? "Resolve Vehicle Remark" : "Add Vehicle Remark"}
          </h2>
        </div>
      </div>
      <form action={onSubmit} className="vehicle-create-form">
        <input name="vmfCode" type="hidden" value={row.vmfCode} readOnly />
        <input name="operation" type="hidden" value={resolveMode ? "resolve" : "add"} readOnly />
        <input name="remarkId" type="hidden" value={row.remark?.remarkId ?? ""} readOnly />
        <div className="field">
          <label htmlFor="vehicle-status-remark-vehicle">Vehicle</label>
          <input id="vehicle-status-remark-vehicle" value={getVehicleLabel(row)} readOnly />
        </div>
        {!resolveMode ? (
          <div className="field">
            <label htmlFor="vehicle-status-remark-category">Remark Category</label>
            <select
              id="vehicle-status-remark-category"
              name="remarkCategory"
              defaultValue="General"
            >
              <option value="General">General</option>
              <option value="Missing">Missing</option>
              <option value="UnderInvestigation">Under Investigation</option>
              <option value="AccidentHold">Accident Hold</option>
              <option value="Other">Other</option>
            </select>
          </div>
        ) : null}
        <div className="field">
          <label htmlFor="vehicle-status-remark-text">
            {resolveMode ? "Resolution Notes" : "Remark"}
          </label>
          <textarea
            id="vehicle-status-remark-text"
            name="remarkText"
            rows={3}
            maxLength={500}
            required
          />
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit" disabled={pending}>
            {pending ? "Saving..." : resolveMode ? "Resolve Remark" : "Save Remark"}
          </button>
          <button className="button button-secondary" type="button" onClick={onCancel}>
            Cancel
          </button>
        </div>
      </form>
    </section>
  );
}
