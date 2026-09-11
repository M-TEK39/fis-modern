"use server";

import { redirect } from "next/navigation";
import { after } from "next/server";

import {
  changeVehicleStatusAgainstApi,
  searchVehiclesForStatusPage,
  VehicleStatusApiError,
  type VehicleStatusVehicle,
} from "@/lib/api/vehicles/api-vehicle-status";
import { getSession } from "@/lib/auth/session";

const VEHICLE_MANAGEMENT_PERMISSION = 1;
const VEHICLE_STATUS_ROLES = ["Acquisition", "Logistics", "TSS", "Workshop"];
const SOLD_STATUS_CODE = 5;
const STOLEN_STATUS_CODE = 4;

export type VehicleStatusActionState = {
  status: "idle" | "success" | "error";
  message?: string;
  results?: VehicleStatusVehicle[];
  page?: number;
  total?: number;
  totalPages?: number;
  searchMode?: "GG" | "GP";
  searchTerm?: string;
};

const initialSearchState: VehicleStatusActionState = { status: "idle", results: [] };

class VehicleStatusValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getInteger(formData: FormData, key: string, label: string): number;
function getInteger(formData: FormData, key: string, label: string, required: false): number | null;
function getInteger(
  formData: FormData,
  key: string,
  label: string,
  required = true,
): number | null {
  const value = getText(formData, key);
  if (!value && !required) {
    return null;
  }

  if (!value) {
    throw new VehicleStatusValidationError(`${label} is required.`);
  }

  const parsed = Number(value);
  if (!Number.isInteger(parsed)) {
    throw new VehicleStatusValidationError(`${label} must be a whole number.`);
  }

  return parsed;
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function hasVehicleManagementPermission(accessLevel?: string) {
  if (!accessLevel) {
    return false;
  }

  try {
    return (
      (BigInt(accessLevel) & BigInt(VEHICLE_MANAGEMENT_PERMISSION)) ===
      BigInt(VEHICLE_MANAGEMENT_PERMISSION)
    );
  } catch {
    return false;
  }
}

async function authorizeStatusMaintenance() {
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

  if (!hasVehicleManagementPermission(session.accessLevel)) {
    return {
      ok: false as const,
      message: "You do not have permission to maintain vehicle statuses.",
    };
  }

  if (!VEHICLE_STATUS_ROLES.some((role) => hasRole(session.roles, role))) {
    return {
      ok: false as const,
      message: "Your account does not have the required access level to edit vehicle statuses.",
    };
  }

  return { ok: true as const };
}

function searchErrorMessage(error: VehicleStatusApiError) {
  if (error.reason === "unauthorized") {
    return "Your session has expired. Sign in again before searching.";
  }

  if (error.reason === "unavailable") {
    return "The vehicle search service is temporarily unavailable. Please try again.";
  }

  return "The vehicle search service returned an unexpected response. Please try again.";
}

function statusErrorMessage(error: VehicleStatusApiError) {
  if (error.reason === "unauthorized") {
    return "Your session has expired or you are no longer allowed to change vehicle statuses. Sign in again.";
  }

  if (error.reason === "not-found") {
    return "The vehicle could not be found. Search again and choose another record.";
  }

  if (error.reason === "unavailable") {
    return "The vehicle status service is temporarily unavailable. Please try again.";
  }

  return error.message || "The vehicle status service returned an unexpected response.";
}

export async function searchVehicleStatusAction(
  _previousState: VehicleStatusActionState = initialSearchState,
  formData: FormData,
): Promise<VehicleStatusActionState> {
  const access = await authorizeStatusMaintenance();
  if (!access.ok) {
    return { status: "error", message: access.message, results: [] };
  }

  const searchTerm = getText(formData, "searchTerm");
  const searchMode = getText(formData, "searchMode").toUpperCase() === "GP" ? "GP" : "GG";
  const rawPage = Number(getText(formData, "page"));
  const requestedPage = Number.isSafeInteger(rawPage) && rawPage > 0 ? rawPage : 1;
  if (!searchTerm) {
    return { status: "error", message: "Enter a GG or GP number to search.", results: [] };
  }

  try {
    const result = await searchVehiclesForStatusPage(searchTerm, searchMode, requestedPage);

    return result.items.length > 0
      ? {
          status: "success",
          results: result.items,
          page: result.page,
          total: result.total,
          totalPages: result.totalPages,
          searchMode,
          searchTerm,
        }
      : { status: "success", message: "No matching vehicles found.", results: [] };
  } catch (error) {
    if (error instanceof VehicleStatusApiError) {
      return { status: "error", message: searchErrorMessage(error), results: [] };
    }

    console.error(
      "FIS vehicle status search failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return { status: "error", message: "Vehicle search failed. Please try again.", results: [] };
  }
}

function getSafeReturnUrl(formData: FormData) {
  const value = getText(formData, "returnUrl");
  return value.startsWith("/") && !value.startsWith("//") ? value : "";
}

function buildRedirectUrl(vmfCode: number, returnUrl: string) {
  const query = new URLSearchParams({ vmfCode: String(vmfCode), updated: "1" });
  if (returnUrl) {
    query.set("returnUrl", returnUrl);
  }

  return `/vehicles/status-maintenance?${query.toString()}`;
}

export async function changeVehicleStatusAction(
  _previousState: VehicleStatusActionState = { status: "idle" },
  formData: FormData,
): Promise<VehicleStatusActionState> {
  const access = await authorizeStatusMaintenance();
  if (!access.ok) {
    return { status: "error", message: access.message };
  }

  let redirectUrl: string | null = null;

  try {
    const vmfCode = getInteger(formData, "vmfCode", "Vehicle");
    const currentStatusCode = getInteger(formData, "currentStatusCode", "Current status");
    const newStatusCode = getInteger(formData, "newStatusCode", "Next status");
    const effectiveDate = getText(formData, "effectiveDate");
    const endOdometer = getInteger(formData, "endOdometer", "Odometer", false);
    const soldAmount = getText(formData, "soldAmount");
    const soldDate = getText(formData, "soldDate");
    const soldTo = getText(formData, "soldTo");
    const comments = getText(formData, "comments");
    const siteCode = getInteger(formData, "siteCode", "Book Under Site", false);

    if (vmfCode <= 0 || currentStatusCode < 0 || newStatusCode < 1 || newStatusCode > 12) {
      throw new VehicleStatusValidationError("The selected vehicle status is invalid.");
    }

    if (newStatusCode === currentStatusCode) {
      throw new VehicleStatusValidationError(
        "Select a status different from the current vehicle status.",
      );
    }

    if (!/^\d{4}-\d{2}-\d{2}$/.test(effectiveDate)) {
      throw new VehicleStatusValidationError("Effective From date is required and must be valid.");
    }

    const parsedEffectiveDate = new Date(`${effectiveDate}T00:00:00Z`);
    if (Number.isNaN(parsedEffectiveDate.getTime()) || parsedEffectiveDate > new Date()) {
      throw new VehicleStatusValidationError("Effective From date cannot be in the future.");
    }

    if (endOdometer !== null && endOdometer < 0) {
      throw new VehicleStatusValidationError("Odometer cannot be negative.");
    }

    if (newStatusCode === STOLEN_STATUS_CODE && (siteCode === null || siteCode <= 0)) {
      throw new VehicleStatusValidationError(
        "Book Under Site is required when marking a vehicle as Stolen.",
      );
    }

    if (newStatusCode === SOLD_STATUS_CODE) {
      if (!soldAmount || !soldDate || !soldTo) {
        throw new VehicleStatusValidationError(
          "Sold Amount, Sold Date, and Sold To are required for Sold status.",
        );
      }

      throw new VehicleStatusValidationError(
        "Sold status is not available until the C# status API can persist Sold Amount, Sold Date, and Sold To.",
      );
    }

    await changeVehicleStatusAgainstApi(
      vmfCode,
      newStatusCode,
      newStatusCode === STOLEN_STATUS_CODE ? siteCode : null,
      effectiveDate,
      comments,
    );

    if (endOdometer !== null || comments) {
      after(() => {
        console.warn(
          "Vehicle status API does not persist the status-maintenance odometer/comments fields; status update completed.",
        );
      });
    }

    redirectUrl = buildRedirectUrl(vmfCode, getSafeReturnUrl(formData));
  } catch (error) {
    if (error instanceof VehicleStatusValidationError) {
      return { status: "error", message: error.message };
    }

    if (error instanceof VehicleStatusApiError) {
      return { status: "error", message: statusErrorMessage(error) };
    }

    console.error(
      "FIS vehicle status update failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return { status: "error", message: "Vehicle status update failed. Please try again." };
  }

  if (redirectUrl) {
    redirect(redirectUrl);
  }

  return { status: "error", message: "Vehicle status update could not be completed." };
}
