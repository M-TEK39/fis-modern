"use client";

import { useFormStatus } from "react-dom";

type UserChoice = Readonly<{ userAccessCode: number; userName: string | null }>;

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Resetting..." : "Reset Password"}
    </button>
  );
}

export default function ForcePasswordForm({
  action,
  users,
  username,
  alphabet,
}: Readonly<{
  action: (formData: FormData) => void | Promise<void>;
  users: UserChoice[];
  username: string;
  alphabet: string;
}>) {
  const selectedUserIsMissing = username && !users.some((user) => user.userName?.toLowerCase() === username.toLowerCase());

  return (
    <form className="vehicle-status-maintenance-panel" action={action}>
      <input name="alphabet" type="hidden" value={alphabet} />
      <div className="form-grid">
        <div className="field">
          <label htmlFor="force-password-username">Username</label>
          <select id="force-password-username" name="username" defaultValue={username} required>
            <option value="">Please select a Username to edit</option>
            {selectedUserIsMissing ? <option value={username}>{username} (selected)</option> : null}
            {users.map((user) => (
              <option key={user.userAccessCode} value={user.userName ?? String(user.userAccessCode)}>
                {user.userName ?? `User ${user.userAccessCode}`}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="force-password-new">New Password</label>
          <input id="force-password-new" name="newPassword" type="password" autoComplete="new-password" required />
        </div>
        <div className="field">
          <label htmlFor="force-password-confirm">Confirm Password</label>
          <input id="force-password-confirm" name="confirmPassword" type="password" autoComplete="new-password" required />
        </div>
      </div>
      <p className="muted-copy">The new password must meet the FIS password policy.</p>
      <div className="button-row">
        <SubmitButton />
        <a className="button button-secondary" href={`/UserAdmin/UserAdmin.aspx?Alphabet=${encodeURIComponent(alphabet)}`}>
          Back
        </a>
      </div>
    </form>
  );
}
