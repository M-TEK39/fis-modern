import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import RouteLoading from "@/components/app-shell/route-loading";
import { getSession } from "@/lib/auth/session";
import { hasFinesAccess } from "@/app/(fleet-operations)/fines/access";

async function FinesReportSelectionContent() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <SessionRecovery returnPath="/fines/reports/select" />;
  if (session.status === "unavailable")
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">API unavailable</p>
        <h2>Fines report selection could not be opened.</h2>
      </section>
    );
  if (!hasFinesAccess(session.roles))
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Access restricted</p>
        <h2>You do not have permission to select Fines reports.</h2>
      </section>
    );

  return (
    <section className="vehicle-card" aria-labelledby="fines-report-selection-title">
      <header className="vehicle-page-header">
        <div>
          <p className="eyebrow">Fines reports</p>
          <h1 id="fines-report-selection-title">Fines Report Selection</h1>
          <p>Select an option below and proceed.</p>
        </div>
        <Link className="button button-secondary" href="/fines/reports">
          Back to Fines Reports
        </Link>
      </header>
      <section className="vehicle-menu-tiles" aria-label="Fines report types">
        <section className="vehicle-menu-tile">
          <h2 className="vehicle-menu-header">Department / Site</h2>
          <div className="vehicle-menu-body">
            <Link className="vehicle-menu-link" href="/fines/reports/dept-period">
              Department/Site
            </Link>
          </div>
        </section>
        <section className="vehicle-menu-tile">
          <h2 className="vehicle-menu-header">Metro</h2>
          <div className="vehicle-menu-body">
            <Link className="vehicle-menu-link" href="/fines/reports/metro">
              Metro (Municipality)
            </Link>
          </div>
        </section>
        <section className="vehicle-menu-tile">
          <h2 className="vehicle-menu-header">Vehicle</h2>
          <div className="vehicle-menu-body">
            <Link className="vehicle-menu-link" href="/fines/reports/vehicle">
              Vehicle
            </Link>
          </div>
        </section>
        <section className="vehicle-menu-tile">
          <h2 className="vehicle-menu-header">All Fines</h2>
          <div className="vehicle-menu-body">
            <Link className="vehicle-menu-link" href="/fines/reports/all">
              A List of all Fines
            </Link>
          </div>
        </section>
      </section>
    </section>
  );
}

export default function FinesReportSelectionPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <Suspense fallback={<RouteLoading />}>
        <FinesReportSelectionContent />
      </Suspense>
    </main>
  );
}
