import { redirect } from "next/navigation";
import { connection } from "next/server";

import {
  getJobCard,
  JobCardApiError,
  type JobCardRecord,
} from "@/lib/api/fleet-operations/api-job-cards";
import { type SessionState } from "@/lib/auth/api-auth";
import { getSession } from "@/lib/auth/session";

export type JobCardSearchParams = Record<string, string | string[] | undefined>;

export async function getJobCardSession() {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  return session;
}

export function hasJobCardRole(roles: readonly string[], kind: "capturer" | "authorizer") {
  return roles.some((role) => {
    const normalized = role.toLocaleLowerCase().replace(/[^a-z0-9]/g, "");
    return (
      normalized.includes("jobcard") &&
      normalized.includes(kind === "capturer" ? "captur" : "author")
    );
  });
}

export function hasLegacyJobCardAccess(
  session: Extract<SessionState, { status: "authenticated" }>,
) {
  const accessLevel = Number(session.accessLevel);
  return Number.isInteger(accessLevel) && (accessLevel & (1 | 32)) !== 0;
}

export function canUseJobCardArea(session: Extract<SessionState, { status: "authenticated" }>) {
  return (
    hasLegacyJobCardAccess(session) ||
    hasJobCardRole(session.roles, "capturer") ||
    hasJobCardRole(session.roles, "authorizer")
  );
}

export function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? (value[0] ?? "") : (value ?? "");
}

export function queryPage(value: string | string[] | undefined) {
  const parsed = Number(queryValue(value));
  return Number.isInteger(parsed) && parsed > 0 ? parsed : 1;
}

export function querySearchType(value: string | string[] | undefined): "GG" | "GP" {
  return queryValue(value).toUpperCase() === "GP" ? "GP" : "GG";
}

export async function getJobCardForSelection(jobCardId: number): Promise<JobCardRecord | null> {
  try {
    return await getJobCard(jobCardId);
  } catch (error) {
    if (error instanceof JobCardApiError && error.reason === "not-found") return null;
    throw error;
  }
}

export function jobCardPageHref(path: string, query: JobCardSearchParams, page: number) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (key === "page" || value === undefined) continue;
    for (const item of Array.isArray(value) ? value : [value]) params.append(key, item);
  }
  if (page > 1) params.set("page", String(page));
  const queryString = params.toString();
  return queryString ? `${path}?${queryString}` : path;
}

export function filterByVehicle(cards: JobCardRecord[], query: string, mode: string) {
  const normalized = query.trim().toLocaleLowerCase();
  if (!normalized) return cards;
  return cards.filter((card) => {
    const value = mode.toUpperCase() === "GP" ? card.registrationNumber : card.ggNumber;
    return value?.toLocaleLowerCase().includes(normalized) || String(card.jobCardId) === normalized;
  });
}
