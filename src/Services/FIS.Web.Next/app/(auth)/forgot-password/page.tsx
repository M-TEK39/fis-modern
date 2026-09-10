import Link from "next/link";
import { Suspense } from "react";

import { AuthBrand, AuthFooter, AuthHeader, AuthPage } from "@/components/app-shell/auth-ui";
import ForgotPasswordForm from "@/app/(auth)/forgot-password/forgot-password-form";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function ForgotPasswordFallback() {
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
        <p>Loading account recovery...</p>
      </div>
    </>
  );
}

async function ForgotPasswordContent({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  const query = await searchParams;
  const initialIdentifier =
    getQueryValue(query.identifier) ??
    getQueryValue(query.username) ??
    getQueryValue(query.Username) ??
    "";

  return (
    <>
      <AuthBrand />
      <AuthHeader
        id="forgot-password-title"
        title="Forgot your password?"
        description="We will email a secure, one-time password reset link if the account has a registered email address."
      />
      <ForgotPasswordForm initialIdentifier={initialIdentifier} />
      <AuthFooter>
        <Link
          className="text-sm font-medium text-primary underline-offset-4 hover:underline"
          href="/login"
        >
          Back to sign in
        </Link>
        <span className="text-xs text-muted-foreground">
          For security, the same response is shown whether or not an account exists.
        </span>
      </AuthFooter>
    </>
  );
}

export default function ForgotPasswordPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  return (
    <AuthPage>
      <Suspense fallback={<ForgotPasswordFallback />}>
        <ForgotPasswordContent searchParams={searchParams} />
      </Suspense>
    </AuthPage>
  );
}
