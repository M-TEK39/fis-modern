import { connection } from "next/server";
import { redirect } from "next/navigation";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { hasRole } from "@/app/(administration)/drivers/access";
import type { SessionState } from "@/lib/auth/api-auth";
import { getSession } from "@/lib/auth/session";

export async function getTripSession() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  return session;
}

export function tripSessionMessage(session: SessionState, returnPath: string) {
  if (session.status === "expired") {
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={returnPath} />
      </main>
    );
  }

  if (session.status === "unavailable") {
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>The sign-in service is temporarily unavailable.</h2>
          <p className="muted-copy">Retry when the FIS API is available.</p>
        </section>
      </main>
    );
  }

  return null;
}

export function tripAccessRestricted(
  message = "Your profile does not include Trip Authority access.",
) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Access restricted</p>
        <h2>{message}</h2>
      </section>
    </main>
  );
}

export function hasTripAuthorityAccess(
  session: Extract<SessionState, { status: "authenticated" }>,
) {
  return (
    hasRole(session.roles, "TripAuthorities") || hasRole(session.roles, "Trip Authorities")
  );
}

export function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

export function parsePositiveInteger(value: string) {
  const parsed = Number(value.trim());
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}
