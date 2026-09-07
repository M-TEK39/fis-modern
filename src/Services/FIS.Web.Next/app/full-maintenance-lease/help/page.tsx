import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { AccessRestricted, ApiUnavailable, FmlFrame, hasFmlPermission } from "@/app/full-maintenance-lease/_components";
import { getSession } from "@/lib/session";

export default async function FmlHelpPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/full-maintenance-lease/help" /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><ApiUnavailable message="FML help could not be opened." /></main>;
  if (!hasFmlPermission(session.accessLevel)) return <main className="page-shell vehicle-page-shell"><AccessRestricted /></main>;

  return (
    <FmlFrame title="Full Maintenance Lease Information / Help" description="The established FML workflow for lease vehicle tariff and contract administration.">
      <section className="form-card">
        <div className="form-card-header"><h2>Lease vehicle workflow</h2><p>Use the same order as the original FML menu.</p></div>
        <div className="form-card-body">
          <ol>
            <li>Capture lease vehicle tariff periods and submit contract terms for authority review.</li>
            <li>Import a tariff file when several lease tariff periods must be captured together.</li>
            <li>Extend the latest tariff period when the current lease period is renewed.</li>
            <li>Add a tariff period for an in-service lease vehicle that does not yet have one.</li>
            <li>Use the reports menu for expiry, open-contract, no-contract, maintenance, and kilometre-utilization checks.</li>
          </ol>
          <p className="muted-copy">The FIS API keeps the original table and field meanings. Expanded fields are used only when they exist; client-era fields remain the fallback.</p>
        </div>
      </section>
      <div className="button-row"><Link className="button button-primary" href="/full-maintenance-lease">FML Menu</Link><Link className="button button-secondary" href="/full-maintenance-lease/reports">FML Reports</Link></div>
    </FmlFrame>
  );
}
