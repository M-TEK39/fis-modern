import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";

const TOWING_ROLE = "Towing";

function hasRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(TOWING_ROLE, undefined, { sensitivity: "accent" }) === 0,
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

async function TowingHelpContent() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <SessionRecovery returnPath="/towing/help" />;
  if (session.status === "unavailable")
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">API unavailable</p>
        <h2>Towing help could not be opened.</h2>
        <p className="muted-copy">Retry when the FIS API is available.</p>
      </section>
    );
  if (!hasRole(session.roles))
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Access restricted</p>
        <h2>You do not have permission to access Towing help.</h2>
      </section>
    );

  return (
    <>
      <div className="module-help-content">
        <section className="module-help-section" aria-labelledby="towing-help-purpose">
          <h2 id="towing-help-purpose">Purpose of Program</h2>
          <p className="module-help-intro">
            The Road Side Assistance program records vehicle towing requests, the information
            supplied at the vehicle, and the assistance firm used to respond.
          </p>
        </section>
        <section className="module-help-section" aria-labelledby="towing-help-fields">
          <h2 id="towing-help-fields">Field guidance</h2>
          <div className="vehicle-table-wrapper">
            <table className="vehicle-table module-help-table">
              <caption className="sr-only">Towing field guidance</caption>
              <DataTableHeader
                columns={[
                  { key: "column-1", label: <>Field</> },
                  { key: "column-2", label: <>Use</> },
                ]}
              />
              <tbody>
                <tr>
                  <th scope="row">Vehicle</th>
                  <td>Resolve the vehicle by its GG fleet number or GP registration number.</td>
                </tr>
                <tr>
                  <th scope="row">Call Reference</th>
                  <td>Keep the related call-centre reference when the request originated there.</td>
                </tr>
                <tr>
                  <th scope="row">Location / Problem</th>
                  <td>Record where the vehicle is and the problem reported.</td>
                </tr>
                <tr>
                  <th scope="row">Company / Site</th>
                  <td>Record the assistance firm and responsible FIS site.</td>
                </tr>
                <tr>
                  <th scope="row">Contact details</th>
                  <td>
                    Record the caller and the person with the vehicle so the legacy process can be
                    followed.
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </div>
      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/towing">
          Towing Menu
        </Link>
      </div>
    </>
  );
}

export default function TowingHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <article className="vehicle-card module-help-page" aria-labelledby="towing-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Road Side Assistance</p>
            <h1 id="towing-help-title">Road Side Assistance Information / Help</h1>
            <p>Use the same request and tow-company concepts as the legacy Towing screens.</p>
          </div>
        </header>
        <Suspense fallback={<HelpFallback />}>
          <TowingHelpContent />
        </Suspense>
      </article>
    </main>
  );
}
