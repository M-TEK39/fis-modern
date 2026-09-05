"use client";

import { useFormStatus } from "react-dom";

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Sending..." : "Send reset link"}
    </button>
  );
}

export default function AdminForgotPasswordForm({
  action,
  username,
  alphabet,
}: Readonly<{
  action: (formData: FormData) => void | Promise<void>;
  username: string;
  alphabet: string;
}>) {
  return (
    <form className="vehicle-status-maintenance-panel" action={action}>
      <input name="alphabet" type="hidden" value={alphabet} />
      <div className="field">
        <label htmlFor="admin-reset-username">Username or user code</label>
        <input
          id="admin-reset-username"
          name="username"
          type="text"
          defaultValue={username}
          autoComplete="username"
          required
        />
        <p className="muted-copy">The reset link is sent only to the email address registered for the account.</p>
      </div>
      <div className="button-row">
        <SubmitButton />
        <a className="button button-secondary" href={`/UserAdmin/UserAdmin.aspx?Alphabet=${encodeURIComponent(alphabet)}`}>
          Back to Users
        </a>
      </div>
    </form>
  );
}
