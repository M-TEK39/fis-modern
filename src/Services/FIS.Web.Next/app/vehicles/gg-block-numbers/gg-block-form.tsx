"use client";

import { useActionState, useState } from "react";
import { useFormStatus } from "react-dom";

import type { GgBlockActionState } from "@/app/vehicles/gg-block-numbers/actions";

type GgBlockAction = (
  previousState: GgBlockActionState,
  formData: FormData,
) => Promise<GgBlockActionState>;

function SubmitButton() {
  const { pending } = useFormStatus();
  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Saving..." : "Submit"}
    </button>
  );
}

export default function GgBlockForm({ action, returnPath }: Readonly<{ action: GgBlockAction; returnPath: string }>) {
  const [state, formAction] = useActionState(action, { status: "idle" });
  const [startGgNumber, setStartGgNumber] = useState("");
  const [endGgNumber, setEndGgNumber] = useState("");

  return (
    <form action={formAction} className="vehicle-create-form">
      <input name="returnPath" type="hidden" value={returnPath} readOnly />
      <div className="form-row">
        <label className="form-label" htmlFor="startGgNumber">Start GG Block Number</label>
        <input
          className="form-input fis-uppercase"
          id="startGgNumber"
          name="startGgNumber"
          type="text"
          maxLength={7}
          inputMode="text"
          autoCapitalize="characters"
          autoComplete="off"
          required
          value={startGgNumber}
          onChange={(event) => setStartGgNumber(event.target.value.toUpperCase())}
        />
      </div>
      <div className="form-row">
        <label className="form-label" htmlFor="endGgNumber">End GG Block Number</label>
        <input
          className="form-input fis-uppercase"
          id="endGgNumber"
          name="endGgNumber"
          type="text"
          maxLength={7}
          inputMode="text"
          autoCapitalize="characters"
          autoComplete="off"
          required
          value={endGgNumber}
          onChange={(event) => setEndGgNumber(event.target.value.toUpperCase())}
        />
      </div>
      <div className="form-actions">
        <SubmitButton />
        <button className="button button-secondary" type="reset" onClick={() => { setStartGgNumber(""); setEndGgNumber(""); }}>
          Clear
        </button>
      </div>
      {state.status === "error" && state.message ? <div className="notice notice-error" role="alert"><span aria-hidden="true">!</span><span>{state.message}</span></div> : null}
    </form>
  );
}
