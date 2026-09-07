import { connection } from "next/server";
import { redirect } from "next/navigation";

import SessionRecovery from "@/app/home/session-recovery";
import { hasRole } from "@/app/drivers/access";
import type { SessionState } from "@/lib/api-auth";
import { getSession } from "@/lib/session";

export async function getLicenseSession() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  return session;
}

export function sessionMessage(session: SessionState, returnPath: string) {
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={returnPath} /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>The sign-in service is temporarily unavailable.</h2><p className="muted-copy">Retry when the FIS API is available.</p></section></main>;
  return null;
}

export function accessRestricted(message = "Your profile does not include Licence access.") {
  return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>{message}</h2></section></main>;
}

export function hasLicenseAccess(session: Extract<SessionState, { status: "authenticated" }>) {
  return hasRole(session.roles, "Licence");
}

export function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] ?? "" : value ?? "";
}

export function dateInput(value: string | null | undefined) {
  return value?.slice(0, 10) ?? "";
}
