import Link from "next/link";

import SessionRecovery from "@/app/home/session-recovery";

export function DemoSessionRecovery({ returnPath }: Readonly<{ returnPath: string }>) {
  return <SessionRecovery returnPath={returnPath} />;
}

export function DemoAccessRestricted({ message }: Readonly<{ message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Access restricted</p>
      <h2>{message}</h2>
      <div className="button-row">
        <Link className="button button-secondary" href="/vehicles">
          Back to Vehicle Master
        </Link>
      </div>
    </section>
  );
}

export function DemoApiUnavailable({ message }: Readonly<{ message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>{message}</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/vehicles/demo/add">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

export function DemoLoadingState({
  message = "Loading demo vehicle information...",
}: Readonly<{ message?: string }>) {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>{message}</p>
    </div>
  );
}
