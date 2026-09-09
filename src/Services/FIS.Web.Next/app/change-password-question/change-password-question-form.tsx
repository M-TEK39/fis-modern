"use client";

import Link from "next/link";
import { useFormStatus } from "react-dom";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

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
    <Button className="w-full" type="submit" disabled={pending}>
      {pending ? "Updating..." : "Change Password"}
    </Button>
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
  users: ReadonlyArray<
    Readonly<{ userAccessCode: number; userName: string | null; email: string | null }>
  >;
  username: string;
  email: string;
}>) {
  const selectedUserIsMissing =
    username && !users.some((user) => user.userName?.toLowerCase() === username.toLowerCase());

  return (
    <form className="flex flex-col gap-6" action={action}>
      <div className="flex flex-col gap-6">
        <div className="grid gap-2">
          <Label htmlFor="change-question-username">Username</Label>
          {canManageOthers ? (
            <select
              className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-base shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring md:text-sm"
              id="change-question-username"
              name="username"
              defaultValue={username}
              required
            >
              <option value="">Please select a Username</option>
              {selectedUserIsMissing ? (
                <option value={username}>{username} (selected)</option>
              ) : null}
              {users.map((user) => (
                <option
                  key={user.userAccessCode}
                  value={user.userName ?? String(user.userAccessCode)}
                >
                  {user.userName ?? `User ${user.userAccessCode}`}
                </option>
              ))}
            </select>
          ) : (
            <Input
              id="change-question-username"
              name="username"
              type="text"
              value={username}
              readOnly
            />
          )}
        </div>
        <div className="grid gap-2">
          <Label htmlFor="change-question-old-password">Old Password</Label>
          <Input
            id="change-question-old-password"
            name="oldPassword"
            type="password"
            autoComplete="current-password"
            required
          />
        </div>
        <div className="grid gap-2">
          <Label htmlFor="change-question-new-password">New Password</Label>
          <Input
            id="change-question-new-password"
            name="newPassword"
            type="password"
            autoComplete="new-password"
            minLength={8}
            required
          />
        </div>
        <div className="grid gap-2">
          <Label htmlFor="change-question-confirm-password">Confirm New Password</Label>
          <Input
            id="change-question-confirm-password"
            name="confirmNewPassword"
            type="password"
            autoComplete="new-password"
            minLength={8}
            required
          />
        </div>
        <div className="grid gap-2">
          <Label htmlFor="change-question-security-question">New Password Question</Label>
          <select
            className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-base shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring md:text-sm"
            id="change-question-security-question"
            name="securityQuestion"
            defaultValue="Select Question..."
            required
          >
            <option value="Select Question...">Select Question...</option>
            {SECURITY_QUESTIONS.map((question) => (
              <option key={question} value={question}>
                {question}
              </option>
            ))}
          </select>
        </div>
        <div className="grid gap-2">
          <Label htmlFor="change-question-security-answer">New Password Answer</Label>
          <Input
            id="change-question-security-answer"
            name="securityAnswer"
            type="text"
            autoComplete="off"
            required
          />
        </div>
        <div className="grid gap-2">
          <Label htmlFor="change-question-email">Email Address</Label>
          <Input
            id="change-question-email"
            name="email"
            type="email"
            defaultValue={email}
            autoComplete="email"
            required
          />
        </div>
      </div>
      <p className="text-xs leading-relaxed text-muted-foreground">
        Use at least 8 characters with uppercase, lowercase, a number, and a special character.
      </p>
      <div className="flex flex-col gap-3">
        <SubmitButton />
        <Button asChild className="w-full" variant="outline">
          <Link href="/home">Cancel</Link>
        </Button>
      </div>
    </form>
  );
}
