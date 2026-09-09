import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { MenuSection } from "@/components/ui/menu-section";
import { getSession } from "@/lib/session";

const CLEARANCE_ROLE = "Clearance";

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
      <h2>You do not have permission to access Clearance.</h2>
    </section>
  );
}

export default async function ClearancePage() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/clearance" />
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
          <h2>Clearance could not be opened.</h2>
          <p className="muted-copy">Retry when the FIS API is available.</p>
          <div className="button-row">
            <Link className="button button-primary" href="/clearance">
              Try again
            </Link>
            <Link className="button button-secondary" href="/login">
              Sign in
            </Link>
          </div>
        </section>
      </main>
    );
  }

  if (!hasRole(session.roles, CLEARANCE_ROLE)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="clearance-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Clearance</p>
            <h1 id="clearance-title">Clearance Maintenance Menu</h1>
            <p>Capture clearance records and maintain the merchant list.</p>
          </div>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </header>

        <div className="vehicle-menu-tiles">
          <MenuSection title="Clearance Maintenance Menu">
            <Link className="vehicle-menu-link" href="/clearance/entry">
              1) Enter Clearance
            </Link>
          </MenuSection>
          <MenuSection title="Merchants">
            <Link className="vehicle-menu-link" href="/clearance/merchant">
              1) Merchant Maintenance
            </Link>
          </MenuSection>
        </div>
      </section>
    </main>
  );
}
