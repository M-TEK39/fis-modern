import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

const REPORTS_ROLE = "Reports";

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

export default async function ClearanceReportsPage({
  routePath = "/clearance/reports",
}: {
  routePath?: string;
}) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  }
  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Clearance reports could not be opened.</h2>
          <p className="muted-copy">Retry when the FIS API is available.</p>
        </section>
      </main>
    );
  }
  if (!hasRole(session.roles, REPORTS_ROLE)) {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to access Clearance reports.</h2>
        </section>
      </main>
    );
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="clearance-reports-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Clearance reports</p>
            <h1 id="clearance-reports-title">Clearance Report Menu</h1>
            <p>Run the same universal clearance report available in the legacy report menu.</p>
          </div>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </header>
        <div className="vehicle-menu-tiles">
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">Clearance Maintenance Menu</h2>
            <div className="vehicle-menu-body">
              <Link className="vehicle-menu-link" href="/clearance/reports/universal">
                1) Clearance Universal
              </Link>
              <Link className="vehicle-menu-link" href="/home">
                Return To Main Page
              </Link>
            </div>
          </section>
        </div>
      </section>
    </main>
  );
}
