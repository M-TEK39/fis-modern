import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { TaxiHeader, TaxiRestricted } from "@/app/taxis/_components";
import { getSession } from "@/lib/session";

export default async function TaxiHelpPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/taxis/help" />
      </main>
    );
  if (
    !session.roles.some(
      (role) =>
        role.localeCompare("Private Hire Vehicles", undefined, { sensitivity: "accent" }) === 0,
    )
  )
    return (
      <main className="page-shell vehicle-page-shell">
        <TaxiRestricted subject="Taxi help" />
      </main>
    );
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card">
        <TaxiHeader
          title="Taxi Maintenance Information / Help"
          description="Reference the taxi maintenance guidance."
        />
        <section className="vehicle-status-maintenance-panel">
          <p className="muted-copy">
            The taxi maintenance manual is available as a separate reference document.
          </p>
          <Link
            className="button button-primary"
            href="/legacy/taxis/Doc_taxis.htm"
            target="_blank"
          >
            Open taxi help
          </Link>
        </section>
      </section>
    </main>
  );
}
