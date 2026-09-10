import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { MenuSection } from "@/components/ui/menu-section";
import { getSession } from "@/lib/auth/session";

const REPORTS_ROLE = "Reports";

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
      <h2>You do not have permission to access Fines.</h2>
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
      <h2>Fines maintenance could not be opened.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/fines">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

export default async function FinesPage() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/fines" />
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
      <section className="vehicle-card" aria-labelledby="fines-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Fines</p>
            <h1 id="fines-title">Fines Maintenance Menu</h1>
            <p>Capture, update, and remove traffic-fine records.</p>
          </div>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </header>

        <div className="vehicle-menu-tiles">
          <MenuSection title="Fines Maintenance Information">
            <Link className="vehicle-menu-link" href="/fines/help">
              Fines Maintenance Information / Help
            </Link>
          </MenuSection>
          <MenuSection title="Fine Maintenance">
            <Link className="vehicle-menu-link" href="/fines/maintenance">
              1) Fine Maintenance
            </Link>
            <Link className="vehicle-menu-link" href="/fines/delete">
              2) Delete a Fine
            </Link>
          </MenuSection>
          <MenuSection title="Traffic Dept Maintenance">
            <Link className="vehicle-menu-link" href="/fines/traffic-dept">
              3) Add / Update &amp; Delete Traffic Dept
            </Link>
          </MenuSection>
        </div>
      </section>
    </main>
  );
}
