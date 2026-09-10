"use client";

import Link from "next/link";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import type { LicenseFeeActionState } from "@/app/(administration)/validation-data/license-fees/actions";
import type { LicenseFeeRecord } from "@/lib/api/reference-data/api-license-fees";

type LicenseFeeAction = (
  previousState: LicenseFeeActionState,
  formData: FormData,
) => Promise<LicenseFeeActionState>;
type LicenseFeeFormProps = {
  action: LicenseFeeAction;
  fee: LicenseFeeRecord;
  mode: "create" | "update";
};
const initialState: LicenseFeeActionState = { status: "idle" };

function inputValue(value: string | number | null | undefined) {
  return value === null || value === undefined ? "" : String(value);
}

function SubmitButton({ mode }: Readonly<{ mode: "create" | "update" }>) {
  const { pending } = useFormStatus();
  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Saving..." : mode === "create" ? "Add Licence Fee" : "Update Licence Fee"}
    </button>
  );
}

function displayDate(value: string | null) {
  return value ? value.replace("T", " ").slice(0, 19) : "-";
}

export default function LicenseFeeForm({ action, fee, mode }: LicenseFeeFormProps) {
  const [state, formAction] = useActionState(action, initialState);
  return (
    <form action={formAction} className="vehicle-create-form">
      {state.status === "error" && state.message ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>{state.message}</span>
        </div>
      ) : null}
      {mode === "update" ? (
        <input name="licenceFeeCode" type="hidden" value={fee.licenceFeeCode} readOnly />
      ) : null}
      <section className="vehicle-form-section" aria-labelledby="license-fee-details-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy validation</p>
            <h2 id="license-fee-details-title">Licence fee details</h2>
          </div>
          <span className="vehicle-required-note">* Required</span>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="description">
              Licence description <span aria-hidden="true">*</span>
              <span className="sr-only"> required</span>
            </label>
            <input
              id="description"
              name="description"
              type="text"
              maxLength={50}
              defaultValue={inputValue(fee.description)}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="fee">Yearly tariff</label>
            <input
              id="fee"
              name="fee"
              type="number"
              min={0}
              step="0.01"
              defaultValue={inputValue(fee.fee)}
            />
          </div>
        </div>
        <p className="muted-copy">
          The original client schema stores this record in <code>licence_fees</code>; the expanded
          schema uses <code>licence_fee</code>. The API selects the available authoritative source.
        </p>
      </section>
      {mode === "update" ? (
        <section className="vehicle-form-section" aria-labelledby="license-fee-audit-title">
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Audit</p>
              <h2 id="license-fee-audit-title">Record history</h2>
            </div>
          </div>
          <dl className="status-maintenance-details">
            <div>
              <dt>Licence fee code</dt>
              <dd>{fee.licenceFeeCode}</dd>
            </div>
            <div>
              <dt>Created</dt>
              <dd>{displayDate(fee.dateCreated)}</dd>
            </div>
            <div>
              <dt>Last updated</dt>
              <dd>{displayDate(fee.dateUpdated)}</dd>
            </div>
          </dl>
        </section>
      ) : null}
      <div className="button-row">
        <SubmitButton mode={mode} />
        <Link className="button button-secondary" href="/Validation/MNT_Licence_Fees.aspx">
          Cancel
        </Link>
      </div>
    </form>
  );
}
