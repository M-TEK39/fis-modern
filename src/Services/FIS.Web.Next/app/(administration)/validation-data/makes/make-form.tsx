"use client";

import Link from "next/link";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import type { MakeActionState } from "@/app/(administration)/validation-data/makes/actions";
import type { MakeRecord } from "@/lib/api/reference-data/api-makes";

type MakeAction = (previousState: MakeActionState, formData: FormData) => Promise<MakeActionState>;

type MakeFormProps = {
  action: MakeAction;
  make: MakeRecord;
  mode: "create" | "update";
};

const initialState: MakeActionState = { status: "idle" };

function SubmitButton({ mode }: Readonly<{ mode: "create" | "update" }>) {
  const { pending } = useFormStatus();
  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Saving..." : mode === "create" ? "Add Make" : "Update Make"}
    </button>
  );
}

export default function MakeForm({ action, make, mode }: MakeFormProps) {
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
        <input name="makeCode" type="hidden" value={make.makeCode} readOnly />
      ) : null}
      <section className="vehicle-form-section" aria-labelledby="make-details-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy vehicle validation</p>
            <h2 id="make-details-title">Make details</h2>
          </div>
          <span className="vehicle-required-note">* Required</span>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="makeDescription">
              Make name <span aria-hidden="true">*</span>
              <span className="sr-only"> required</span>
            </label>
            <input
              id="makeDescription"
              name="makeDescription"
              type="text"
              maxLength={60}
              defaultValue={mode === "update" ? make.makeDescription : ""}
              required
            />
          </div>
          {mode === "update" ? (
            <div className="field">
              <label htmlFor="makeCodeDisplay">Make code</label>
              <input id="makeCodeDisplay" type="number" value={make.makeCode} readOnly />
            </div>
          ) : null}
        </div>
      </section>
      <div className="button-row">
        <SubmitButton mode={mode} />
        <Link className="button button-secondary" href="/Validation/MNT_make.aspx">
          Cancel
        </Link>
      </div>
    </form>
  );
}
