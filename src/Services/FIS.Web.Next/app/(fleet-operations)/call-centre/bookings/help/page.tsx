import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";

const CALL_CENTRE_ROLE = "Call Centre";

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function HelpFallback() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Checking access...</p>
    </div>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Service unavailable</p>
      <h2>Booking help could not be opened.</h2>
      <p className="muted-copy">Retry when the sign-in service is available.</p>
    </section>
  );
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access booking help.</h2>
      <p className="muted-copy">This page requires the Call Centre role.</p>
    </section>
  );
}

async function BookingHelpContent() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return <SessionRecovery returnPath="/call-centre/bookings/help" />;
  if (session.status === "unavailable") return <ApiUnavailable />;
  if (!hasRole(session.roles, CALL_CENTRE_ROLE)) return <AccessRestricted />;

  return (
    <>
      <div className="module-help-content">
        <details className="module-help-disclosure">
          <summary>Booking help</summary>
          <div className="module-help-section">
            <p className="module-help-intro">
              Use the booking workflow to maintain vehicle reservations and review booking status.
              Notification recipients are maintained separately in the booking notification section.
            </p>
            <p className="module-help-note">
              The legacy booking capture/edit form remains a separate workflow and is being migrated
              only after its source screens are verified.
            </p>
          </div>
        </details>
      </div>
      <div className="vehicle-footer-actions">
        <Link className="button button-secondary" href="/call-centre/bookings/notifications">
          Booking notification addresses
        </Link>
        <Link className="button button-secondary" href="/home">
          Home
        </Link>
      </div>
    </>
  );
}

export default function BookingHelpPage() {
  return (
    <main className="page-shell vehicle-page-shell">
      <article className="vehicle-card module-help-page" aria-labelledby="booking-help-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Call Centre / Booking Section</p>
            <h1 id="booking-help-title">Booking information/help</h1>
            <p>Booking guidance and process notes.</p>
          </div>
          <Link className="button button-secondary" href="/call-centre">
            Call Centre Menu
          </Link>
        </header>
        <Suspense fallback={<HelpFallback />}>
          <BookingHelpContent />
        </Suspense>
      </article>
    </main>
  );
}
