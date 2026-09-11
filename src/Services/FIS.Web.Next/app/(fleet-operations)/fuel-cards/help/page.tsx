import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";

const FUEL_CARDS_ROLE = "Fuelcards";

function hasFuelCardsRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(FUEL_CARDS_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

function HelpFallback() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Checking help access...</p>
    </div>
  );
}

async function FuelCardsHelpContent() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return <SessionRecovery returnPath="/fuel-cards/help" />;
  if (!hasFuelCardsRole(session.roles))
    return (
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Access restricted</p>
        <h2>You do not have permission to access Fuelcard help.</h2>
      </section>
    );

  return (
    <div className="document-help-content">
      <iframe
        src="/legacy/fuelcard/Doc_Fuelcards.htm"
        title="Fuelcard Maintenance Help"
        className="help-iframe document-help-frame"
      />
    </div>
  );
}

export default function FuelCardsHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card document-help-card" aria-labelledby="fuel-cards-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Fuelcards</p>
            <h1 id="fuel-cards-help-title">Fuelcard Maintenance Information / Help</h1>
            <p>Reference the original fuelcard maintenance guidance.</p>
          </div>
          <Link className="button button-secondary" href="/fuel-cards">
            Back
          </Link>
        </header>
        <Suspense fallback={<HelpFallback />}>
          <FuelCardsHelpContent />
        </Suspense>
      </section>
    </main>
  );
}
