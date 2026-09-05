"use client";

import Link from "next/link";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import type { LossTypeActionState } from "@/app/validation-data/loss-types/actions";
import type { LossTypeRecord } from "@/lib/api-loss-types";

type LossTypeAction = (
  previousState: LossTypeActionState,
  formData: FormData,
) => Promise<LossTypeActionState>;

type LossTypeFormProps = {
  action: LossTypeAction;
  lossType: LossTypeRecord;
  mode: "create" | "update";
};

const initialState: LossTypeActionState = { status: "idle" };

function SubmitButton({ mode }: Readonly<{ mode: "create" | "update" }>) {
  const { pending } = useFormStatus();
  return <button className="button button-primary" type="submit" disabled={pending}>{pending ? "Saving..." : mode === "create" ? "Add Loss Description" : "Update Loss Description"}</button>;
}

export default function LossTypeForm({ action, lossType, mode }: LossTypeFormProps) {
  const [state, formAction] = useActionState(action, initialState);

  return <form action={formAction} className="vehicle-create-form">
    {state.status === "error" && state.message ? <div className="notice notice-error" role="alert"><span aria-hidden="true">!</span><span>{state.message}</span></div> : null}
    {mode === "update" ? <input name="lossTypeCode" type="hidden" value={lossType.lossTypeCode} readOnly /> : null}
    <section className="vehicle-form-section" aria-labelledby="loss-type-details-title">
      <div className="vehicle-form-section-header"><div><p className="eyebrow">Legacy loss validation</p><h2 id="loss-type-details-title">Loss description details</h2></div><span className="vehicle-required-note">* Required</span></div>
      <div className="field-grid"><div className="field"><label htmlFor="description">Loss description <span aria-hidden="true">*</span><span className="sr-only"> required</span></label><input id="description" name="description" type="text" maxLength={30} defaultValue={lossType.description ?? ""} required /></div></div>
      <p className="muted-copy">The description is stored in the legacy <code>Loss_type</code> table and remains available to loss/theft workflows.</p>
    </section>
    {mode === "update" ? <section className="vehicle-form-section" aria-labelledby="loss-type-audit-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Audit</p><h2 id="loss-type-audit-title">Record history</h2></div></div><dl className="status-maintenance-details"><div><dt>Loss type code</dt><dd>{lossType.lossTypeCode}</dd></div><div><dt>Created</dt><dd>{lossType.dateCreated?.slice(0, 10) ?? "-"}</dd></div><div><dt>Last updated</dt><dd>{lossType.dateUpdated?.slice(0, 10) ?? "-"}</dd></div></dl></section> : null}
    <div className="button-row"><SubmitButton mode={mode} /><Link className="button button-secondary" href="/Validation/MNT_Loss_Type.aspx">Cancel</Link></div>
  </form>;
}
