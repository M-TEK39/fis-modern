import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

const ROLE = "Private Hire Vehicles";

function hasRole(roles: readonly string[]) {
  return roles.some((role) => role.localeCompare(ROLE, undefined, { sensitivity: "accent" }) === 0);
}

export default async function PrivateHireMaintenanceMenuPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/private-hire/maintenance-menu" /></main>;
  if (!hasRole(session.roles)) return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to maintain Private Hire Vehicles.</h2></section></main>;

  return (
    <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="private-hire-maintenance-title">
      <header className="vehicle-page-header"><div><p className="eyebrow">Private Hire Vehicles</p><h1 id="private-hire-maintenance-title">Private Hire Maintenance Menu</h1><p>Maintain vehicle and contractor records.</p></div><Link className="button button-secondary" href="/private-hire">Menu</Link></header>
      <div className="vehicle-menu-tiles">
        <section className="vehicle-menu-tile"><h2 className="vehicle-menu-header">Private Hire vehicle maintenance</h2><div className="vehicle-menu-body"><Link className="vehicle-menu-link" href="/private-hire/maintenance?mode=add">1) Add a Private Hire vehicle</Link><Link className="vehicle-menu-link" href="/private-hire/maintenance?mode=edit">2) Edit a Private Hire vehicle</Link><Link className="vehicle-menu-link" href="/private-hire/maintenance?mode=delete">3) Delete a Private Hire vehicle</Link></div></section>
        <section className="vehicle-menu-tile"><h2 className="vehicle-menu-header">Private Hire contractor maintenance</h2><div className="vehicle-menu-body"><Link className="vehicle-menu-link" href="/private-hire/maintenance?mode=contractor-add">1) Add a Private Hire contractor</Link><Link className="vehicle-menu-link" href="/private-hire/maintenance?mode=contractor-edit">2) Edit a Private Hire contractor</Link><Link className="vehicle-menu-link" href="/private-hire/maintenance?mode=contractor-delete">3) Delete a Private Hire contractor</Link></div></section>
        <section className="vehicle-menu-tile"><h2 className="vehicle-menu-header">Private Hire reports</h2><div className="vehicle-menu-body"><Link className="vehicle-menu-link" href="/private-hire/reports/all-vehicles">All Private Hire vehicles</Link><Link className="vehicle-menu-link" href="/private-hire/reports/one-vehicle">One Private Hire vehicle</Link><Link className="vehicle-menu-link" href="/private-hire/reports/contractors">Private Hire contractors</Link><Link className="vehicle-menu-link" href="/private-hire/reports/per-site">Vehicles per site</Link><Link className="vehicle-menu-link" href="/private-hire/reports/per-department">Vehicles per department</Link><Link className="vehicle-menu-link" href="/private-hire/reports/per-department-in-service">Vehicles per department in service</Link><Link className="vehicle-menu-link" href="/private-hire/reports/per-company">Vehicles per hire company</Link></div></section>
      </div>
    </section></main>
  );
}
