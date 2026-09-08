import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

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

export default async function FinesHelpPage() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/fines/help" />
      </main>
    );
  }

  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  }

  if (!hasRole(session.roles, REPORTS_ROLE)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <article className="vehicle-card" aria-labelledby="fines-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Fines maintenance</p>
            <h1 id="fines-help-title">Fines Maintenance Information / Help</h1>
            <p>Definitions for the fields captured in the Fines section.</p>
          </div>
          <Link className="button button-secondary" href="/fines">
            Fines Menu
          </Link>
        </header>

        <section aria-labelledby="fines-help-purpose-title">
          <h2 id="fines-help-purpose-title">Purpose of Program</h2>
          <p>
            The Fines program provides a uniform process for capturing traffic fines and analysing
            the outcome of those matters.
          </p>
        </section>

        <section aria-labelledby="fines-help-fields-title">
          <h2 id="fines-help-fields-title">Term / Field Analysis</h2>
          <p>
            The definitions below describe the fields used throughout the Fines maintenance screens.
          </p>
          <div className="vehicle-table-wrapper">
            <table className="vehicle-table">
              <caption className="sr-only">Fines maintenance terms and field definitions</caption>
              <thead>
                <tr>
                  <th scope="col">Term / Field</th>
                  <th scope="col">Definition</th>
                </tr>
              </thead>
              <tbody>
                {HELP_ENTRIES.map((entry) => (
                  <tr key={entry.term}>
                    <th scope="row">{entry.term}</th>
                    <td>{entry.description}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </article>
    </main>
  );
}
