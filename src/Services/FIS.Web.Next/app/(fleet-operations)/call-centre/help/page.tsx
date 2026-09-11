import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";

const CALL_CENTRE_ROLE = "Call Centre";

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function HelpFallback() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Checking access...</p>
    </div>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>Call Centre help could not be opened.</h2>
      <p className="muted-copy">Retry when the FIS API is available.</p>
      <Link className="button button-primary" href="/call-centre/help">
        Try again
      </Link>
    </section>
  );
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access Call Centre help.</h2>
      <p className="muted-copy">This page requires the Call Centre role.</p>
    </section>
  );
}

async function CallCentreHelpContent() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return <SessionRecovery returnPath="/call-centre/help" />;
  }

  if (session.status === "unavailable") {
    return <ApiUnavailable />;
  }

  if (!hasRole(session.roles, CALL_CENTRE_ROLE)) {
    return <AccessRestricted />;
  }

  return (
    <div className="module-help-content">
      <details className="module-help-disclosure">
        <summary>Incident Capture</summary>
        <div className="module-help-section">
          <p className="module-help-intro">
            Record new service requests and incidents reported by departments.
          </p>
        </div>
      </details>
      <details className="module-help-disclosure">
        <summary>Incident Editing</summary>
        <div className="module-help-section">
          <p className="module-help-intro">
            Update existing incident records and track resolution details.
          </p>
        </div>
      </details>
      <details className="module-help-disclosure">
        <summary>Notifications</summary>
        <div className="module-help-section">
          <p className="module-help-intro">
            Maintain notification addresses for call centre communications.
          </p>
        </div>
      </details>
    </div>
  );
}

export default function CallCentreHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <article className="vehicle-card module-help-page" aria-labelledby="call-centre-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Call Centre</p>
            <h1 id="call-centre-help-title">Call Centre Maintenance Information / Help</h1>
            <p>Call centre maintenance guidance and references.</p>
          </div>
          <Link className="button button-secondary" href="/call-centre">
            Back to menu
          </Link>
        </header>
        <Suspense fallback={<HelpFallback />}>
          <CallCentreHelpContent />
        </Suspense>
      </article>
    </main>
  );
}
