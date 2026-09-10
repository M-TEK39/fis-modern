import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { MenuSection } from "@/components/ui/menu-section";
import { getSession } from "@/lib/auth/session";

const CALL_CENTRE_ROLE = "Call Centre";

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
      <h2>You do not have permission to access Call Centre.</h2>
      <p className="muted-copy">This menu requires the Call Centre role.</p>
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
      <h2>Call Centre could not be opened.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/call-centre">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

export default async function CallCentrePage() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/call-centre" />
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

  if (!hasRole(session.roles, CALL_CENTRE_ROLE)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="call-centre-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Call Centre</p>
            <h1 id="call-centre-title">Call Centre Maintenance Menu</h1>
            <p>Capture, update, and report on call centre incidents.</p>
          </div>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </header>

        <div className="vehicle-menu-tiles">
          <MenuSection title="Call Centre Maintenance Menu">
            <Link className="vehicle-menu-link" href="/call-centre/help">
              Call Centre Maintenance Information / Help
            </Link>
          </MenuSection>

          <MenuSection title="Incident Section">
            <Link className="vehicle-menu-link" href="/call-centre/incident/capture">
              1) Capture a New Incident
            </Link>
            <Link className="vehicle-menu-link" href="/call-centre/incident/edit">
              2) Edit / Update an Existing Incident
            </Link>
            <Link className="vehicle-menu-link" href="/call-centre/notifications">
              3) Notification List for eMail Addresses
            </Link>
            <Link className="vehicle-menu-link" href="/call-centre/reports">
              4) Reports
            </Link>
          </MenuSection>

          <MenuSection title="Booking Section">
            <Link className="vehicle-menu-link" href="/call-centre/bookings/notifications">
              1) Notification e-mail Addresses
            </Link>
            <Link className="vehicle-menu-link" href="/call-centre/bookings/help">
              Booking information/help
            </Link>
          </MenuSection>
        </div>
      </section>
    </main>
  );
}
