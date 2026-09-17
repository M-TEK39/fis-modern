import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { hasLegacyRole } from "@/app/(administration)/drivers/access";
import type { SessionState } from "@/lib/auth/api-auth";
import { getSession } from "@/lib/auth/session";

export async function getTrackingSession() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  return session;
}

export function sessionMessage(session: SessionState, returnPath: string) {
  if (session.status !== "expired" && session.status !== "unavailable") return null;
  return (
    <main className="page-shell vehicle-page-shell">
      <SessionRecovery returnPath={returnPath} />
    </main>
  );
}

export function accessRestricted(message: string) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-status-card" role="alert">
        <p className="eyebrow">Access restricted</p>
        <h2>{message}</h2>
      </section>
    </main>
  );
}

export function hasTrackingAccess(session: Extract<SessionState, { status: "authenticated" }>) {
  return hasLegacyRole(session.roles, "Tracking");
}

export function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

export function parsePositiveInteger(value: string) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

export function dateInput(value: string | null | undefined) {
  return value?.slice(0, 10) ?? "";
}

export function reportDateRange(start: string, end: string) {
  const today = new Date();
  const defaultStart = new Date(today);
  defaultStart.setFullYear(today.getFullYear() - 50);
  const defaultEnd = new Date(today);
  defaultEnd.setFullYear(today.getFullYear() + 10);
  return {
    startDate: start || defaultStart.toISOString().slice(0, 10),
    endDate: end || defaultEnd.toISOString().slice(0, 10),
  };
}
