import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import type { SessionState } from "@/lib/api-auth";
import { getSession } from "@/lib/session";

export async function getMonitorSession() {
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

function normalizedRole(role: string) {
  return role.toLocaleLowerCase().replace(/[^a-z0-9]/g, "");
}

export function hasCallCentreAccess(session: Extract<SessionState, { status: "authenticated" }>) {
  return session.roles.some((role) => normalizedRole(role) === "callcentre");
}

export function hasReportsAccess(session: Extract<SessionState, { status: "authenticated" }>) {
  return session.roles.some((role) => normalizedRole(role) === "reports");
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
