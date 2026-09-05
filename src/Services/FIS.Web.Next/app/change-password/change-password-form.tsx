"use client";

import Link from "next/link";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import { changePasswordAction, type ChangePasswordActionState } from "@/app/actions/auth";

const initialState: ChangePasswordActionState = { status: "idle" };

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <button className="button button-primary button-wide" type="submit" disabled={pending}>
      {pending ? "Updating..." : "Change password"}
    </button>
  );
}

export default function ChangePasswordForm({ passwordChangeRequired }: { passwordChangeRequired: boolean }) {
  const [state, formAction] = useActionState<ChangePasswordActionState, FormData>(changePasswordAction, initialState);

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
      {state.status === "error" && state.message ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>{state.message}</span>
        </div>
      ) : null}

      <div className="field">
        <label htmlFor="current-password">Current password</label>
        <input
          id="current-password"
          name="currentPassword"
          type="password"
          autoComplete="current-password"
          placeholder="Enter your current password"
          required
        />
      </div>

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
        {passwordChangeRequired
          ? "Your password has expired. Use at least 8 characters with uppercase, lowercase, a number, and a special character."
          : "Use at least 8 characters with uppercase, lowercase, a number, and a special character."}
      </p>
      <SubmitButton />
    </form>
  );
}
