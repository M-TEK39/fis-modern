import Link from "next/link";

export default function SignedOutPage() {
  return (
    <main className="page-shell">
      <section className="auth-card" aria-labelledby="signed-out-title">
        <div className="auth-header">
          <div className="status-icon status-icon-success" aria-hidden="true">
            ✓
          </div>
          <h1 id="signed-out-title">You&apos;ve been signed out</h1>
          <p>Your session has been ended successfully.</p>
        </div>

        <div className="auth-actions">
          <Link className="button button-primary button-wide" href="/login">
            Sign in again
          </Link>
          <Link className="button button-secondary button-wide" href="/">
            Return to home
          </Link>
        </div>
      </section>
    </main>
  );
}
