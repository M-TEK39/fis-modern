import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { MenuSection } from "@/components/ui/menu-section";
import { getSession } from "@/lib/auth/session";

const TOWING_ROLE = "Towing";

function hasTowingRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(TOWING_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access Road Side Assistance.</h2>
      <p className="muted-copy">Your account needs the legacy Towing role.</p>
    </section>
  );
}

export default async function TowingPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/towing" />
      </main>
    );
  }
  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Road Side Assistance could not be opened.</h2>
          <p className="muted-copy">Retry when the FIS API is available.</p>
        </section>
      </main>
    );
  }
  if (!hasTowingRole(session.roles)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="towing-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Road Side Assistance</p>
            <h1 id="towing-title">Road Side Assistance Menu</h1>
            <p>
              Maintain towing requests and assistance firm information using the established FIS
              workflow.
            </p>
          </div>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </header>

        <div className="vehicle-menu-tiles">
          <MenuSection title="Road Side Assistance Section">
            <Link className="vehicle-menu-link" href="/towing/request">
              1) Capture a New Request
            </Link>
            <Link className="vehicle-menu-link" href="/towing/request">
              2) Edit / Update / Delete an Existing Request
            </Link>
            <Link className="vehicle-menu-link" href="/towing/reports">
              3) Road Side Request Reports
            </Link>
          </MenuSection>

          <MenuSection title="Tow Truck Section">
            <Link className="vehicle-menu-link" href="/towing/tow-truck-data">
              4) Capture / Edit / Update Road Assistance Data
            </Link>
            <Link className="vehicle-menu-link" href="/towing/tow-truck-data/report">
              5) Show All Road Assistance Data
            </Link>
          </MenuSection>
        </div>
      </section>
    </main>
  );
}
