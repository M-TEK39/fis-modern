"use client";

import { useActionState } from "react";
import { useFormStatus } from "react-dom";

import { loginAction, type LoginActionState } from "@/app/actions/auth";
import { cn } from "../../lib/utils";
import { Button } from "../button";
import { Input } from "../input";
import { Label } from "../label";

const initialLoginState: LoginActionState = { status: "idle" };

function MicrosoftIcon() {
  return (
    <svg aria-hidden="true" className="size-4" viewBox="0 0 24 24">
      <path d="M2 2h9v9H2zM13 2h9v9h-9zM2 13h9v9H2zM13 13h9v9h-9z" fill="currentColor" />
    </svg>
  );
}

function SubmitButton() {
  const { pending } = useFormStatus();

  return (
    <Button className="w-full" type="submit" disabled={pending}>
      {pending ? "Signing in..." : "Sign in"}
    </Button>
  );
}

export function LoginForm05({
  className,
  microsoftSignInEnabled,
  microsoftSignInUrl,
}: Readonly<{
  className?: string;
  microsoftSignInEnabled: boolean;
  microsoftSignInUrl: string;
}>) {
  const [state, formAction] = useActionState<LoginActionState, FormData>(
    loginAction,
    initialLoginState,
  );

  return (
    <div className={cn("flex flex-col gap-6", className)}>
      <form action={formAction} className="flex flex-col gap-6" noValidate>
        {state.status === "error" && state.message ? (
          <div
            className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive"
            role="alert"
          >
            <span>{state.message}</span>
          </div>
        ) : null}

        <div className="flex flex-col gap-6">
          <div className="grid gap-2">
            <Label htmlFor="firstName">First name</Label>
            <Input
              id="firstName"
              name="firstName"
              type="text"
              autoComplete="username"
              placeholder="Enter your first name"
              required
            />
          </div>

          <div className="grid gap-2">
            <Label htmlFor="password">Password</Label>
            <Input
              id="password"
              name="password"
              type="password"
              autoComplete="current-password"
              placeholder="Enter your password"
              required
            />
          </div>

          <SubmitButton />
        </div>

        <div className="relative text-center text-sm after:absolute after:inset-0 after:top-1/2 after:z-0 after:flex after:items-center after:border-t after:border-border">
          <span className="relative z-10 bg-background px-2 text-muted-foreground">Or</span>
        </div>

        {microsoftSignInEnabled ? (
          <Button asChild className="w-full" variant="outline">
            <a href={microsoftSignInUrl}>
              <MicrosoftIcon />
              Sign in with Microsoft
            </a>
          </Button>
        ) : (
          <>
            <Button className="w-full" type="button" variant="outline" disabled>
              <MicrosoftIcon />
              Sign in with Microsoft
            </Button>
            <p className="text-center text-xs text-muted-foreground">
              Microsoft sign-in is not available.
            </p>
          </>
        )}
      </form>
    </div>
  );
}
