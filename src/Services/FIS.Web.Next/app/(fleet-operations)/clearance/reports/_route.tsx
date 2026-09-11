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

async function ClearanceReportsContent({
  routePath = "/clearance/reports",
}: {
  routePath?: string;
}) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") {
    return <SessionRecovery returnPath={routePath} />;
  }
  if (session.status === "unavailable") {
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">API unavailable</p>
        <h2>Clearance reports could not be opened.</h2>
        <p className="muted-copy">Retry when the FIS API is available.</p>
      </section>
    );
  }
  if (!hasRole(session.roles, REPORTS_ROLE)) {
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Access restricted</p>
        <h2>You do not have permission to access Clearance reports.</h2>
      </section>
    );
  }

  return (
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
  );
}

export default function ClearanceReportsPage(props: { routePath?: string }) {
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
        <Suspense fallback={<RouteLoading />}>
          <ClearanceReportsContent {...props} />
        </Suspense>
      </section>
    </main>
  );
}
