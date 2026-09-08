import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { TaxiHeader, TaxiRestricted } from "@/app/taxis/_components";
import { getSession } from "@/lib/session";

export default async function TaxiMaintenanceInfoPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/taxis/maintenance/info" />
      </main>
    );
  if (
    !session.roles.some(
      (role) =>
        role.localeCompare("Taxi Information Maintenance", undefined, { sensitivity: "accent" }) ===
        0,
    )
  )
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
