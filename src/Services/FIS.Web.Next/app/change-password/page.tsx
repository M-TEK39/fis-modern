import Link from "next/link";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import ChangePasswordForm from "@/app/change-password/change-password-form";
import { getSession } from "@/lib/session";

function UnavailableState() {
  return (
    <section className="status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h1>We could not verify your session.</h1>
      <p className="muted-copy">The application is still running. Retry when the FIS API is available.</p>
      <div className="button-row">
        <Link className="button button-primary" href="/change-password">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

function ExpiredSessionState() {
  return (
    <section className="status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Session expired</p>
      <h1>Sign in again to change your password.</h1>
      <p className="muted-copy">Your password change page needs an active FIS session.</p>
      <div className="button-row">
        <Link className="button button-primary" href="/login">
          Return to sign in
        </Link>
      </div>
    </section>
  );
}

export default function ChangePasswordPage() {
  return (
    <main className="page-shell">
      <Suspense fallback={<ChangePasswordFallback />}>
        <ChangePasswordContent />
      </Suspense>
    </main>
  );
}

function ChangePasswordFallback() {
  return (
    <section className="status-card" aria-busy="true">
      <div className="loading-card">
        <span className="spinner" aria-hidden="true" />
        <p>Checking your session...</p>
      </div>
    </section>
  );
}

async function ChangePasswordContent() {
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  return (
    <>
      {session.status === "unavailable" ? <UnavailableState /> : null}
      {session.status === "expired" ? <ExpiredSessionState /> : null}
      {session.status === "authenticated" ? (
        <section className="auth-card" aria-labelledby="change-password-title">
          <div className="brand">
            <div className="brand-mark" aria-hidden="true">
              FIS
            </div>
            <div>
              <p className="brand-name">Fleet Information System</p>
              <p className="brand-caption">Secure account settings</p>
            </div>
          </div>
          <div className="auth-header">
            <p className="eyebrow">Password security</p>
            <h1 id="change-password-title">
              {session.passwordChangeRequired ? "Your password has expired" : "Change your password"}
            </h1>
            <p>
              {session.passwordChangeRequired
                ? "You must change your password before continuing."
                : "Update your FIS password while keeping your account secure."}
            </p>
          </div>
          <ChangePasswordForm passwordChangeRequired={session.passwordChangeRequired} />
          {!session.passwordChangeRequired ? (
            <div className="auth-footer">
              <Link className="text-link" href="/home">
                Cancel and return home
              </Link>
            </div>
          ) : null}
        </section>
      ) : null}
    </>
  );
}
