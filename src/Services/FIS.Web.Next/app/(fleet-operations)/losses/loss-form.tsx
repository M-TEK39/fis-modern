import Link from "next/link";

import type { LossRecord } from "@/lib/api/fleet-operations/api-losses";
import type { LossTypeRecord } from "@/lib/api/fleet-operations/api-loss-types";
import type { SiteRecord } from "@/lib/api/reference-data/api-sites";

type LossFormAction = (formData: FormData) => Promise<void>;

export type LossFormProps = {
  action: LossFormAction;
  loss?: LossRecord | null;
  lossTypes: LossTypeRecord[];
  sites: SiteRecord[];
  returnPath: string;
  mode: "create" | "edit";
  initialVmfCode?: number | null;
  initialVehicleIdentifier?: string | null;
};

function dateValue(value: string | null | undefined) {
  return value?.slice(0, 10) ?? "";
}

function numberValue(value: number | null | undefined) {
  return value === null || value === undefined ? "" : String(value);
}

function checked(value: boolean | undefined) {
  return value === undefined ? "" : value ? "Yes" : "No";
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function TextField({
  id,
  name,
  label,
  defaultValue,
  maxLength,
  required = false,
}: Readonly<{
  id: string;
  name: string;
  label: string;
  defaultValue?: string | null;
  maxLength?: number;
  required?: boolean;
}>) {
  return (
    <div className="form-field">
      <label className="form-label" htmlFor={id}>
        {label}
      </label>
      <input
        className="form-input"
        defaultValue={defaultValue ?? ""}
        id={id}
        maxLength={maxLength}
        name={name}
        required={required}
      />
    </div>
  );
}

function NumberField({
  id,
  name,
  label,
  defaultValue,
  step = "1",
}: Readonly<{
  id: string;
  name: string;
  label: string;
  defaultValue?: string | number | null;
  step?: string;
}>) {
  return (
    <div className="form-field">
      <label className="form-label" htmlFor={id}>
        {label}
      </label>
      <input
        className="form-input"
        defaultValue={defaultValue ?? ""}
        id={id}
        min="0"
        name={name}
        step={step}
        type="number"
      />
    </div>
  );
}

function ChoiceField({
  name,
  label,
  defaultValue,
}: Readonly<{ name: string; label: string; defaultValue?: boolean }>) {
  return (
    <div className="form-field">
      <span className="form-label">{label}</span>
      <select className="form-select" defaultValue={checked(defaultValue)} name={name}>
        <option value="">Not specified</option>
        <option value="Yes">Yes</option>
        <option value="No">No</option>
      </select>
    </div>
  );
}

export default function LossForm({
  action,
  loss,
  lossTypes,
  sites,
  returnPath,
  mode,
  initialVmfCode,
  initialVehicleIdentifier,
}: LossFormProps) {
  const vmfCode = loss?.vmfCode ?? initialVmfCode;
  const vehicleIdentifier = loss?.vehicleIdentifier ?? initialVehicleIdentifier;

  return (
    <form action={action} className="vehicle-status-maintenance-panel">
      <input name="returnPath" type="hidden" value={returnPath} />
      {loss ? <input name="lossCode" type="hidden" value={loss.lossCode} /> : null}
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">
            {mode === "create" ? "New loss record" : `Loss record ${loss?.lossCode ?? ""}`}
          </p>
          <h2>{mode === "create" ? "Loss details" : "Edit loss details"}</h2>
        </div>
        <span className="vehicle-required-note">* Required</span>
      </div>

      <div className="form-grid">
        <TextField
          id="loss-reference"
          name="lossReference"
          label="Loss Reference"
          defaultValue={loss?.lossReference}
          maxLength={30}
          required
        />
        <div className="form-field">
          <label className="form-label" htmlFor="vehicle-search-mode">
            Vehicle Search
          </label>
          <select
            className="form-select"
            defaultValue="GG"
            id="vehicle-search-mode"
            name="vehicleSearchMode"
          >
            <option value="GG">GG number</option>
            <option value="GP">GP / registration number</option>
          </select>
        </div>
        <TextField
          id="vehicle-identifier"
          name="vehicleIdentifier"
          label="GG / GP Number"
          defaultValue={vehicleIdentifier}
          maxLength={30}
        />
        <NumberField
          id="vmf-code"
          name="vmfCode"
          label="VMF Code (if known)"
          defaultValue={vmfCode}
        />
        {mode === "edit" ? (
          <div className="form-field">
            <span className="form-label">Vehicle</span>
            <div className="form-readonly-value">
              {valueOrDash(vehicleIdentifier)} ({valueOrDash(vmfCode)})
            </div>
          </div>
        ) : null}
        <div className="form-field">
          <label className="form-label" htmlFor="loss-date">
            Loss Date
          </label>
          <input
            className="form-input"
            defaultValue={dateValue(loss?.lossDate)}
            id="loss-date"
            name="lossDate"
            required
            type="date"
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="loss-type">
            Loss Type
          </label>
          <select
            className="form-select"
            defaultValue={numberValue(loss?.lossTypeCode)}
            id="loss-type"
            name="lossTypeCode"
          >
            <option value="">Select loss type</option>
            {lossTypes.map((item) => (
              <option key={item.lossTypeCode} value={item.lossTypeCode}>
                {valueOrDash(item.description)} ({item.lossTypeCode})
              </option>
            ))}
          </select>
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="loss-status">
            Loss Status
          </label>
          <select
            className="form-select"
            defaultValue={loss?.lossStatus ?? "Open"}
            id="loss-status"
            name="lossStatus"
          >
            <option value="Open">Open</option>
            <option value="Under Investigation">Under Investigation</option>
            <option value="Reported to SAPD">Reported to SAPD</option>
            <option value="Closed">Closed</option>
          </select>
        </div>
        <NumberField
          id="loss-amount"
          name="lossAmount"
          label="Loss Amount"
          defaultValue={loss?.lossAmount}
          step="0.01"
        />
        <NumberField
          id="department-claim"
          name="departmentClaim"
          label="Department Claim"
          defaultValue={loss?.departmentClaim}
          step="0.01"
        />
        <div className="form-field">
          <label className="form-label" htmlFor="site-code">
            Site
          </label>
          <select
            className="form-select"
            defaultValue={numberValue(loss?.siteCode)}
            id="site-code"
            name="siteCode"
          >
            <option value="">Select site</option>
            {sites
              .filter((site) => site.siteActive)
              .toSorted((left, right) =>
                (left.description ?? "").localeCompare(right.description ?? ""),
              )
              .map((site) => (
                <option key={site.siteCode} value={site.siteCode}>
                  {valueOrDash(site.description)} ({site.siteCode})
                </option>
              ))}
          </select>
        </div>
        <TextField
          id="department-contact"
          name="departmentContact"
          label="Departmental Contact"
          defaultValue={loss?.departmentContact}
          maxLength={20}
        />
        <TextField
          id="sapd"
          name="sapd"
          label="SAPD Office / Station"
          defaultValue={loss?.sapd}
          maxLength={20}
        />
        <TextField
          id="inspector"
          name="inspector"
          label="Inspector"
          defaultValue={loss?.inspector}
          maxLength={20}
        />
        <TextField
          id="case-number"
          name="caseNumber"
          label="Case Number"
          defaultValue={loss?.caseNumber}
          maxLength={20}
        />
        <TextField
          id="driver-name"
          name="driverName"
          label="Driver Name"
          defaultValue={loss?.driverName}
          maxLength={20}
        />
        <TextField
          id="hq-reference"
          name="hqReference"
          label="HQ Reference"
          defaultValue={loss?.hqReference}
          maxLength={20}
        />
        <TextField
          id="place-of-loss"
          name="placeOfLoss"
          label="Place of Loss"
          defaultValue={loss?.placeOfLoss}
          maxLength={30}
        />
        <NumberField
          id="call-reference"
          name="callReference"
          label="Call Reference"
          defaultValue={loss?.callReference}
        />
        <div className="form-field">
          <label className="form-label" htmlFor="tow-need">
            Tow Required
          </label>
          <select
            className="form-select"
            defaultValue={loss?.towNeed ?? ""}
            id="tow-need"
            name="towNeed"
          >
            <option value="">Not specified</option>
            <option value="Yes">Yes</option>
            <option value="No">No</option>
          </select>
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="reported-ggmt">
            Date Reported at GGMT
          </label>
          <input
            className="form-input"
            defaultValue={dateValue(loss?.dateReportedGgmt)}
            id="reported-ggmt"
            name="dateReportedGgmt"
            type="date"
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="reported-sapd">
            Date Reported at SAPD
          </label>
          <input
            className="form-input"
            defaultValue={dateValue(loss?.dateReportedSapd)}
            id="reported-sapd"
            name="dateReportedSapd"
            type="date"
          />
        </div>
        <TextField
          id="remarks"
          name="remarks"
          label="Remarks"
          defaultValue={loss?.remarks}
          maxLength={30}
        />
        <ChoiceField name="cancelled" label="Cancelled" defaultValue={loss?.cancelled} />
        <ChoiceField name="coverForfeit" label="Cover Forfeit" defaultValue={loss?.coverForfeit} />
        <ChoiceField name="prosecute" label="Prosecute" defaultValue={loss?.prosecute} />
        <ChoiceField
          name="compensationOrder"
          label="Compensation Order"
          defaultValue={loss?.compensationOrder}
        />
        <ChoiceField
          name="garagingAuthority"
          label="Garaging Authority"
          defaultValue={loss?.garagingAuthority}
        />
        <ChoiceField
          name="reportFromDepartment"
          label="Department Report"
          defaultValue={loss?.reportFromDepartment}
        />
      </div>

      <div className="button-row">
        <button className="button button-primary" type="submit">
          {mode === "create" ? "Save Loss" : "Update Loss"}
        </button>
        <Link className="button button-secondary" href="/Losses/MNT_Loss_GetGg.aspx">
          Cancel
        </Link>
      </div>
    </form>
  );
}
