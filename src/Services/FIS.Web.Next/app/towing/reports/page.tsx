import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

const REPORTS_ROLE = "Reports";

function hasReportsRole(roles: readonly string[]) {
  return roles.some((role) => role.localeCompare(REPORTS_ROLE, undefined, { sensitivity: "accent" }) === 0);
}

export default async function TowingReportsPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/towing/reports" /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>Towing reports could not be opened.</h2><p className="muted-copy">Retry when the FIS API is available.</p></section></main>;
  if (!hasReportsRole(session.roles)) return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to access Towing reports.</h2><p className="muted-copy">This menu requires the legacy Reports role.</p></section></main>;

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="towing-reports-title">
        <header className="vehicle-page-header"><div><p className="eyebrow">Road Side Assistance</p><h1 id="towing-reports-title">Road Side Assistance Report Menu</h1><p>Run the report workflows available in the legacy Towing menu.</p></div><Link className="button button-secondary" href="/towing">Towing Menu</Link></header>
        <section className="vehicle-menu-tile">
          <h2 className="vehicle-menu-header">Road Side Assistance Section</h2>
          <div className="vehicle-menu-body"><Link className="vehicle-menu-link" href="/towing/reports/request">1) Road Side Assistance Request Report</Link></div>
        </section>
        <section className="vehicle-menu-tile">
          <h2 className="vehicle-menu-header">Road Side Assistance DATA Section</h2>
          <div className="vehicle-menu-body">
            <Link className="vehicle-menu-link" href="/towing/tow-truck-data/report">1) All Assistance Firm General Data</Link>
            <Link className="vehicle-menu-link" href="/towing/reports/firm-date">2) Road Side Call&apos;s for a Firm</Link>
          </div>
        </section>
      </section>
    </main>
  );
}
