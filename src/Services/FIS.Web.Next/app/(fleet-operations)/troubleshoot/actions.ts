"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  removeTripsWithoutRoutes,
  saveApproverRanks,
  TroubleshootApiError,
  updateTroubleshootLogs,
} from "@/lib/api/fleet-operations/api-troubleshoot";
import { getSession } from "@/lib/auth/session";

const TROUBLESHOOTING_ROLE = "Trouble Shooting";

function text(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}
function integer(formData: FormData, key: string, label: string, required = true) {
  const value = text(formData, key);
  if (!value && !required) return null;
  const parsed = Number(value);
  if (!value || !Number.isSafeInteger(parsed) || parsed < 0)
    throw new Error(`${label} must be a whole number.`);
  return parsed;
}

function date(formData: FormData, key: string, label: string) {
  const value = text(formData, key);
  if (!value) return null;
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) throw new Error(`${label} is invalid.`);
  const [year, month, day] = value.split("-").map(Number);
  const parsed = new Date(Date.UTC(year, month - 1, day));
  if (
    parsed.getUTCFullYear() !== year ||
    parsed.getUTCMonth() !== month - 1 ||
    parsed.getUTCDate() !== day
  )
    throw new Error(`${label} is invalid.`);
  return value;
}

function pathWithMessage(path: string, message: string, key = "error") {
  const separator = path.includes("?") ? "&" : "?";
  redirect(`${path}${separator}${key}=${encodeURIComponent(message)}`);
}

function hasTroubleshootingRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(TROUBLESHOOTING_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

async function authorizeTroubleshooting() {
  const session = await getSession();
  if (session.status === "unavailable")
    return {
      ok: false as const,
      message: "The sign-in service is temporarily unavailable. Please try again.",
    };
  if (session.status !== "authenticated")
    return {
      ok: false as const,
      message: "Your session has expired. Sign in again before continuing.",
    };
  if (!hasTroubleshootingRole(session.roles))
    return {
      ok: false as const,
      message: "You do not have permission to maintain Troubleshoot records.",
    };
  return { ok: true as const };
}

function apiMessage(error: unknown, fallback: string) {
  if (error instanceof TroubleshootApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable")
      return "The Troubleshoot service is temporarily unavailable. Please try again.";
    if (error.reason === "not-found") return "The requested Troubleshoot record was not found.";
    return error.message || fallback;
  }
  return fallback;
}

export async function updateTroubleshootLogsAction(formData: FormData) {
  const access = await authorizeTroubleshooting();
  if (!access.ok) pathWithMessage("/troubleshoot/log", access.message);
  const userAccessCode = integer(formData, "userAccessCode", "User/site", false);
  if (!userAccessCode || userAccessCode <= 0)
    pathWithMessage("/troubleshoot/log", "Select a user/site before updating logs.");

  let updated = 0;
  try {
    updated = await updateTroubleshootLogs();
    revalidatePath("/troubleshoot/log");
  } catch (error) {
    pathWithMessage(
      `/troubleshoot/log?userAccessCode=${userAccessCode}`,
      apiMessage(error, "Troubleshoot logs could not be updated."),
    );
  }
  redirect(`/troubleshoot/log?userAccessCode=${userAccessCode}&saved=${updated}`);
}

export async function saveApproverRanksAction(formData: FormData) {
  const access = await authorizeTroubleshooting();
  if (!access.ok) pathWithMessage("/troubleshoot/approver-ranks", access.message);
  const page = integer(formData, "page", "Page", false) ?? 1;
  const returnPath = `/troubleshoot/approver-ranks?page=${page}`;

  try {
    const ids = formData.getAll("rankId");
    const names = formData.getAll("rankName");
    const descriptions = formData.getAll("description");
    const ranks = names.map((_, index) => {
      const idValue = ids[index];
      const id = typeof idValue === "string" && idValue ? Number(idValue) : 0;
      const rankName = typeof names[index] === "string" ? names[index].trim() : "";
      const description = typeof descriptions[index] === "string" ? descriptions[index].trim() : "";
      if (!Number.isSafeInteger(id) || id < 0 || !rankName || !description)
        throw new Error("Every approver rank needs a name and description.");
      if (rankName.length > 200 || description.length > 500)
        throw new Error("Approver rank text is too long.");
      return { id, rankName, description };
    });
    if (ranks.length === 0) throw new Error("Add at least one approver rank before saving.");
    await saveApproverRanks(ranks);
    revalidatePath("/troubleshoot/approver-ranks");
  } catch (error) {
    pathWithMessage(returnPath, apiMessage(error, "Approver ranks could not be saved."));
  }
  redirect(`${returnPath}&saved=1`);
}

export async function removeTripsWithoutRoutesAction(formData: FormData) {
  const access = await authorizeTroubleshooting();
  if (!access.ok) pathWithMessage("/troubleshoot/remove-trips-no-routes", access.message);
  if (text(formData, "confirm") !== "yes")
    pathWithMessage(
      "/troubleshoot/remove-trips-no-routes",
      "Confirm that you want to remove trips without routes.",
    );

  let removed = 0;
  try {
    const fromDate = date(formData, "fromDate", "From date");
    const toDate = date(formData, "toDate", "To date");
    if (fromDate && toDate && fromDate > toDate)
      throw new Error("The To date must be on or after the From date.");
    removed = await removeTripsWithoutRoutes({
      fromDate: fromDate ?? undefined,
      toDate: toDate ?? undefined,
    });
    revalidatePath("/troubleshoot/remove-trips-no-routes");
  } catch (error) {
    pathWithMessage(
      "/troubleshoot/remove-trips-no-routes",
      apiMessage(error, "Trips without routes could not be removed."),
    );
  }
  redirect(`/troubleshoot/remove-trips-no-routes?saved=${removed}`);
}
