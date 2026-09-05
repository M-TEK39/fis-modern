"use client";

import { useFormStatus } from "react-dom";

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Resetting..." : "Reset Login"}
    </button>
  );
}

export default function ResetLoginForm({
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
        <label htmlFor="reset-login-username">Username</label>
        <input
          id="reset-login-username"
          name="username"
          type="text"
          defaultValue={username}
          autoComplete="username"
          placeholder="Start typing a valid username"
          required
        />
        <p className="muted-copy">Reset the login counter and unlock the selected user account.</p>
      </div>
      <div className="button-row">
        <SubmitButton />
        <a className="button button-secondary" href={`/UserAdmin/UserAdmin.aspx?Alphabet=${encodeURIComponent(alphabet)}`}>
          Back
        </a>
      </div>
    </form>
  );
}
