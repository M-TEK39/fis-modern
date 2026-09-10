"use client";

import Link from "next/link";
import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import { changePasswordAction, type ChangePasswordActionState } from "@/app/(auth)/actions/auth";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

const initialState: ChangePasswordActionState = { status: "idle" };

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <Button className="w-full" type="submit" disabled={pending}>
      {pending ? "Updating..." : "Change password"}
    </Button>
  );
}

export default function ChangePasswordForm({
  passwordChangeRequired,
}: {
  passwordChangeRequired: boolean;
}) {
  const [state, formAction] = useActionState<ChangePasswordActionState, FormData>(
    changePasswordAction,
    initialState,
  );

  if (state.status === "success") {
    return (
      <>
        <div
          className="rounded-md border border-border bg-muted px-3 py-2 text-sm text-muted-foreground"
          role="status"
        >
          {state.message}
        </div>
        <Button asChild className="w-full">
          <Link href="/login">Continue to sign in</Link>
        </Button>
      </>
    );
  }

  return (
    <form action={formAction} className="flex flex-col gap-6" noValidate>
      {state.status === "error" && state.message ? (
        <div
          className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive"
          role="alert"
        >
          {state.message}
        </div>
      ) : null}

      <div className="grid gap-2">
        <Label htmlFor="current-password">Current password</Label>
        <Input
          id="current-password"
          name="currentPassword"
          type="password"
          autoComplete="current-password"
          placeholder="Enter your current password"
          required
        />
      </div>

      <div className="grid gap-2">
        <Label htmlFor="new-password">New password</Label>
        <Input
          id="new-password"
          name="newPassword"
          type="password"
          autoComplete="new-password"
          minLength={8}
          placeholder="Enter your new password"
          required
        />
      </div>

      <div className="grid gap-2">
        <Label htmlFor="confirm-new-password">Confirm new password</Label>
        <Input
          id="confirm-new-password"
          name="confirmNewPassword"
          type="password"
          autoComplete="new-password"
          minLength={8}
          placeholder="Re-enter your new password"
          required
        />
      </div>

      <p className="text-xs leading-relaxed text-muted-foreground">
        {passwordChangeRequired
          ? "Your password has expired. Use at least 8 characters with uppercase, lowercase, a number, and a special character."
          : "Use at least 8 characters with uppercase, lowercase, a number, and a special character."}
      </p>
      <SubmitButton />
    </form>
  );
}
