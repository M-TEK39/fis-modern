import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { AccessRestricted, ApiUnavailable, FmlFrame, hasFmlPermission } from "@/app/full-maintenance-lease/_components";
import { getSession } from "@/lib/session";

export default async function FmlReportsPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/full-maintenance-lease/reports" /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><ApiUnavailable message="The FML reports menu could not be opened." /></main>;
  if (!hasFmlPermission(session.accessLevel)) return <main className="page-shell vehicle-page-shell"><AccessRestricted /></main>;

  return <FmlFrame title="FML Reports" description="Run the Full Maintenance Lease reports." backHref="/full-maintenance-lease"><div className="notice notice-info" role="note">Maintenance history and over-utilized reports use the shared date and vehicle filter screen.</div><section className="vehicle-menu-tile"><h2 className="vehicle-menu-header">Reports</h2><div className="vehicle-menu-body"><Link className="vehicle-menu-link" href="/full-maintenance-lease/reports/filter?report=maintenance-history">1) FML Vehicle Maintenance History</Link><Link className="vehicle-menu-link" href="/full-maintenance-lease/reports/contracts-expiring">2) FML Contracts that will expire in the next 3 months</Link><Link className="vehicle-menu-link" href="/full-maintenance-lease/reports/expired-contracts-open">3) Expired FML contracts with open client contracts</Link><Link className="vehicle-menu-link" href="/full-maintenance-lease/reports/vehicles-no-contracts">4) FML vehicles without client contracts</Link><Link className="vehicle-menu-link" href="/full-maintenance-lease/reports/filter?report=over-utilized">5) Over-utilized FML vehicles based on kilometres</Link></div></section></FmlFrame>;
}
