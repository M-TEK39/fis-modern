import Link from "next/link";
import { Suspense } from "react";

import { logoutAction } from "@/app/actions/auth";
import LoginForm from "@/app/login/login-form";
import { getSession } from "@/lib/session";

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

function LoginFallback() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Checking your session...</p>
    </div>
  );
}

async function LoginContent() {
  const session = await getSession();
  const microsoftSignInUrl = process.env.MICROSOFT_SIGN_IN_URL?.trim() || "/api/auth/microsoft/sign-in";
  const microsoftSignInEnabled = Boolean(process.env.MICROSOFT_SIGN_IN_ENABLED?.trim());

  return (
    <>
      {session.status === "unavailable" ? (
        <div className="notice notice-info" role="status">
          <span aria-hidden="true">i</span>
          <span>We could not check your existing session. You can still try to sign in.</span>
        </div>
      ) : null}

      {session.status === "expired" ? (
        <div className="notice notice-info" role="status">
          <span aria-hidden="true">i</span>
          <span>Your session needs to be refreshed. Sign in again to continue.</span>
        </div>
      ) : null}

      {session.status === "authenticated" ? (
        <>
          <div className="auth-header">
            <p className="eyebrow">Session active</p>
            <h1>Already signed in</h1>
            <p>{session.email ? `Signed in as ${session.email}.` : "You are already authenticated."}</p>
          </div>
          <div className="auth-actions">
            <Link className="button button-primary button-wide" href="/home">
              Go to home
            </Link>
            <form action={logoutAction} className="button-wide">
              <button className="button button-secondary button-wide" type="submit">
                Sign out
              </button>
            </form>
          </div>
        </>
      ) : (
        <>
          <div className="auth-header">
            <p className="eyebrow">Secure access</p>
            <h1>Welcome back</h1>
            <p>Use your fleet credentials to access the system.</p>
          </div>
          {microsoftSignInEnabled ? (
            <a className="button button-secondary button-wide" href={microsoftSignInUrl}>
              Sign in with Microsoft
            </a>
          ) : null}
          <LoginForm />
          <div className="auth-footer">
            <Link className="text-link" href="/forgot-password">
              Forgot your password?
            </Link>
            <span className="auth-footnote">Authorized Gauteng Provincial Government staff only.</span>
          </div>
        </>
      )}
    </>
  );
}

export default function LoginPage() {
  return (
    <main className="page-shell">
      <section className="auth-card" aria-labelledby="login-page-title">
        <Brand />
        <h1 id="login-page-title" className="sr-only">
          Sign in to Fleet Information System
        </h1>
        <Suspense fallback={<LoginFallback />}>
          <LoginContent />
        </Suspense>
      </section>
    </main>
  );
}
