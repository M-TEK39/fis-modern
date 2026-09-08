"use client";

import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import { forgotPasswordAction, type ForgotPasswordActionState } from "@/app/actions/auth";

const initialState: ForgotPasswordActionState = { status: "idle" };

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <button className="button button-primary button-wide" type="submit" disabled={pending}>
      {pending ? "Sending..." : "Send reset link"}
    </button>
  );
}

export default function ForgotPasswordForm({
  initialIdentifier = "",
}: {
  initialIdentifier?: string;
}) {
  const [state, formAction] = useActionState<ForgotPasswordActionState, FormData>(
    forgotPasswordAction,
    initialState,
  );

  return (
    <form action={formAction} className="form-stack" noValidate>
      {state.status === "error" && state.message ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>{state.message}</span>
        </div>
      ) : null}

      {state.status === "success" && state.message ? (
        <div className="notice notice-info" role="status">
          <span aria-hidden="true">i</span>
          <span>{state.message}</span>
        </div>
      ) : null}

      <div className="field">
        <label htmlFor="identifier">Username, email, or user code</label>
        <input
          id="identifier"
          name="identifier"
          type="text"
          autoComplete="username"
          placeholder="Enter your account identifier"
          defaultValue={initialIdentifier}
          required
        />
      </div>

      <SubmitButton />
    </form>
  );
}
