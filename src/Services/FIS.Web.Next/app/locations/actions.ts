"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { createLocation, deleteLocation, LocationApiError, updateLocation } from "@/lib/api-locations";
import { getSession } from "@/lib/session";

export type LocationActionState = { status: "idle" | "error"; message?: string };
const initialState: LocationActionState = { status: "idle" };

class LocationValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getRequiredText(formData: FormData, key: string, label: string, maxLength: number) {
  const value = getText(formData, key);
  if (!value) throw new LocationValidationError(`${label} is required.`);
  if (value.length > maxLength) throw new LocationValidationError(`${label} must be ${maxLength} characters or fewer.`);
  return value;
}

function getOptionalText(formData: FormData, key: string, label: string, maxLength: number) {
  const value = getText(formData, key);
  if (value.length > maxLength) throw new LocationValidationError(`${label} must be ${maxLength} characters or fewer.`);
  return value || null;
}

function getOptionalCoordinate(formData: FormData, key: string, label: string, min: number, max: number) {
  const value = getText(formData, key);
  if (!value) return null;
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < min || parsed > max) throw new LocationValidationError(`${label} must be between ${min} and ${max}.`);
  return parsed;
}

function getLocationId(formData: FormData) {
  const value = getText(formData, "locationId");
  if (!value) return 0;
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0 || parsed > 2_147_483_647) throw new LocationValidationError("The selected location is invalid.");
  return parsed;
}

function buildInput(formData: FormData) {
  return {
    locationName: getRequiredText(formData, "locationName", "Location name", 100),
    address: getOptionalText(formData, "address", "Address", 100),
    latitude: getOptionalCoordinate(formData, "latitude", "Latitude", -90, 90),
    longitude: getOptionalCoordinate(formData, "longitude", "Longitude", -180, 180),
  };
}

async function authorizeLocationMaintenance() {
  const session = await getSession();
  if (session.status === "unavailable") return { ok: false as const, message: "The sign-in service is temporarily unavailable. Please try again." };
  if (session.status !== "authenticated") return { ok: false as const, message: "Your session has expired. Sign in again before continuing." };
  return { ok: true as const };
}

function apiErrorMessage(error: unknown, operation: string) {
  if (error instanceof LocationApiError) {
    if (error.reason === "unauthorized") return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "not-found") return "The selected location no longer exists. Reload the list and try again.";
    if (error.reason === "conflict") return error.message;
    if (error.reason === "unavailable") return `The location ${operation} service is temporarily unavailable. Please try again.`;
    return error.message;
  }
  return `The location could not be ${operation}. Please try again.`;
}

export async function saveLocationAction(
  _previousState: LocationActionState = initialState,
  formData: FormData,
): Promise<LocationActionState> {
  const access = await authorizeLocationMaintenance();
  if (!access.ok) return { status: "error", message: access.message };

  try {
    const locationId = getLocationId(formData);
    const input = buildInput(formData);
    if (locationId === 0) await createLocation(input);
    else await updateLocation(locationId, input);
  } catch (error) {
    return { status: "error", message: error instanceof LocationValidationError ? error.message : apiErrorMessage(error, "saved") };
  }

  revalidatePath("/locations");
  redirect("/locations?saved=1");
}

export async function deleteLocationAction(formData: FormData) {
  const access = await authorizeLocationMaintenance();
  if (!access.ok) redirect(`/locations?error=${encodeURIComponent(access.message)}`);

  let locationId: number;
  try {
    locationId = getLocationId(formData);
    if (locationId === 0) throw new LocationValidationError("The selected location is invalid.");
    await deleteLocation(locationId);
  } catch (error) {
    const message = error instanceof LocationValidationError ? error.message : apiErrorMessage(error, "deleted");
    redirect(`/locations?error=${encodeURIComponent(message)}`);
  }

  revalidatePath("/locations");
  redirect("/locations?saved=1");
}
