import Link from "next/link";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getTrackingSession, hasTrackingAccess } from "@/app/(fleet-operations)/tracking/_page";

function HelpFallback() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Loading help…</p>
    </div>
  );
}

function RestrictedState({ message }: Readonly<{ message: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>{message}</h2>
    </section>
  );
}

async function TrackingHelpContent() {
  const session = await getTrackingSession();
  if (session.status === "expired" || session.status === "unavailable")
    return <SessionRecovery returnPath="/tracking/help" />;
  if (session.status !== "authenticated")
    return <RestrictedState message="Your session could not be loaded." />;
  if (!hasTrackingAccess(session))
    return <RestrictedState message="Your profile does not include Vehicle Management access." />;

  return (
    <div className="module-help-content">
      <section className="module-help-section" aria-labelledby="tracking-help-workflow">
        <h2 id="tracking-help-workflow">Tracking workflow</h2>
        <p className="module-help-intro">
          Use Tracking Maintenance to capture tracker installations, removals, statuses, types, and
          notes for a vehicle. Use the reports menu to review the same records by vehicle, device,
          period, site, or department.
        </p>
        <p>
          Vehicle searches use the existing GG and registration-number data. The API keeps the
          original tracking table and legacy fields available when modern audit columns are not
          present.
        </p>
        <p className="module-help-note">
          Tracking has no separate legacy help document. The workflow above follows the current
          Tracking Maintenance and Tracking Reports menu entries.
        </p>
        <div className="button-row">
          <Link className="button button-secondary" href="/tracking">
            Tracking menu
          </Link>
        </div>
      </section>
    </div>
  );
}

export default function TrackingHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <article className="vehicle-card module-help-page" aria-labelledby="tracking-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Tracking</p>
            <h1 id="tracking-help-title">Tracking Information / Help</h1>
            <p>Reference information for tracking maintenance and reports.</p>
          </div>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </header>
        <Suspense fallback={<HelpFallback />}>
          <TrackingHelpContent />
        </Suspense>
      </article>
    </main>
  );
}
