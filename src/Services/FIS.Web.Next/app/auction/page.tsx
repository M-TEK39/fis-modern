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

export default async function AuctionPage() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/auction" />
      </main>
    );
  }

  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Auction could not be opened.</h2>
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
          <h2>You do not have permission to access Auction.</h2>
          <p className="muted-copy">This menu requires the Reports role.</p>
        </section>
      </main>
    );
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="auction-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Auction</p>
            <h1 id="auction-title">Auction Maintenance Menu</h1>
            <p>Maintain Johannesburg auction records and open the associated reports.</p>
          </div>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </header>
        <div className="vehicle-menu-tiles">
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">Auction Maintenance Information</h2>
            <div className="vehicle-menu-body">
              <Link className="vehicle-menu-link" href="/auction/help">
                Auction Maintenance Information / Help
              </Link>
            </div>
          </section>
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">Auction Maintenance for Johannesburg Garage</h2>
            <div className="vehicle-menu-body">
              <Link className="vehicle-menu-link" href="/auction/maintenance">
                1) Auction Maintenance
              </Link>
              <Link className="vehicle-menu-link" href="/auction/delete-vehicle">
                2) Delete a Vehicle on Auction
              </Link>
            </div>
          </section>
          <section className="vehicle-menu-tile">
            <h2 className="vehicle-menu-header">Auction Reports Menu</h2>
            <div className="vehicle-menu-body">
              <Link className="vehicle-menu-link" href="/auction/reports">
                Open Auction Reports Menu
              </Link>
            </div>
          </section>
        </div>
      </section>
    </main>
  );
}
