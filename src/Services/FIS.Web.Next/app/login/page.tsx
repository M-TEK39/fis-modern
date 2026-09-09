import Link from "next/link";
import { connection } from "next/server";
import { Suspense } from "react";

import { logoutAction } from "@/app/actions/auth";
import { AuthBrand, AuthFooter, AuthHeader, AuthNotice, AuthPage } from "@/app/_components/auth-ui";
import { Button } from "@/components/ui/button";
import LoginForm from "@/app/login/login-form";
import { getSession } from "@/lib/session";

function LoginFallback() {
  return (
    <div
      className="flex flex-col items-center gap-3 py-8 text-sm text-muted-foreground"
      aria-busy="true"
    >
      <span className="spinner" aria-hidden="true" />
      <p>Checking your session...</p>
    </div>
  );
}

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function isEnabled(value: string | undefined) {
  const normalized = value?.trim().toLowerCase();
  return Boolean(normalized && !["0", "false", "no", "off"].includes(normalized));
}

async function LoginContent({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const [query, session] = await Promise.all([searchParams, getSession()]);
  const microsoftSignInUrl =
    process.env.MICROSOFT_SIGN_IN_URL?.trim() || "/api/auth/microsoft/sign-in";
  const microsoftSignInEnabled = isEnabled(process.env.MICROSOFT_SIGN_IN_ENABLED);
  const microsoftSignInFailed = getQueryValue(query.error) === "microsoft-sign-in";

  return (
    <>
      {microsoftSignInFailed ? (
        <AuthNotice tone="error">
          Microsoft sign-in could not be completed. Use your FIS credentials or try again.
        </AuthNotice>
      ) : null}

      {session.status === "unavailable" ? (
        <AuthNotice>
          We could not check your existing session. You can still try to sign in.
        </AuthNotice>
      ) : null}

      {session.status === "expired" ? (
        <AuthNotice>Your session needs to be refreshed. Sign in again to continue.</AuthNotice>
      ) : null}

      {session.status === "authenticated" ? (
        <>
          <AuthHeader
            id="login-page-title"
            title="Already signed in"
            description={
              session.email ? `Signed in as ${session.email}.` : "You are already authenticated."
            }
          />
          <div className="flex flex-col gap-3">
            <Button asChild className="w-full">
              <Link href="/home">Go to home</Link>
            </Button>
            <form action={logoutAction} className="w-full">
              <Button className="w-full" type="submit" variant="outline">
                Sign out
              </Button>
            </form>
          </div>
        </>
      ) : (
        <>
          <AuthHeader
            id="login-page-title"
            title="Welcome back"
            description="Use your fleet credentials to access the system."
          />
          <LoginForm
            microsoftSignInEnabled={microsoftSignInEnabled}
            microsoftSignInUrl={microsoftSignInUrl}
          />
          <AuthFooter>
            <Link
              className="text-sm font-medium text-primary underline-offset-4 hover:underline"
              href="/forgot-password"
            >
              Forgot your password?
            </Link>
            <span className="text-xs text-muted-foreground">
              Authorized Gauteng Provincial Government staff only.
            </span>
          </AuthFooter>
        </>
      )}
    </>
  );
}

export default function LoginPage({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  return (
    <AuthPage>
      <AuthBrand />
      <Suspense fallback={<LoginFallback />}>
        <LoginContent searchParams={searchParams} />
      </Suspense>
    </AuthPage>
  );
}
