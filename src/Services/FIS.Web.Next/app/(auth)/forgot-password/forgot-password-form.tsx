"use client";

import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import { forgotPasswordAction, type ForgotPasswordActionState } from "@/app/(auth)/actions/auth";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

const initialState: ForgotPasswordActionState = { status: "idle" };

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <Button className="w-full" type="submit" disabled={pending}>
      {pending ? "Sending..." : "Send reset link"}
    </Button>
  );
}

export default function ForgotPasswordForm({
  initialIdentifier = "",
}: {
  initialIdentifier?: string;
}) {
  const [state, formAction] = useActionState<ForgotPasswordActionState, FormData>(
    forgotPasswordAction,
    initialState,
  );

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

      {state.status === "success" && state.message ? (
        <div
          className="rounded-md border border-border bg-muted px-3 py-2 text-sm text-muted-foreground"
          role="status"
        >
          {state.message}
        </div>
      ) : null}

      <div className="grid gap-2">
        <Label htmlFor="identifier">Username, email, or user code</Label>
        <Input
          id="identifier"
          name="identifier"
          type="text"
          autoComplete="username"
          placeholder="Enter your account identifier"
          defaultValue={initialIdentifier}
          required
        />
      </div>

      <SubmitButton />
    </form>
  );
}
