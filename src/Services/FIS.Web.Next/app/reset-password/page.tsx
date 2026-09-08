import Link from "next/link";
import { Suspense } from "react";

import ResetPasswordForm from "@/app/reset-password/reset-password-form";

function Brand() {
  return (
    <div className="brand">
      <div className="brand-mark" aria-hidden="true">
        FIS
      </div>
      <div>
        <p className="brand-name">Fleet Information System</p>
        <p className="brand-caption">Gauteng Provincial Government</p>
      </div>
    </div>
  );
}

type ResetPasswordPageProps = {
  searchParams: Promise<{ token?: string | string[] }>;
};

export default async function ResetPasswordPage({ searchParams }: ResetPasswordPageProps) {
  return (
    <main className="page-shell">
      <Suspense fallback={<ResetPasswordFallback />}>
        <ResetPasswordContent searchParams={searchParams} />
      </Suspense>
    </main>
  );
}

function ResetPasswordFallback() {
  return (
    <section className="auth-card" aria-busy="true">
      <Brand />
      <div className="loading-card">
        <span className="spinner" aria-hidden="true" />
        <p>Checking your reset link...</p>
      </div>
    </section>
  );
}

async function ResetPasswordContent({ searchParams }: ResetPasswordPageProps) {
  const query = await searchParams;
  const tokenValue = query.token;
  const token = Array.isArray(tokenValue) ? tokenValue[0]?.trim() : tokenValue?.trim();

  return (
    <section className="auth-card" aria-labelledby="reset-password-title">
      <Brand />
      {token ? (
        <>
          <div className="auth-header">
            <p className="eyebrow">Account recovery</p>
            <h1 id="reset-password-title">Choose a new password</h1>
            <p>This link verifies your email address. Choose a new password to regain access.</p>
          </div>
          <ResetPasswordForm token={token} />
          <div className="auth-footer">
            <Link className="text-link" href="/login">
              Cancel
            </Link>
          </div>
        </>
      ) : (
        <>
          <div className="auth-header">
            <p className="eyebrow">Account recovery</p>
            <h1 id="reset-password-title">Invalid reset link</h1>
            <p>This password reset link is missing or invalid. Request a new link to continue.</p>
          </div>
          <Link className="button button-primary button-wide" href="/forgot-password">
            Request a new link
          </Link>
        </>
      )}
    </section>
  );
}
