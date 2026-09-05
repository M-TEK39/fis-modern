import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { getSession } from "@/lib/session";

const CALL_CENTRE_ROLE = "Call Centre";

function hasRole(roles: readonly string[], role: string) {
  return roles.some((candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0);
}

export default async function BookingHelpPage() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/call-centre/bookings/help" /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Service unavailable</p><h2>Booking help could not be opened.</h2><p className="muted-copy">Retry when the sign-in service is available.</p></section></main>;
  if (!hasRole(session.roles, CALL_CENTRE_ROLE)) return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to access booking help.</h2><p className="muted-copy">This page requires the Call Centre role.</p></section></main>;

  return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="booking-help-title"><header className="vehicle-page-header"><div><p className="eyebrow">Call Centre / Booking Section</p><h1 id="booking-help-title">Booking information/help</h1><p>Booking guidance and process notes.</p></div><Link className="button button-secondary" href="/call-centre">Call Centre Menu</Link></header><section className="vehicle-status-maintenance-panel"><h2>Booking help</h2><p className="muted-copy">Use the booking workflow to maintain vehicle reservations and review booking status. Notification recipients are maintained separately in the booking notification section.</p><p className="muted-copy">The legacy booking capture/edit form remains a separate workflow and is being migrated only after its source screens are verified.</p></section><div className="vehicle-footer-actions"><Link className="button button-secondary" href="/call-centre/bookings/notifications">Booking notification addresses</Link><Link className="button button-secondary" href="/home">Home</Link></div></section></main>;
}
