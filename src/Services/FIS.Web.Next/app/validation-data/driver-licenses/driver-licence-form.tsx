"use client";

import Link from "next/link";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import type { DriverLicenceActionState } from "@/app/validation-data/driver-licenses/actions";
import type { DriverLicenceRecord } from "@/lib/api-driver-licences";

type DriverLicenceAction = (previousState: DriverLicenceActionState, formData: FormData) => Promise<DriverLicenceActionState>;

type DriverLicenceFormProps = {
  action: DriverLicenceAction;
  licence: DriverLicenceRecord;
  mode: "create" | "update";
};

const initialState: DriverLicenceActionState = { status: "idle" };

function SubmitButton({ mode }: Readonly<{ mode: "create" | "update" }>) {
  const { pending } = useFormStatus();
  return <button className="button button-primary" type="submit" disabled={pending}>{pending ? "Saving..." : mode === "create" ? "Add Driver Licence" : "Update Driver Licence"}</button>;
}

export default function DriverLicenceForm({ action, licence, mode }: DriverLicenceFormProps) {
  const [state, formAction] = useActionState(action, initialState);

  return <form action={formAction} className="vehicle-create-form">
    {state.status === "error" && state.message ? <div className="notice notice-error" role="alert"><span aria-hidden="true">!</span><span>{state.message}</span></div> : null}
    {mode === "update" ? <input name="licenceCode" type="hidden" value={licence.licenceCode} readOnly /> : null}
    <section className="vehicle-form-section" aria-labelledby="driver-licence-details-title">
      <div className="vehicle-form-section-header"><div><p className="eyebrow">Legacy licence validation</p><h2 id="driver-licence-details-title">Driver licence details</h2></div><span className="vehicle-required-note">* Required</span></div>
      <div className="field-grid"><div className="field"><label htmlFor="description">Description <span aria-hidden="true">*</span><span className="sr-only"> required</span></label><input id="description" name="description" type="text" maxLength={30} defaultValue={licence.description ?? ""} required /></div></div>
      <p className="muted-copy">The description is stored in the legacy <code>driver_licence</code> table and remains available to vehicle model validation.</p>
    </section>
    {mode === "update" ? <section className="vehicle-form-section" aria-labelledby="driver-licence-audit-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Audit</p><h2 id="driver-licence-audit-title">Record history</h2></div></div><dl className="status-maintenance-details"><div><dt>Licence code</dt><dd>{licence.licenceCode}</dd></div><div><dt>Created</dt><dd>{licence.dateCreated?.slice(0, 10) ?? "-"}</dd></div><div><dt>Last updated</dt><dd>{licence.dateUpdated?.slice(0, 10) ?? "-"}</dd></div></dl></section> : null}
    <div className="button-row"><SubmitButton mode={mode} /><Link className="button button-secondary" href="/Validation/MNT_DriversLicence.aspx">Cancel</Link></div>
  </form>;
}
