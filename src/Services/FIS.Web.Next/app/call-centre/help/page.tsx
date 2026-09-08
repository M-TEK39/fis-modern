import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

const CALL_CENTRE_ROLE = "Call Centre";

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

export default async function CallCentreHelpPage() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/call-centre/help" />
      </main>
    );
  }

  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <div className="status-icon status-icon-error" aria-hidden="true">
            !
          </div>
          <p className="eyebrow">API unavailable</p>
          <h2>Call Centre help could not be opened.</h2>
          <p className="muted-copy">Retry when the FIS API is available.</p>
          <Link className="button button-primary" href="/call-centre/help">
            Try again
          </Link>
        </section>
      </main>
    );
  }

  if (!hasRole(session.roles, CALL_CENTRE_ROLE)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <div className="status-icon status-icon-error" aria-hidden="true">
            !
          </div>
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to access Call Centre help.</h2>
          <p className="muted-copy">This page requires the Call Centre role.</p>
        </section>
      </main>
    );
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="call-centre-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Call Centre</p>
            <h1 id="call-centre-help-title">Call Centre Maintenance Information / Help</h1>
            <p>Call centre maintenance guidance and references.</p>
          </div>
          <Link className="button button-secondary" href="/call-centre">
            Back to menu
          </Link>
        </header>

        <div className="vehicle-menu-tiles">
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">Incident Capture</h2>
            <div className="vehicle-menu-body">
              <p>Record new service requests and incidents reported by departments.</p>
            </div>
          </section>
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">Incident Editing</h2>
            <div className="vehicle-menu-body">
              <p>Update existing incident records and track resolution details.</p>
            </div>
          </section>
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">Notifications</h2>
            <div className="vehicle-menu-body">
              <p>Maintain notification addresses for call centre communications.</p>
            </div>
          </section>
        </div>
      </section>
    </main>
  );
}
