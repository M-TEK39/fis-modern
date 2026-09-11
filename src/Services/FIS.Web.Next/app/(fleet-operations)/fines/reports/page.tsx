import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import RouteLoading from "@/components/app-shell/route-loading";
import { getSession } from "@/lib/auth/session";

const REPORTS_ROLE = "Reports";

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

async function FinesReportsContent() {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <SessionRecovery returnPath="/fines/reports" />;
  if (session.status === "unavailable") {
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">API unavailable</p>
        <h2>Fines reports could not be opened.</h2>
        <p className="muted-copy">Retry when the FIS API is available.</p>
      </section>
    );
  }
  if (!hasRole(session.roles, REPORTS_ROLE)) {
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Access restricted</p>
        <h2>You do not have permission to access Fines reports.</h2>
      </section>
    );
  }

  return (
    <section className="vehicle-card" aria-labelledby="fines-reports-title">
      <header className="vehicle-page-header">
        <div>
          <p className="eyebrow">Fines reports</p>
          <h1 id="fines-reports-title">Fines Reports Menu</h1>
          <p>Run the same report sequence as the legacy Fines report menu.</p>
        </div>
        <Link className="button button-secondary" href="/fines">
          Fines Menu
        </Link>
      </header>
      <div className="vehicle-menu-tiles">
        <section className="vehicle-menu-tile">
          <h2 className="vehicle-menu-header">One Vehicle Fines Reports</h2>
          <div className="vehicle-menu-body">
            <Link className="vehicle-menu-link" href="/fines/reports/one-vehicle">
              1) Fines Report on ONE Vehicle
            </Link>
          </div>
        </section>
        <section className="vehicle-menu-tile">
          <h2 className="vehicle-menu-header">Other Fines Reports</h2>
          <div className="vehicle-menu-body">
            <Link className="vehicle-menu-link" href="/fines/reports/select">
              2) Fines Report, for a Dept / Site, for a period
            </Link>
            <Link className="vehicle-menu-link" href="/fines/reports/appear-date">
              3) Fines Report on Appear Date
            </Link>
            <Link className="vehicle-menu-link" href="/fines/reports/letter">
              4) Submission to Re-Issue Fine in Transport Officer&apos;s Name
            </Link>
            <Link className="vehicle-menu-link" href="/fines/reports/traffic-dept">
              5) Traffic Dept Detail
            </Link>
            <Link className="vehicle-menu-link" href="/reports/fis-report">
              Return To Main Page
            </Link>
          </div>
        </section>
      </div>
    </section>
  );
}

export default function FinesReportsPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <Suspense fallback={<RouteLoading />}>
        <FinesReportsContent />
      </Suspense>
    </main>
  );
}
