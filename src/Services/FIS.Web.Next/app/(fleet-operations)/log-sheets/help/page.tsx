import Link from "next/link";
import { Suspense } from "react";

import { getLogsheetSession, hasLogsheetAccess } from "@/app/(fleet-operations)/log-sheets/_page";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";

function HelpFallback() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Checking access...</p>
    </div>
  );
}

function AccessRestricted({ message }: { message: string }) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>{message}</h2>
    </section>
  );
}

async function LogsheetHelpContent() {
  const session = await getLogsheetSession();
  if (session.status === "expired" || session.status === "unavailable") {
    return <SessionRecovery returnPath="/log-sheets/help" />;
  }
  if (session.status !== "authenticated")
    return <AccessRestricted message="Your session could not be loaded." />;
  if (!hasLogsheetAccess(session))
    return <AccessRestricted message="Your profile does not include Log Sheets access." />;

  return (
    <>
      <div className="module-help-content">
        <section className="module-help-section" aria-labelledby="logsheet-help-title">
          <h2 id="logsheet-help-title">Logsheet workflow</h2>
          <ol className="module-help-steps">
            <li>Search for a vehicle by its GG or GP number before entering a logsheet.</li>
            <li>
              Enter the requisition, month, odometer readings, days used, batch number, and site.
            </li>
            <li>
              Use the reports to review captured activity or total kilometres by vehicle class.
            </li>
            <li>
              Editing and deletion remain restricted to the legacy logsheet manager access codes.
            </li>
          </ol>
          <p className="module-help-note">
            The service reads the original required `dbo.Logsheets` columns and uses expanded audit
            columns when the database provides them.
          </p>
        </section>
      </div>
      <div className="vehicle-footer-actions">
        <Link className="button button-primary" href="/log-sheets/enter">
          Enter a Logsheet
        </Link>
        <Link className="button button-secondary" href="/log-sheets">
          Back to menu
        </Link>
      </div>
    </>
  );
}

export default function LogsheetHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <article className="vehicle-card module-help-page" aria-labelledby="logsheet-page-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Logsheet management</p>
            <h1 id="logsheet-page-title">Logsheet Maintenance Information / Help</h1>
            <p>Use this workflow to capture and report monthly vehicle usage.</p>
          </div>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </header>
        <Suspense fallback={<HelpFallback />}>
          <LogsheetHelpContent />
        </Suspense>
      </article>
    </main>
  );
}
