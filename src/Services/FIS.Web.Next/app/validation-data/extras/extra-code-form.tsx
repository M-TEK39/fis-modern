"use client";

import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import type { ExtraCodeActionState } from "@/app/validation-data/extras/actions";

type ExtraCodeAction = (
  previousState: ExtraCodeActionState,
  formData: FormData,
) => Promise<ExtraCodeActionState>;

const initialState: ExtraCodeActionState = { status: "idle" };

function SubmitButton() {
  const { pending } = useFormStatus();
  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Saving..." : "Add Extra"}
    </button>
  );
}

export default function ExtraCodeForm({ action }: Readonly<{ action: ExtraCodeAction }>) {
  const [state, formAction] = useActionState(action, initialState);
  return (
    <form action={formAction} className="vehicle-create-form">
      {state.status === "error" && state.message ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>{state.message}</span>
        </div>
      ) : null}
      <section className="vehicle-form-section" aria-labelledby="extra-code-details-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Legacy validation</p>
            <h2 id="extra-code-details-title">Add an extra</h2>
          </div>
          <span className="vehicle-required-note">* Required</span>
        </div>
        <div className="field-grid">
          <div className="field">
            <label htmlFor="description">
              Extra description <span aria-hidden="true">*</span>
              <span className="sr-only"> required</span>
            </label>
            <input id="description" name="description" type="text" maxLength={50} required />
          </div>
        </div>
        <p className="muted-copy">
          Descriptions are stored in the legacy <code>extra_codes</code> table. Expanded audit
          fields are used automatically when the database provides them.
        </p>
      </section>
      <div className="button-row">
        <SubmitButton />
      </div>
    </form>
  );
}
