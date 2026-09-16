import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import type { SessionState } from "@/lib/auth/api-auth";
import { getSession } from "@/lib/auth/session";
import type { VehicleOption } from "@/lib/api/vehicles/api-vehicles";

export async function getLogsheetSession() {
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

export function hasLogsheetAccess(session: Extract<SessionState, { status: "authenticated" }>) {
  return session.roles.some((role) => {
    const normalized = normalizedRole(role);
    return normalized === "logsheets" || normalized === "reports";
  });
}

export function hasLogsheetManagerAccess(
  session: Extract<SessionState, { status: "authenticated" }>,
) {
  const code = Number(session.userAccessCode);
  return Number.isInteger(code) && [279, 47, 38].includes(code);
}

export function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

export function parsePositiveInteger(value: string) {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

export function filterVehicles(options: readonly VehicleOption[], search: string, mode: string) {
  const normalized = search.trim().toLocaleLowerCase();
  if (!normalized) return [];
  const isGp = mode.toLocaleUpperCase() === "GP";
  return options
    .filter((vehicle) =>
      (isGp ? vehicle.registrationNumber : vehicle.fleetNumber)
        ?.toLocaleLowerCase()
        .includes(normalized),
    )
    .sort((left, right) => {
      const leftValue = isGp ? left.registrationNumber : left.fleetNumber;
      const rightValue = isGp ? right.registrationNumber : right.fleetNumber;
      const leftExact = leftValue?.trim().toLocaleLowerCase() === normalized ? 0 : 1;
      const rightExact = rightValue?.trim().toLocaleLowerCase() === normalized ? 0 : 1;
      return leftExact - rightExact || left.vmfCode - right.vmfCode;
    });
}

export function statusMessage(query: Record<string, string | string[] | undefined>) {
  const key = ["saved", "updated", "deleted", "error"].find((name) => query[name]);
  return key ? { key, value: queryValue(query[key]) } : null;
}
