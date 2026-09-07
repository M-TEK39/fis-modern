import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { type SessionState } from "@/lib/api-auth";
import { getSession } from "@/lib/session";

export async function getJobCardSession() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  return session;
}

export function SessionProblem({ returnPath }: Readonly<{ returnPath: string }>) {
  return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={returnPath} /></main>;
}

export function hasJobCardRole(roles: readonly string[], kind: "capturer" | "authorizer") {
  return roles.some((role) => {
    const normalized = role.toLocaleLowerCase().replace(/[^a-z0-9]/g, "");
    return normalized.includes("jobcard") && normalized.includes(kind === "capturer" ? "captur" : "author");
  });
}

export function hasLegacyJobCardAccess(session: Extract<SessionState, { status: "authenticated" }>) {
  const accessLevel = Number(session.accessLevel);
  return Number.isInteger(accessLevel) && (accessLevel & (1 | 32)) !== 0;
}

export function canUseJobCardArea(session: Extract<SessionState, { status: "authenticated" }>) {
  return hasLegacyJobCardAccess(session) || hasJobCardRole(session.roles, "capturer") || hasJobCardRole(session.roles, "authorizer");
}

export function accessRestricted(message: string) {
  return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>{message}</h2></section></main>;
}

export function sessionMessage(session: SessionState, returnPath: string) {
  return session.status === "expired" || session.status === "unavailable" ? <SessionProblem returnPath={returnPath} /> : null;
}

export function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] ?? "" : value ?? "";
}

export function filterByVehicle(cards: Awaited<ReturnType<typeof import("@/lib/api-job-cards").getJobCards>>, query: string, mode: string) {
  const normalized = query.trim().toLocaleLowerCase();
  if (!normalized) return cards;
  return cards.filter((card) => {
    const value = mode.toUpperCase() === "GP" ? card.registrationNumber : card.ggNumber;
    return value?.toLocaleLowerCase().includes(normalized) || String(card.jobCardId) === normalized;
  });
}
