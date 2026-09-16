import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { TaxiHeader, TaxiRestricted } from "@/app/(fleet-operations)/taxis/_components";
import { hasTaxiAccess } from "@/app/(fleet-operations)/taxis/access";
import { getSession } from "@/lib/auth/session";

async function TaxiMaintenanceInfoPageContent() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/taxis/maintenance/info" />
      </main>
    );
  if (!hasTaxiAccess(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <TaxiRestricted subject="Taxi Information Maintenance" />
      </main>
    );
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card">
        <TaxiHeader
          title="Taxi Information Maintenance"
          description="Maintain taxi information records using the legacy-compatible workflow."
        />
        <section className="vehicle-status-maintenance-panel">
          <p className="muted-copy">
            Taxi request and log maintenance is available from the Taxi menu. Additional vehicle
            information is preserved by the API compatibility layer.
          </p>
        </section>
      </section>
    </main>
  );
}

export default function TaxiMaintenanceInfoPage() {
  return (
    <Suspense fallback={<RouteLoading />}>
      <TaxiMaintenanceInfoPageContent />
    </Suspense>
  );
}
