"use client";

import Link from "next/link";
import { useFormStatus } from "react-dom";

const SECURITY_QUESTIONS = [
  "What was your childhood nickname?",
  "What school did you attend for sixth grade?",
  "What is your mother's maiden name?",
  "In what city or town was your first job?",
  "What is your spouse's mother's maiden name?",
  "Who was your childhood hero?",
  "What are the last 5 digits of your driver's license number?",
  "What is your father's middle name?",
  "What is the name of your favorite childhood friend?",
  "What was the name of your first stuffed animal?",
  "What colour was your first car?",
];

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <button className="button button-primary" type="submit" disabled={pending}>
      {pending ? "Updating..." : "Change Password"}
    </button>
  );
}

export default function ChangePasswordQuestionForm({
  action,
  canManageOthers,
  users,
  username,
  email,
}: Readonly<{
  action: (formData: FormData) => void | Promise<void>;
  canManageOthers: boolean;
  users: ReadonlyArray<Readonly<{ userAccessCode: number; userName: string | null; email: string | null }>>;
  username: string;
  email: string;
}>) {
  const selectedUserIsMissing = username && !users.some((user) => user.userName?.toLowerCase() === username.toLowerCase());

  return (
    <form className="vehicle-status-maintenance-panel" action={action}>
      <div className="form-grid">
        <div className="field">
          <label htmlFor="change-question-username">Username</label>
          {canManageOthers ? (
            <select id="change-question-username" name="username" defaultValue={username} required>
              <option value="">Please select a Username</option>
              {selectedUserIsMissing ? <option value={username}>{username} (selected)</option> : null}
              {users.map((user) => (
                <option key={user.userAccessCode} value={user.userName ?? String(user.userAccessCode)}>
                  {user.userName ?? `User ${user.userAccessCode}`}
                </option>
              ))}
            </select>
          ) : (
            <input id="change-question-username" name="username" type="text" value={username} readOnly />
          )}
        </div>
        <div className="field">
          <label htmlFor="change-question-old-password">Old Password</label>
          <input id="change-question-old-password" name="oldPassword" type="password" autoComplete="current-password" required />
        </div>
        <div className="field">
          <label htmlFor="change-question-new-password">New Password</label>
          <input id="change-question-new-password" name="newPassword" type="password" autoComplete="new-password" minLength={8} required />
        </div>
        <div className="field">
          <label htmlFor="change-question-confirm-password">Confirm New Password</label>
          <input id="change-question-confirm-password" name="confirmNewPassword" type="password" autoComplete="new-password" minLength={8} required />
        </div>
        <div className="field form-group-full">
          <label htmlFor="change-question-security-question">New Password Question</label>
          <select id="change-question-security-question" name="securityQuestion" defaultValue="Select Question..." required>
            <option value="Select Question...">Select Question...</option>
            {SECURITY_QUESTIONS.map((question) => <option key={question} value={question}>{question}</option>)}
          </select>
        </div>
        <div className="field">
          <label htmlFor="change-question-security-answer">New Password Answer</label>
          <input id="change-question-security-answer" name="securityAnswer" type="text" autoComplete="off" required />
        </div>
        <div className="field">
          <label htmlFor="change-question-email">Email Address</label>
          <input id="change-question-email" name="email" type="email" defaultValue={email} autoComplete="email" required />
        </div>
      </div>
      <p className="muted-copy">Use at least 8 characters with uppercase, lowercase, a number, and a special character.</p>
      <div className="button-row">
        <SubmitButton />
        <Link className="button button-secondary" href="/home">Cancel</Link>
      </div>
    </form>
  );
}
