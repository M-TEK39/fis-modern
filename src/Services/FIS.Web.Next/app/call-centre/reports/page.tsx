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

export default async function CallCentreReportsPage() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/call-centre/reports" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Service unavailable</p>
          <h2>Call Centre reports could not be opened.</h2>
          <p className="muted-copy">Retry when the FIS API is available.</p>
        </section>
      </main>
    );
  if (!hasRole(session.roles, REPORTS_ROLE))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to access Call Centre reports.</h2>
          <p className="muted-copy">This menu requires the Reports role.</p>
        </section>
      </main>
    );

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="call-centre-reports-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Call Centre / Reports</p>
            <h1 id="call-centre-reports-title">Call Centre Reports Menu</h1>
            <p>Run the same eight report workflows as the legacy Call Centre report menu.</p>
          </div>
          <Link className="button button-secondary" href="/call-centre">
            Call Centre Menu
          </Link>
        </header>
        <div className="vehicle-menu-tiles">
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">One Call Incident Reports</h2>
            <div className="vehicle-menu-body">
              <Link className="vehicle-menu-link" href="/call-centre/reports/one-reference">
                1) Incident Report on ONE Reference Number
              </Link>
              <Link className="vehicle-menu-link" href="/call-centre/reports/one-vehicle">
                2) Incident Report on ONE Vehicle
              </Link>
            </div>
          </section>
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">All Call Incident Reports</h2>
            <div className="vehicle-menu-body">
              <Link className="vehicle-menu-link" href="/call-centre/reports/all-reference">
                3) Incident Report on ALL Call Numbers
              </Link>
            </div>
          </section>
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">Other Call Incident Reports</h2>
            <div className="vehicle-menu-body">
              <Link className="vehicle-menu-link" href="/call-centre/reports/dept-site-period">
                4) Call Centre Info, for a Dept / Site, for a period
              </Link>
              <Link className="vehicle-menu-link" href="/call-centre/reports/statistics">
                5) Call Centre Statistics
              </Link>
              <Link className="vehicle-menu-link" href="/call-centre/reports/clo-report">
                6) Client Liaison Officer (CLO) Report
              </Link>
              <Link className="vehicle-menu-link" href="/call-centre/reports/data-access">
                7) Full Report on Data Access Detail
              </Link>
              <Link className="vehicle-menu-link" href="/call-centre/reports/open-calls">
                8) Open Calls (Calls NOT Closed)
              </Link>
              <Link className="vehicle-menu-link" href="/reports/fis-report">
                Return To Main Page
              </Link>
            </div>
          </section>
        </div>
      </section>
    </main>
  );
}
