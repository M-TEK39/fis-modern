"use server";

import {
  searchVehiclesForBarcode,
  updateVehicleBarcode,
  VehicleBarcodeApiError,
  type VehicleBarcodeVehicle,
} from "@/lib/api/vehicles/api-vehicle-barcode";
import { getSession } from "@/lib/auth/session";

const VEHICLE_MANAGEMENT_PERMISSION = 1;

export type VehicleBarcodeSearchActionState = {
  status: "idle" | "success" | "error";
  message?: string;
  results: VehicleBarcodeVehicle[];
};

export type VehicleBarcodeUpdateActionState = {
  status: "idle" | "success" | "error";
  message?: string;
};

const initialSearchState: VehicleBarcodeSearchActionState = { status: "idle", results: [] };

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
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

async function authorizeBarcodeMaintenance() {
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
      message: "You do not have permission to maintain vehicle barcodes.",
    };
  }

  return { ok: true as const };
}

function apiErrorMessage(error: unknown, operation: "search" | "update") {
  if (error instanceof VehicleBarcodeApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "not-found")
      return "The selected vehicle could not be found. Search again.";
    if (error.reason === "unavailable")
      return `The vehicle barcode ${operation} service is temporarily unavailable. Please try again.`;
    return error.message;
  }

  return `The vehicle barcode could not be ${operation === "search" ? "searched" : "updated"}. Please try again.`;
}

export async function searchVehicleBarcodeAction(
  _previousState: VehicleBarcodeSearchActionState = initialSearchState,
  formData: FormData,
): Promise<VehicleBarcodeSearchActionState> {
  const access = await authorizeBarcodeMaintenance();
  if (!access.ok) {
    return { status: "error", message: access.message, results: [] };
  }

  const searchTerm = getText(formData, "searchTerm");
  const searchMode = getText(formData, "searchMode").toUpperCase() === "GP" ? "GP" : "GG";
  if (!searchTerm) {
    return { status: "error", message: "Enter a GG or GP number to search.", results: [] };
  }

  try {
    const matches = await searchVehiclesForBarcode(searchTerm);
    const results = matches
      .filter((vehicle) => {
        const value = searchMode === "GP" ? vehicle.registrationNumber : vehicle.fleetNumber;
        return value?.toLocaleLowerCase().includes(searchTerm.toLocaleLowerCase()) === true;
      })
      .toSorted((left, right) => {
        const leftValue = searchMode === "GP" ? left.registrationNumber : left.fleetNumber;
        const rightValue = searchMode === "GP" ? right.registrationNumber : right.fleetNumber;
        const leftExact =
          leftValue?.trim().localeCompare(searchTerm, undefined, { sensitivity: "accent" }) === 0;
        const rightExact =
          rightValue?.trim().localeCompare(searchTerm, undefined, { sensitivity: "accent" }) === 0;
        return Number(rightExact) - Number(leftExact) || left.vmfCode - right.vmfCode;
      });

    return results.length > 0
      ? { status: "success", results }
      : { status: "success", message: "No matching vehicles found.", results: [] };
  } catch (error) {
    console.error(
      "FIS vehicle barcode search failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return { status: "error", message: apiErrorMessage(error, "search"), results: [] };
  }
}

export async function updateVehicleBarcodeAction(
  _previousState: VehicleBarcodeUpdateActionState = { status: "idle" },
  formData: FormData,
): Promise<VehicleBarcodeUpdateActionState> {
  const access = await authorizeBarcodeMaintenance();
  if (!access.ok) {
    return { status: "error", message: access.message };
  }

  const vmfCode = Number(getText(formData, "vmfCode"));
  if (!Number.isInteger(vmfCode) || vmfCode <= 0) {
    return { status: "error", message: "The selected vehicle is invalid." };
  }

  const barcode = getText(formData, "barcode");
  if (barcode.length > 50) {
    return { status: "error", message: "Barcode must be 50 characters or fewer." };
  }

  try {
    await updateVehicleBarcode(vmfCode, barcode);
    return { status: "success", message: "Vehicle barcode updated successfully." };
  } catch (error) {
    console.error(
      "FIS vehicle barcode update failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return { status: "error", message: apiErrorMessage(error, "update") };
  }
}
