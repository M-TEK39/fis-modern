import Link from "next/link";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getLogbookSession, hasLogbookAccess } from "@/app/(fleet-operations)/log-books/_page";

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

async function LogbookHelpContent() {
  const session = await getLogbookSession();

  if (session.status === "expired" || session.status === "unavailable") {
    return <SessionRecovery returnPath="/log-books/help" />;
  }

  if (session.status !== "authenticated") {
    return <RestrictedState message="Your session could not be loaded." />;
  }

  if (!hasLogbookAccess(session)) {
    return <RestrictedState message="Your profile does not include Logbooks access." />;
  }

  return (
    <>
      <div className="module-help-content">
        <section className="module-help-section" aria-labelledby="logbook-help-purpose">
          <h2 id="logbook-help-purpose">Purpose of Program</h2>
          <p className="module-help-intro">
            The purpose of the Logbooks program is to provide a uniform program for capturing data
            related to the fuel cards and the subsequent analysis of the outcome of the matters.
          </p>
        </section>

        <section className="module-help-section" aria-labelledby="logbook-help-analysis">
          <h2 id="logbook-help-analysis">Term / Field Analysis</h2>
          <p>
            The required data fields are mostly self-explanatory, but the definitions below specify
            their intended meaning.
          </p>
          <p className="module-help-note">
            It is assumed that each user of the GGMT administrative functions on the Fleet
            Information System has received on-the-job training for the field relevant to their
            division.
          </p>
          <details className="module-help-disclosure" open>
            <summary>Field definitions</summary>
            <ol className="module-help-steps">
              <li>
                <strong>Handout Date:</strong> Date the logbook has been handed over to the user.
              </li>
              <li>
                <strong>Begin Number:</strong> The sequence number the logbook&apos;s first page
                starts with.
              </li>
              <li>
                <strong>Number:</strong> The sequence number the logbook ends with.
              </li>
              <li>
                <strong>Dept Code:</strong> The specified departmental site of the person that
                collected the fuel card.
              </li>
              <li>
                <strong>Name of Receiver:</strong> Name of the person that collected the logbook.
              </li>
              <li>
                <strong>Reveiver Tel:</strong> Contact telephone number of the person that collected
                the logbook.
              </li>
              <li>
                <strong>Comment:</strong> Essential information that must be taken into
                consideration.
              </li>
              <li>
                <strong>Submit:</strong> Submits the information to the database.
              </li>
            </ol>
          </details>
        </section>

        <section className="module-help-section" aria-labelledby="logbook-help-actions">
          <h2 id="logbook-help-actions">Continue</h2>
          <p className="module-help-intro">
            Use Maintenance to review or add a handout for one vehicle. Use Collection when several
            vehicles receive logbooks together. Use Delete only when a handout has been returned and
            should be removed from the active list.
          </p>
          <div className="button-row">
            <Link className="button button-primary" href="/log-books/maintenance">
              Maintenance
            </Link>
            <Link className="button button-secondary" href="/log-books">
              Main menu
            </Link>
          </div>
        </section>
      </div>
    </>
  );
}

export default function LogbookHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <article className="vehicle-card module-help-page" aria-labelledby="logbook-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Log book management</p>
            <h1 id="logbook-help-title">Logbook Maintenance Information / Help</h1>
            <p>Logbook guidance and operational notes.</p>
          </div>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </header>
        <Suspense fallback={<HelpFallback />}>
          <LogbookHelpContent />
        </Suspense>
      </article>
    </main>
  );
}
