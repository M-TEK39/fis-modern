import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import HelpEntriesSection from "@/components/ui/help-entries-section";
import { getSession } from "@/lib/auth/session";

const REPORTS_ROLE = "Reports";

type HelpEntry = {
  term: string;
  description: string;
};

const HELP_ENTRIES: readonly HelpEntry[] = [
  {
    term: "Date of Offence",
    description: "The date the offence was committed, as indicated on the fine.",
  },
  {
    term: "Reference Number",
    description: "The unique reference number issued by the traffic authority.",
  },
  { term: "Issued By", description: "The person or authority that issued the fine." },
  {
    term: "Traffic Dept.",
    description: "The traffic department or authority associated with the fine.",
  },
  { term: "Amount of Fine", description: "The Rand value of the fine as shown on the notice." },
  {
    term: "Due Date of Payment",
    description: "The date by which payment is due to avoid prosecution.",
  },
  {
    term: "Due Date to Appear in Court",
    description: "The date on which the recipient must appear in court, when applicable.",
  },
  { term: "Document Type", description: "The type of document received for the fine." },
  {
    term: "Date Received at GMT",
    description: "The date the fine was received by fleet management.",
  },
  {
    term: "Date of Notification to the Driver",
    description: "The date the driver was notified of the fine.",
  },
  {
    term: "Dept Code",
    description: "The department code associated with the responsible vehicle or driver.",
  },
  {
    term: "Responsible Person",
    description: "The name and ID number of the responsible person at the department.",
  },
];

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function HelpFallback() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Loading help…</p>
    </div>
  );
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access Fines help.</h2>
      <p className="muted-copy">This menu requires the Reports role.</p>
    </section>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>Fines help could not be opened.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/fines/help">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

async function FinesHelpContent() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return <SessionRecovery returnPath="/fines/help" />;
  }

  if (session.status === "unavailable") {
    return <ApiUnavailable />;
  }

  if (!hasRole(session.roles, REPORTS_ROLE)) {
    return <AccessRestricted />;
  }

  return (
    <>
      <div className="module-help-content">
        <section className="module-help-section" aria-labelledby="fines-help-purpose-title">
          <h2 id="fines-help-purpose-title">Purpose of Program</h2>
          <p className="module-help-intro">
            The Fines program provides a uniform process for capturing traffic fines and analysing
            the outcome of those matters.
          </p>
        </section>

        <HelpEntriesSection
          headingId="fines-help-fields-title"
          description="The definitions below describe the fields used throughout the Fines maintenance screens."
          caption="Fines maintenance terms and field definitions"
          entries={HELP_ENTRIES}
        />
      </div>
      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/fines">
          Fines Menu
        </Link>
      </div>
    </>
  );
}

export default function FinesHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <article className="vehicle-card module-help-page" aria-labelledby="fines-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Fines maintenance</p>
            <h1 id="fines-help-title">Fines Maintenance Information / Help</h1>
            <p>Definitions for the fields captured in the Fines section.</p>
          </div>
        </header>
        <Suspense fallback={<HelpFallback />}>
          <FinesHelpContent />
        </Suspense>
      </article>
    </main>
  );
}
