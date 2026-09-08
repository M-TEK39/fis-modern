"use server";

import {
  getRecoveredVehicleDetails,
  getRecoveredVehicleSearch,
  RecoveredVehicleApiError,
  type RecoveredVehicleDetails,
  type RecoveredVehicleSearchMode,
  type RecoveredVehicleSearchResult,
  updateRecoveredVehicle,
} from "@/lib/api-recovered-vehicles";
import { getSession } from "@/lib/session";

type RecoveredVehicleActionResult = {
  status: "success" | "error";
  message?: string;
  matches?: RecoveredVehicleSearchResult[];
  details?: RecoveredVehicleDetails;
  newVmfCode?: number;
};

function hasDemoVehicleRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare("Demo Vehicles", undefined, { sensitivity: "base" }) === 0,
  );
}

async function authorizeRecoveredVehicle() {
  const session = await getSession();
  if (session.status === "unavailable") {
    return {
      ok: false as const,
      message: "The sign-in service is temporarily unavailable. Please try again.",
    };
  }

  if (session.status !== "authenticated") {
    return {
      ok: false as const,
      message: "Your session has expired. Sign in again before continuing.",
    };
  }

  if (!hasDemoVehicleRole(session.roles)) {
    return {
      ok: false as const,
      message: "You do not have permission to update recovered vehicles.",
    };
  }

  return { ok: true as const };
}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getMode(formData: FormData): RecoveredVehicleSearchMode | null {
  const mode = getText(formData, "mode").toUpperCase();
  return mode === "GG" || mode === "GP" ? mode : null;
}

function apiErrorMessage(error: unknown, fallback: string) {
  if (!(error instanceof RecoveredVehicleApiError)) {
    return fallback;
  }

  if (error.reason === "unauthorized") {
    return "Your session has expired. Sign in again before continuing.";
  }

  if (error.reason === "conflict") {
    return error.message;
  }

  if (error.reason === "not-found") {
    return "The selected vehicle could not be found.";
  }

  if (error.reason === "unavailable") {
    return "The recovered vehicle service is temporarily unavailable. Please try again.";
  }

  return error.message || fallback;
}

export async function searchRecoveredVehiclesAction(
  formData: FormData,
): Promise<RecoveredVehicleActionResult> {
  const access = await authorizeRecoveredVehicle();
  if (!access.ok) {
    return { status: "error", message: access.message };
  }

  const mode = getMode(formData);
  const search = getText(formData, "search");
  if (!mode) {
    return { status: "error", message: "Choose GG or GP before searching." };
  }

  if (!search) {
    return { status: "error", message: "Enter a GG or GP number before searching." };
  }

  try {
    return {
      status: "success",
      matches: await getRecoveredVehicleSearch(search, mode),
    };
  } catch (error) {
    console.error(
      "FIS recovered vehicle search failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return {
      status: "error",
      message: apiErrorMessage(error, "The recovered vehicle search could not be completed."),
    };
  }
}

export async function loadRecoveredVehicleAction(
  formData: FormData,
): Promise<RecoveredVehicleActionResult> {
  const access = await authorizeRecoveredVehicle();
  if (!access.ok) {
    return { status: "error", message: access.message };
  }

  const vmfCode = Number(getText(formData, "vmfCode"));
  if (!Number.isInteger(vmfCode) || vmfCode < 1) {
    return { status: "error", message: "The selected vehicle is invalid." };
  }

  try {
    return { status: "success", details: await getRecoveredVehicleDetails(vmfCode) };
  } catch (error) {
    console.error(
      "FIS recovered vehicle load failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return {
      status: "error",
      message: apiErrorMessage(error, "The recovered vehicle could not be loaded."),
    };
  }
}

function isValidRecoveredFleetNumber(value: string) {
  if (value.length < 4 || value.length > 8 || value[0] !== "G") {
    return false;
  }

  for (let index = 3; index < Math.min(value.length, 6); index += 1) {
    if (!/\d/.test(value[index])) {
      return false;
    }
  }

  return true;
}

function isValidDateOnly(value: string) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return false;
  }

  const [year, month, day] = value.split("-").map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  return (
    date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day
  );
}

export async function saveRecoveredVehicleAction(
  formData: FormData,
): Promise<RecoveredVehicleActionResult> {
  const access = await authorizeRecoveredVehicle();
  if (!access.ok) {
    return { status: "error", message: access.message };
  }

  const vmfCode = Number(getText(formData, "vmfCode"));
  const recoveredFleetNumber = getText(formData, "recoveredFleetNumber").toUpperCase();
  const dateChanged = getText(formData, "dateChanged");
  const newStatusCode = Number(getText(formData, "newStatusCode"));

  if (!Number.isInteger(vmfCode) || vmfCode < 1) {
    return { status: "error", message: "The selected vehicle is invalid." };
  }

  if (!isValidRecoveredFleetNumber(recoveredFleetNumber)) {
    return {
      status: "error",
      message: "The recovered GG number must start with G and contain a valid numeric suffix.",
    };
  }

  if (!isValidDateOnly(dateChanged)) {
    return { status: "error", message: "Enter a valid date changed value." };
  }

  if (
    !Number.isInteger(newStatusCode) ||
    newStatusCode < 1 ||
    newStatusCode > 12 ||
    newStatusCode === 4
  ) {
    return {
      status: "error",
      message: "Choose a valid non-stolen status for the recovered vehicle.",
    };
  }

  try {
    const result = await updateRecoveredVehicle({
      vmfCode,
      recoveredFleetNumber,
      dateChanged,
      newStatusCode,
    });
    return {
      status: "success",
      message: `Recovered vehicle created with VMF code ${result.newVmfCode}.`,
      details: result.updatedVehicle,
      newVmfCode: result.newVmfCode,
    };
  } catch (error) {
    console.error(
      "FIS recovered vehicle update failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return {
      status: "error",
      message: apiErrorMessage(error, "The recovered vehicle could not be updated."),
    };
  }
}
