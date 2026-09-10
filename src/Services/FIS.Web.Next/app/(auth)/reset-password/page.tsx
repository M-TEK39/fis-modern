import Link from "next/link";
import { Suspense } from "react";

import { AuthBrand, AuthFooter, AuthHeader, AuthPage } from "@/components/app-shell/auth-ui";
import { Button } from "@/components/ui/button";
import ResetPasswordForm from "@/app/(auth)/reset-password/reset-password-form";

type ResetPasswordPageProps = {
  searchParams: Promise<{ token?: string | string[] }>;
};

export default async function ResetPasswordPage({ searchParams }: ResetPasswordPageProps) {
  return (
    <AuthPage>
      <Suspense fallback={<ResetPasswordFallback />}>
        <ResetPasswordContent searchParams={searchParams} />
      </Suspense>
    </AuthPage>
  );
}

function ResetPasswordFallback() {
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
        <p>Checking your reset link...</p>
      </div>
    </>
  );
}

async function ResetPasswordContent({ searchParams }: ResetPasswordPageProps) {
  const query = await searchParams;
  const tokenValue = query.token;
  const token = Array.isArray(tokenValue) ? tokenValue[0]?.trim() : tokenValue?.trim();

  return (
    <>
      <AuthBrand />
      {token ? (
        <>
          <AuthHeader
            id="reset-password-title"
            title="Choose a new password"
            description="This link verifies your email address. Choose a new password to regain access."
          />
          <ResetPasswordForm token={token} />
          <AuthFooter>
            <Link
              className="text-sm font-medium text-primary underline-offset-4 hover:underline"
              href="/login"
            >
              Cancel
            </Link>
          </AuthFooter>
        </>
      ) : (
        <>
          <AuthHeader
            id="reset-password-title"
            title="Invalid reset link"
            description="This password reset link is missing or invalid. Request a new link to continue."
          />
          <Button asChild className="w-full">
            <Link href="/forgot-password">Request a new link</Link>
          </Button>
        </>
      )}
    </>
  );
}
