"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import { createTracking, TrackingApiError, updateTracking } from "@/lib/api-tracking";
import { getSession } from "@/lib/session";

class TrackingValidationError extends Error {}

function text(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function integer(formData: FormData, key: string, label: string, required = true) {
  const value = text(formData, key);
  if (!value && !required) return null;
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0)
    throw new TrackingValidationError(`${label} must be a positive whole number.`);
  return parsed;
}

function date(formData: FormData, key: string, label: string) {
  const value = text(formData, key);
  if (!value) return null;
  if (
    !/^\d{4}-\d{2}-\d{2}$/.test(value) ||
    Number.isNaN(new Date(`${value}T00:00:00.000Z`).getTime())
  )
    throw new TrackingValidationError(`${label} is invalid.`);
  return value;
}

function returnPath(formData: FormData) {
  const value = text(formData, "returnPath");
  return value.startsWith("/") && !value.startsWith("//") ? value : "/tracking/maintenance";
}

function redirectWithMessage(
  path: string,
  key: "saved" | "updated" | "error",
  message: string,
): never {
  redirect(
    `${path}${path.includes("?") ? "&" : "?"}${new URLSearchParams({ [key]: message }).toString()}`,
  );
}

function apiErrorMessage(error: unknown) {
  if (error instanceof TrackingApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "not-found") return "The tracking record was not found.";
    if (error.reason === "unavailable")
      return "The Tracking service is temporarily unavailable. Please try again.";
  }
  return "The Tracking operation failed. Please try again.";
}

export async function saveTrackingAction(formData: FormData) {
  const trackCode = integer(formData, "trackCode", "Tracking code", false);
  const path = returnPath(formData);
  const session = await getSession();
  if (session.status !== "authenticated")
    redirectWithMessage(
      path,
      "error",
      "Your session has expired. Sign in again before continuing.",
    );
  if (!hasVehicleManagementPermission(session.accessLevel))
    redirectWithMessage(path, "error", "You do not have permission to maintain Tracking records.");

  try {
    const trackNumber = text(formData, "trackNumber") || null;
    const previousGg = text(formData, "previousGg") || null;
    const followGg = text(formData, "followGg") || null;
    const status = text(formData, "trackStatus") || null;
    const type = text(formData, "trackType") || null;
    const note = text(formData, "trackNote") || null;
    if (trackNumber && trackNumber.length > 50)
      throw new TrackingValidationError("Tracker number must be 50 characters or fewer.");
    if (previousGg && previousGg.length > 50)
      throw new TrackingValidationError("Previous GG must be 50 characters or fewer.");
    if (followGg && followGg.length > 50)
      throw new TrackingValidationError("Follow GG must be 50 characters or fewer.");
    if (status && status.length > 100)
      throw new TrackingValidationError("Tracker status must be 100 characters or fewer.");
    if (type && type.length > 100)
      throw new TrackingValidationError("Tracker type must be 100 characters or fewer.");
    if (note && note.length > 4000)
      throw new TrackingValidationError("Tracking notes must be 4000 characters or fewer.");
    const input = {
      vmf_code: integer(formData, "vmfCode", "Vehicle", false),
      track_num: trackNumber,
      gg_previous: previousGg,
      gg_follow: followGg,
      install_date: date(formData, "installDate", "Install date"),
      remove_date: date(formData, "removeDate", "Remove date"),
      track_status: status,
      track_type: type,
      track_note: note,
    };
    if (trackCode) {
      await updateTracking(trackCode, input);
      revalidatePath("/tracking");
      revalidatePath("/tracking/maintenance");
      redirectWithMessage(path, "updated", "Tracking record updated.");
    }
    await createTracking(input);
    revalidatePath("/tracking");
    revalidatePath("/tracking/maintenance");
    redirectWithMessage(path, "saved", "Tracking record captured.");
  } catch (error) {
    redirectWithMessage(
      path,
      "error",
      error instanceof TrackingValidationError ? error.message : apiErrorMessage(error),
    );
  }
}
