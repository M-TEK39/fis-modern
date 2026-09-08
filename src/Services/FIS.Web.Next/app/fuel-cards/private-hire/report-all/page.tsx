import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { ApiUnavailable, FuelCardTable } from "@/app/fuel-cards/_components";
import { FuelCardApiError, getAllPrivateHireFuelCards } from "@/lib/api-fuel-cards";
import { getSession } from "@/lib/session";

export default async function PrivateHireFuelCardReportPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/fuel-cards/private-hire/report-all" />
      </main>
    );
  if (
    !session.roles.some(
      (role) => role.localeCompare("Fuelcards", undefined, { sensitivity: "accent" }) === 0,
    )
  )
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to access private hire Fuelcard reports.</h2>
        </section>
      </main>
    );
  try {
    const cards = await getAllPrivateHireFuelCards();
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="private-hire-report-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Private hire fuelcard reports</p>
              <h1 id="private-hire-report-title">Report for ALL Private Hire Vehicle Fuelcards</h1>
              <p>View all private hire fuelcard records from the compatible legacy table.</p>
            </div>
            <Link className="button button-secondary" href="/fuel-cards">
              Menu
            </Link>
          </header>
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="private-hire-report-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Live result</p>
                <h2 id="private-hire-report-results-title">
                  Private hire fuelcards ({cards.length})
                </h2>
              </div>
            </div>
            <FuelCardTable cards={cards} privateHire />
          </section>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof FuelCardApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/fuel-cards/private-hire/report-all" />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable
          path="/fuel-cards/private-hire/report-all"
          subject="Private hire fuelcard report"
        />
      </main>
    );
  }
}
