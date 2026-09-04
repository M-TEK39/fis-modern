"use client";

import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import { loginAction, type LoginActionState } from "@/app/actions/auth";

const initialLoginState: LoginActionState = { status: "idle" };

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <button className="button button-primary button-wide" type="submit" disabled={pending}>
      {pending ? "Signing in..." : "Sign in"}
    </button>
  );
}

export default function LoginForm() {
  const [state, formAction] = useActionState<LoginActionState, FormData>(loginAction, initialLoginState);

  return (
    <form action={formAction} className="form-stack" noValidate>
      {state.status === "error" && state.message ? (
        <div className="notice notice-error" role="alert">
          <span aria-hidden="true">!</span>
          <span>{state.message}</span>
        </div>
      ) : null}

      <div className="field">
        <label htmlFor="firstName">First name</label>
        <input
          id="firstName"
          name="firstName"
          type="text"
          autoComplete="username"
          placeholder="Enter your first name"
          required
        />
      </div>

      <div className="field">
        <label htmlFor="password">Password</label>
        <input
          id="password"
          name="password"
          type="password"
          autoComplete="current-password"
          placeholder="Enter your password"
          required
        />
      </div>

      <SubmitButton />
    </form>
  );
}
