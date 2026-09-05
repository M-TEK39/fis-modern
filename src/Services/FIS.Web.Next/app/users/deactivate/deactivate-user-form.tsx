"use client";

import { useFormStatus } from "react-dom";

function ActionButton({ operation, children }: Readonly<{ operation: string; children: React.ReactNode }>) {
  const { pending } = useFormStatus();

  return (
    <button className="button button-secondary" type="submit" name="operation" value={operation} disabled={pending}>
      {children}
    </button>
  );
}

export default function DeactivateUserForm({
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
        <label htmlFor="deactivate-user-username">Username</label>
        <input
          id="deactivate-user-username"
          name="username"
          type="text"
          defaultValue={username}
          autoComplete="username"
          placeholder="Start typing a valid username"
          required
        />
        <p className="muted-copy">Choose the account action to apply to the selected user.</p>
      </div>
      <div className="button-row">
        <ActionButton operation="deactivate">De-Activate User</ActionButton>
        <ActionButton operation="deactivate-expired">De-ActivateUser Expired Password</ActionButton>
        <ActionButton operation="activate">Activate User</ActionButton>
        <a className="button button-secondary" href={`/UserAdmin/UserAdmin.aspx?Alphabet=${encodeURIComponent(alphabet)}`}>
          Back
        </a>
      </div>
    </form>
  );
}
