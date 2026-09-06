import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

type ValidationHelpPageProps = {
  routePath?: string;
};

const HELP_ENTRIES = [
  ["Maintenance", "The process of ensuring that data accurately reflects the facts for each field, such as class code maintenance."],
  ["Edit", "The process of changing or correcting information previously entered into the database."],
  ["Add New", "The creation of a new entry in the database."],
  ["Delete", "The action of cancelling information from the database."],
  ["Department", "The government institution where the vehicle is assigned."],
  ["Site", "The specified site within a government institution."],
  ["Name of Responsible Person", "The person responsible for the fleet."],
  ["Address", "The physical address of the responsible department or site."],
] as const;

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access Validation Data help.</h2>
      <Link className="button button-secondary" href="/validation-data">Validation Data</Link>
    </section>
  );
}

export default async function ValidationHelpPage({ routePath = "/validation-data/help" }: ValidationHelpPageProps) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
  if (session.status === "unavailable") {
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Service unavailable</p><h2>Validation Data help could not be opened.</h2><p className="muted-copy">Retry when the FIS API is available.</p></section></main>;
  }
  if (!hasVehicleManagementPermission(session.accessLevel)) return <main className="page-shell vehicle-page-shell"><AccessRestricted /></main>;

  return (
    <main className="page-shell vehicle-page-shell">
      <article className="vehicle-card legacy-doc" aria-labelledby="validation-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Validation Data</p>
            <h1 id="validation-help-title">Validation Data Maintenance Information / Help</h1>
            <p>Guidance for the validation and reference data used throughout FIS.</p>
          </div>
          <Link className="button button-secondary" href="/validation-data">Validation Data</Link>
        </header>
        <section aria-labelledby="validation-purpose-title">
          <h2 id="validation-purpose-title">Purpose of Program</h2>
          <p>The Validation Data program provides a uniform process for capturing contract-essential system data, including site and department maintenance.</p>
          <p>Users of the GGMT administrative functions are expected to have received on-the-job training for the fields relevant to their division.</p>
        </section>
        <section aria-labelledby="validation-terms-title">
          <h2 id="validation-terms-title">Term / Field Analysis</h2>
          <p>The required data fields are mostly self-explanatory; the definitions below describe their intended use.</p>
          <div className="vehicle-table-wrapper">
            <table className="vehicle-table">
              <caption className="sr-only">Validation Data terms and definitions</caption>
              <thead><tr><th scope="col">Term / Field</th><th scope="col">Definition</th></tr></thead>
              <tbody>{HELP_ENTRIES.map(([term, description]) => <tr key={term}><th scope="row">{term}</th><td>{description}</td></tr>)}</tbody>
            </table>
          </div>
        </section>
      </article>
    </main>
  );
}
