"use client";

import Link from "next/link";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import { resetPasswordAction, type ForgotPasswordActionState } from "@/app/actions/auth";

const initialState: ForgotPasswordActionState = { status: "idle" };

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <button className="button button-primary button-wide" type="submit" disabled={pending}>
      {pending ? "Resetting..." : "Reset password"}
    </button>
  );
}

export default function ResetPasswordForm({ token }: { token: string }) {
  const [state, formAction] = useActionState<ForgotPasswordActionState, FormData>(
    resetPasswordAction,
    initialState,
  );

  if (state.status === "success") {
    return (
      <>
        <div className="notice notice-info" role="status">
          <span aria-hidden="true">i</span>
          <span>{state.message}</span>
        </div>
        <Link className="button button-primary button-wide" href="/login">
          Continue to sign in
        </Link>
      </>
    );
  }

  return (
    <form action={formAction} className="form-stack" noValidate>
      <input type="hidden" name="token" value={token} />

      {state.status === "error" && state.message ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>{state.message}</span>
        </div>
      ) : null}

      <div className="field">
        <label htmlFor="new-password">New password</label>
        <input
          id="new-password"
          name="newPassword"
          type="password"
          autoComplete="new-password"
          minLength={8}
          placeholder="Enter your new password"
          required
        />
      </div>

      <div className="field">
        <label htmlFor="confirm-new-password">Confirm new password</label>
        <input
          id="confirm-new-password"
          name="confirmNewPassword"
          type="password"
          autoComplete="new-password"
          minLength={8}
          placeholder="Re-enter your new password"
          required
        />
      </div>

      <p className="auth-footnote">
        Use at least 8 characters with uppercase, lowercase, a number, and a special character.
      </p>
      <SubmitButton />
    </form>
  );
}
