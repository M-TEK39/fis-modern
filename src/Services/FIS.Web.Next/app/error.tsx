"use client";

export default function GlobalError({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <main className="page-shell">
      <section className="status-card" role="alert">
        <div className="status-icon status-icon-error" aria-hidden="true">
          !
        </div>
        <p className="eyebrow">Something went wrong</p>
        <h1>We could not load this page.</h1>
        <p className="muted-copy">Your session is still safe. Try the request again or return to sign in.</p>
        <div className="button-row">
          <button className="button button-primary" type="button" onClick={() => reset()}>
            Try again
          </button>
          <a className="button button-secondary" href="/login">
            Return to sign in
          </a>
        </div>
      </section>
    </main>
  );
}
