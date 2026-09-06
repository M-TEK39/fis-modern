import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

const REPORTS_ROLE = "Reports";

function hasRole(roles: readonly string[], role: string) {
  return roles.some((candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0);
}

export default async function AuctionReportsPage({ routePath = "/auction/reports" }: { routePath?: string }) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") {
    redirect("/login");
  }
  if (session.status === "expired") {
    return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
  }
  if (session.status === "unavailable") {
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>Auction reports could not be opened.</h2><p className="muted-copy">Retry when the FIS API is available.</p></section></main>;
  }
  if (!hasRole(session.roles, REPORTS_ROLE)) {
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to access Auction reports.</h2></section></main>;
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="auction-reports-title">
        <header className="vehicle-page-header"><div><p className="eyebrow">Auction reports</p><h1 id="auction-reports-title">Auction Reports Menu</h1><p>Run the same five report workflows available in the legacy Auction menu.</p></div><Link className="button button-secondary" href="/auction">Auction Menu</Link></header>
        <div className="vehicle-menu-tiles">
          <section className="vehicle-menu-tile"><h2 className="vehicle-menu-header">One Vehicle Auction Reports</h2><div className="vehicle-menu-body"><Link className="vehicle-menu-link" href="/auction/reports/one-vehicle">1) Auction Report on ONE Vehicle</Link></div></section>
          <section className="vehicle-menu-tile"><h2 className="vehicle-menu-header">All Vehicle&apos;s Auction Reports</h2><div className="vehicle-menu-body"><Link className="vehicle-menu-link" href="/auction/reports/all-vehicles">2) Auction Report on ALL Vehicle&apos;s</Link></div></section>
          <section className="vehicle-menu-tile"><h2 className="vehicle-menu-header">Other Auction Reports</h2><div className="vehicle-menu-body">
            <Link className="vehicle-menu-link" href="/auction/reports/sale-to-name">3) Auction Report on Sale to Name</Link>
            <Link className="vehicle-menu-link" href="/auction/reports/auction-gg">4) Auction Report of ONE Auction - Sort by GG Number</Link>
            <Link className="vehicle-menu-link" href="/auction/reports/auction-lot">5) Auction Report of ONE Auction - Sort by LOT Number</Link>
            <Link className="vehicle-menu-link" href="/auction">Return To Main Page</Link>
          </div></section>
        </div>
      </section>
    </main>
  );
}
