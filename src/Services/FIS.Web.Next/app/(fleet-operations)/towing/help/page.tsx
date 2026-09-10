import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";

const TOWING_ROLE = "Towing";

function hasRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(TOWING_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

export default async function TowingHelpPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/towing/help" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Towing help could not be opened.</h2>
          <p className="muted-copy">Retry when the FIS API is available.</p>
        </section>
      </main>
    );
  if (!hasRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to access Towing help.</h2>
        </section>
      </main>
    );

  return (
    <main className="page-shell vehicle-page-shell">
      <article className="vehicle-card" aria-labelledby="towing-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Road Side Assistance</p>
            <h1 id="towing-help-title">Road Side Assistance Information / Help</h1>
            <p>Use the same request and tow-company concepts as the legacy Towing screens.</p>
          </div>
          <Link className="button button-secondary" href="/towing">
            Towing Menu
          </Link>
        </header>
        <section aria-labelledby="towing-help-purpose">
          <h2 id="towing-help-purpose">Purpose of Program</h2>
          <p>
            The Road Side Assistance program records vehicle towing requests, the information
            supplied at the vehicle, and the assistance firm used to respond.
          </p>
        </section>
        <section aria-labelledby="towing-help-fields">
          <h2 id="towing-help-fields">Field guidance</h2>
          <div className="vehicle-table-wrapper">
            <table className="vehicle-table">
              <caption className="sr-only">Towing field guidance</caption>
              <thead>
                <tr>
                  <th scope="col">Field</th>
                  <th scope="col">Use</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td>Vehicle</td>
                  <td>Resolve the vehicle by its GG fleet number or GP registration number.</td>
                </tr>
                <tr>
                  <td>Call Reference</td>
                  <td>Keep the related call-centre reference when the request originated there.</td>
                </tr>
                <tr>
                  <td>Location / Problem</td>
                  <td>Record where the vehicle is and the problem reported.</td>
                </tr>
                <tr>
                  <td>Company / Site</td>
                  <td>Record the assistance firm and responsible FIS site.</td>
                </tr>
                <tr>
                  <td>Contact details</td>
                  <td>
                    Record the caller and the person with the vehicle so the legacy process can be
                    followed.
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </article>
    </main>
  );
}
