import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import { AuthBrand, AuthFooter, AuthHeader, AuthPage } from "@/components/app-shell/auth-ui";
import { Button } from "@/components/ui/button";
import ChangePasswordForm from "@/app/(auth)/change-password/change-password-form";
import { getSession } from "@/lib/auth/session";

function UnavailableState() {
  return (
    <>
      <AuthBrand caption="Secure account settings" />
      <AuthHeader
        id="change-password-unavailable-title"
        title="We could not verify your session."
        description="Please try again when the service is available."
      />
      <div className="flex flex-col gap-3">
        <Button asChild className="w-full">
          <Link href="/change-password">Try again</Link>
        </Button>
        <Button asChild className="w-full" variant="outline">
          <Link href="/login">Sign in</Link>
        </Button>
      </div>
    </>
  );
}

function ExpiredSessionState() {
  return (
    <>
      <AuthBrand caption="Secure account settings" />
      <AuthHeader
        id="change-password-expired-title"
        title="Sign in again to change your password."
        description="Your session has expired. Sign in again to continue."
      />
      <div className="flex flex-col gap-3">
        <Button asChild className="w-full">
          <Link href="/login">Return to sign in</Link>
        </Button>
      </div>
    </>
  );
}

export default function ChangePasswordPage() {
  return (
    <AuthPage>
      <Suspense fallback={<ChangePasswordFallback />}>
        <ChangePasswordContent />
      </Suspense>
    </AuthPage>
  );
}

function ChangePasswordFallback() {
  return (
    <>
      <AuthBrand />
      <div
        className="flex flex-col items-center gap-3 py-8 text-sm text-muted-foreground"
        aria-busy="true"
      >
        <span
          className="h-4 w-4 animate-spin rounded-full border-2 border-muted-foreground/30 border-t-muted-foreground"
          aria-hidden="true"
        />
        <p>Checking your session...</p>
      </div>
    </>
  );
}

async function ChangePasswordContent() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  return (
    <>
      {session.status === "unavailable" ? <UnavailableState /> : null}
      {session.status === "expired" ? <ExpiredSessionState /> : null}
      {session.status === "authenticated" ? (
        <>
          <AuthBrand caption="Secure account settings" />
          <AuthHeader
            id="change-password-title"
            title={
              session.passwordChangeRequired ? "Your password has expired" : "Change your password"
            }
            description={
              session.passwordChangeRequired
                ? "You must change your password before continuing."
                : "Update your FIS password while keeping your account secure."
            }
          />
          <ChangePasswordForm passwordChangeRequired={session.passwordChangeRequired} />
          {!session.passwordChangeRequired ? (
            <AuthFooter>
              <Link
                className="text-sm font-medium text-primary underline-offset-4 hover:underline"
                href="/home"
              >
                Cancel and return home
              </Link>
            </AuthFooter>
          ) : null}
        </>
      ) : null}
    </>
  );
}
