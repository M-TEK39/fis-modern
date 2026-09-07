import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

export default async function FuelCardsHelpPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/fuel-cards/help" /></main>;
  if (!session.roles.some((role) => role.localeCompare("Fuelcards", undefined, { sensitivity: "accent" }) === 0)) return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to access Fuelcard help.</h2></section></main>;
  return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="fuel-cards-help-title"><header className="vehicle-page-header"><div><p className="eyebrow">Fuelcards</p><h1 id="fuel-cards-help-title">Fuelcard Maintenance Information / Help</h1><p>Reference the original fuelcard maintenance guidance.</p></div><Link className="button button-secondary" href="/fuel-cards">Back</Link></header><iframe src="/legacy/fuelcard/Doc_Fuelcards.htm" title="Fuelcard Maintenance Help" className="help-iframe" /></section></main>;
}
