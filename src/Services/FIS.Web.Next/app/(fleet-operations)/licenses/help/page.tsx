import Link from "next/link";
import { Suspense } from "react";

import { getLicenseSession, hasLicenseAccess } from "@/app/(fleet-operations)/licenses/_page";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";

function HelpFallback() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Checking access...</p>
    </div>
  );
}

function SessionProblem({ status }: { status: "expired" | "unavailable" }) {
  if (status === "expired") {
    return <SessionRecovery returnPath="/licenses/help" />;
  }

  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>The sign-in service is temporarily unavailable.</h2>
      <p className="muted-copy">Retry when the FIS API is available.</p>
    </section>
  );
}

function AccessRestricted({ message = "Your profile does not include Licence access." }) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>{message}</h2>
    </section>
  );
}

async function LicenseHelpContent() {
  const session = await getLicenseSession();
  if (session.status === "expired" || session.status === "unavailable") {
    return <SessionProblem status={session.status} />;
  }
  if (session.status !== "authenticated")
    return <AccessRestricted message="Your session could not be loaded." />;
  if (!hasLicenseAccess(session)) return <AccessRestricted />;

  return (
    <>
      <div className="module-help-content">
        <section className="module-help-section" aria-labelledby="license-help-title">
          <p className="eyebrow">Licence maintenance</p>
          <h2 id="license-help-title">Workflow guidance</h2>
          <ol className="module-help-steps">
            <li>
              Maintain the newest licence expiry date and registration document for each vehicle.
            </li>
            <li>
              Use the one-vehicle workflow for a single update, or the multi-collection workflow
              when collecting two or more licences.
            </li>
            <li>
              Use the garage workflow when a licence is available for collection at Johannesburg or
              Pretoria.
            </li>
            <li>
              Keep the receiver name, identification, telephone, site, collection date, and COF
              information accurate for audit and reporting.
            </li>
          </ol>
          <p className="module-help-note">
            The licence reports use the same vehicle and licence data as the maintenance workflows.
            If a report returns no rows, check the vehicle identifiers and date range.
          </p>
        </section>
      </div>
      <div className="vehicle-footer-actions">
        <Link className="button button-primary" href="/manuals">
          Open Manuals
        </Link>
        <Link className="button button-secondary" href="/licenses">
          Back to Licence Menu
        </Link>
      </div>
    </>
  );
}

export default function LicenseHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <article className="vehicle-card module-help-page" aria-labelledby="license-page-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Licences</p>
            <h1 id="license-page-title">Licence Maintenance Help</h1>
            <p>Guidance for licence maintenance and certificate workflows.</p>
          </div>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </header>
        <Suspense fallback={<HelpFallback />}>
          <LicenseHelpContent />
        </Suspense>
      </article>
    </main>
  );
}
