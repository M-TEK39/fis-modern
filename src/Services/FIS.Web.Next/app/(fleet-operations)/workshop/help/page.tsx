import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";

const WORKSHOP_ROLE = "Workshop";

function hasWorkshopRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(WORKSHOP_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

function HelpFallback() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Checking help access...</p>
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
      <h2>Workshop could not be opened.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/workshop">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access Workshop.</h2>
    </section>
  );
}

async function WorkshopHelpContent() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");

  if (session.status === "expired") {
    return <SessionRecovery returnPath="/workshop/help" />;
  }

  if (session.status === "unavailable") {
    return <ApiUnavailable />;
  }

  if (!hasWorkshopRole(session.roles)) {
    return <AccessRestricted />;
  }

  return (
    <div className="document-help-content">
      <iframe
        src="/legacy/workshop/Doc_Workshop.htm"
        title="Workshop Help"
        className="help-iframe document-help-frame"
      />
    </div>
  );
}

export default function WorkshopHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card document-help-card" aria-labelledby="workshop-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Workshop</p>
            <h1 id="workshop-help-title">Workshop Maintenance Information / Help</h1>
          </div>
          <Link className="button button-secondary" href="/workshop">
            Back
          </Link>
        </header>
        <Suspense fallback={<HelpFallback />}>
          <WorkshopHelpContent />
        </Suspense>
      </section>
    </main>
  );
}
