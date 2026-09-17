import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import type { SessionState } from "@/lib/auth/api-auth";
import { getSession } from "@/lib/auth/session";

export async function getLogbookSession() {
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

export function hasLogbookAccess(session: Extract<SessionState, { status: "authenticated" }>) {
  return session.roles.some(
    (role) => role.toLocaleLowerCase().replace(/[^a-z0-9]/g, "") === "logbooks",
  ) || session.roles.some((role) => {
    const normalized = role.toLocaleLowerCase().replace(/[^a-z0-9]/g, "");
    return normalized === "systemadministrator";
  });
}

export function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

export function queryValues(value: string | string[] | undefined) {
  return Array.isArray(value) ? value : value ? [value] : [];
}

export function parsePositiveInteger(value: string) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}
