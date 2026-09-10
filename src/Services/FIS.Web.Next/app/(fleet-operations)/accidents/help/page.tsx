import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";

const ACCIDENTS_ROLE = "Accidents";

type HelpEntry = {
  id?: string;
  term: string;
  description: string;
};

const HELP_ENTRIES: readonly HelpEntry[] = [
  {
    term: "Accident Maintenance",
    description:
      "The capturing and updating of data for vehicles that have been involved in an accident.",
  },
  {
    term: "GG/ GP Number",
    description: "Registration number of the vehicle that has been involved in the accident.",
  },
  {
    id: "submit-maintenance",
    term: "Submit",
    description: "Submits the information to the database.",
  },
  { term: "MOD", description: "Allows a user to edit or modify previous entries in the system." },
  {
    term: "Add",
    description: "Adds a new incident that is not related to any previous accident on the system.",
  },
  { term: "Accident Date", description: "Date the specified vehicle was involved in an accident." },
  { term: "Accident Time", description: "Time of the specified accident." },
  { term: "GG CAR Km", description: "Odometer reading of the vehicle involved in the accident." },
  {
    term: "Date Updated",
    description:
      "The last date on which any updates were made to data related to this vehicle or accident.",
  },
  {
    term: "Accident Description",
    description: "Description of the damage to the Government vehicle.",
  },
  { term: "Accident Category", description: "Identification of the accident type." },
  {
    term: "Notify HQ",
    description: "Indicates whether Head Office has been supplied with all relevant documentation.",
  },
  {
    term: "Notify Date HQ",
    description: "Date documentation pertaining to this matter was forwarded to Head Office.",
  },
  {
    term: "GG Driver Fault",
    description: "Indicates whether the GG driver was responsible for the accident.",
  },
  {
    term: "GG Driver Name",
    description: "Name of the driver of the GG vehicle at the time of the accident.",
  },
  {
    term: "GG Driver ID Number",
    description: "ID number of the driver of the GG vehicle at the time of the accident.",
  },
  { term: "Site", description: "Site where the vehicle and driver are stationed." },
  {
    term: "Trip Authority",
    description:
      "Indicates whether the person had a valid Trip Authority for the specified vehicle at the time of the accident.",
  },
  {
    term: "Transport Officer",
    description:
      "The Transport Officer of the user department responsible for the specified vehicle.",
  },
  {
    term: "Transport Officer Tel.",
    description: "The contact telephone number of the relevant Transport Officer.",
  },
  {
    term: "GG Reference",
    description: "The unique reference number issued by GGMT for this matter.",
  },
  {
    term: "Case Number",
    description:
      "The reference number issued by the South African Police Service and allocated to this accident.",
  },
  { term: "GG Car Damage", description: "Damage value." },
  { term: "GG Damage Desc", description: "Description of the damage to the GG vehicle." },
  {
    term: "Death",
    description: "Indicates whether any person was killed as a result of the accident.",
  },
  {
    term: "Injured",
    description: "Indicates whether any person was injured as a result of the accident.",
  },
  {
    term: "Private Car Registration+",
    description:
      "Registration number of the private vehicle with which the GG vehicle was involved in this accident.",
  },
  {
    term: "Private Party Name",
    description: "Identification of the private party driving the private vehicle.",
  },
  { term: "Private Car Damage", description: "Damage value." },
  { term: "Claim Received", description: "Indicates whether a claim has been received." },
  { term: "Claim amount", description: "The amount of the claim that has been received." },
  { term: "File Close Date", description: "Date the file was closed." },
  {
    term: "Receive Docs. For relieve?",
    description: "Indicates whether all required documentation has been received.",
  },
  { term: "Notes", description: "Any additional information relevant to this matter." },
  { id: "submit-data", term: "Submit", description: "Submits the data to the database." },
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
      <p>Checking accident access...</p>
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
      <h2>Your session could not be checked.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/accidents/help">
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
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access Accident Maintenance help.</h2>
      <p className="muted-copy">
        Contact your FIS administrator if you need accident-management access.
      </p>
    </section>
  );
}

async function AccidentHelpContent() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return <SessionRecovery returnPath="/accidents/help" />;
  }

  if (session.status === "unavailable") {
    return <ApiUnavailable />;
  }

  if (!hasRole(session.roles, ACCIDENTS_ROLE)) {
    return <AccessRestricted />;
  }

  return (
    <>
      <div className="accident-help-content">
        <section aria-labelledby="accident-help-purpose">
          <h2 id="accident-help-purpose">Purpose of Program</h2>
          <p>
            The purpose of the accidents program is to provide a uniform program for capturing data
            about vehicles involved in accidents and the subsequent outcome of those accidents.
          </p>
        </section>

        <section aria-labelledby="accident-help-analysis">
          <h2 id="accident-help-analysis">Term / Field Analysis</h2>
          <p>
            The required data fields are mostly self-explanatory, but the definitions below specify
            their intended meaning.
          </p>
          <p>
            It is assumed that each user of the GGMT administrative functions on the Fleet
            Information System has received on-the-job training for the field relevant to their
            division.
          </p>
        </section>

        <div className="vehicle-table-wrapper">
          <table className="vehicle-table accident-help-table">
            <caption className="sr-only">Accident maintenance terms and field definitions</caption>
            <thead>
              <tr>
                <th scope="col">Term / Field</th>
                <th scope="col">Definition</th>
              </tr>
            </thead>
            <tbody>
              {HELP_ENTRIES.map((entry) => (
                <tr key={entry.id ?? entry.term}>
                  <th scope="row">{entry.term}</th>
                  <td>{entry.description}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/accidents">
          Back to Accident Menu
        </Link>
        <Link className="button button-secondary" href="/home">
          Home
        </Link>
        <form action={logoutAction}>
          <button className="button button-secondary" type="submit">
            Sign out
          </button>
        </form>
      </div>
    </>
  );
}

export default function AccidentHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="accident-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Accident maintenance</p>
            <h1 id="accident-help-title">Accident Maintenance Information / Help</h1>
            <p>Definitions and guidance for the accident maintenance fields.</p>
          </div>
        </header>
        <Suspense fallback={<HelpFallback />}>
          <AccidentHelpContent />
        </Suspense>
      </section>
    </main>
  );
}
