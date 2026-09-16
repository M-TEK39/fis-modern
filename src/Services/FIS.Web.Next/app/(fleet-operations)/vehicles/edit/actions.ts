"use server";

import { redirect } from "next/navigation";

import {
  searchVehiclesAgainstApi,
  VehicleCreateApiError,
} from "@/lib/api/vehicles/api-vehicle-create";
import {
  updateVehicleAgainstApi,
  VehicleEditApiError,
  type VehicleUpdateRequest,
} from "@/lib/api/vehicles/api-vehicle-edit";
import { hasVehicleMasterRole } from "@/app/(fleet-operations)/vehicles/access";
import { getSession } from "@/lib/auth/session";
import type { VehicleEditSearchActionState } from "@/app/(fleet-operations)/vehicles/edit/vehicle-edit-types";

export type VehicleEditActionState = {
  status: "idle" | "success" | "error";
  message?: string;
};

const initialSearchState: VehicleEditSearchActionState = { status: "idle", results: [] };

class VehicleFormValidationError extends Error {}

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
    throw new VehicleFormValidationError(`${label} is required.`);
  }

  const parsed = Number(value);
  if (!Number.isInteger(parsed)) {
    throw new VehicleFormValidationError(`${label} must be a whole number.`);
  }

  return parsed;
}

async function authorizeVehicleEdit() {
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

  if (!hasVehicleMasterRole(session.roles)) {
    return {
      ok: false as const,
      message: "You do not have permission to maintain Vehicle Master records.",
    };
  }

  return { ok: true as const };
}

function searchErrorMessage(error: VehicleCreateApiError) {
  if (error.reason === "unauthorized") {
    return "Your session has expired. Sign in again before searching.";
  }

  if (error.reason === "forbidden") {
    return "You do not have permission to search Vehicle Master records.";
  }

  if (error.reason === "unavailable") {
    return "The vehicle search service is temporarily unavailable. Please try again.";
  }

  return "The vehicle search service returned an unexpected response. Please try again.";
}

export async function searchVehicleEditAction(
  _previousState: VehicleEditSearchActionState = initialSearchState,
  formData: FormData,
): Promise<VehicleEditSearchActionState> {
  if (getText(formData, "intent") === "reset") {
    return initialSearchState;
  }

  const access = await authorizeVehicleEdit();
  if (!access.ok) {
    return { status: "error", message: access.message, results: [] };
  }

  const searchTerm = getText(formData, "searchTerm");
  if (!searchTerm) {
    return { status: "error", message: "Enter a GG or GP number to search.", results: [] };
  }

  try {
    const results = await searchVehiclesAgainstApi(searchTerm);
    return results.length > 0
      ? { status: "success", results }
      : { status: "success", message: "No matching vehicles found.", results: [] };
  } catch (error) {
    if (error instanceof VehicleCreateApiError) {
      return { status: "error", message: searchErrorMessage(error), results: [] };
    }

    console.error(
      "FIS vehicle edit search failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return { status: "error", message: "Vehicle search failed. Please try again.", results: [] };
  }
}

function buildUpdateRequest(formData: FormData): VehicleUpdateRequest {
  const fleetNumber = getText(formData, "fleetNumber").toUpperCase();
  const registrationNumber = getText(formData, "registrationNumber").toUpperCase();
  const engineNumber = getText(formData, "engineNumber").toUpperCase();
  const chassisNumber = getText(formData, "chassisNumber").toUpperCase();
  const colour = getText(formData, "colour");

  if (!fleetNumber || !registrationNumber || !engineNumber || !chassisNumber || !colour) {
    throw new VehicleFormValidationError(
      "Complete all required vehicle identity and colour fields.",
    );
  }

  if (engineNumber === chassisNumber) {
    throw new VehicleFormValidationError("Chassis and engine numbers must be different.");
  }

  if (registrationNumber.length < 6 || registrationNumber.length > 8) {
    throw new VehicleFormValidationError("GP number must be between 6 and 8 characters.");
  }

  const modelCode = getInteger(formData, "modelCode", "Model");
  const typeCode = getInteger(formData, "typeCode", "Vehicle type");
  const statusCode = getInteger(formData, "statusCode", "Vehicle status");
  const locationCode = getInteger(formData, "locationCode", "Location");
  const takeOnOdo = getInteger(formData, "takeOnOdo", "Take-on odometer");
  const currentOdo = getInteger(formData, "currentOdo", "Current odometer");
  const tare = getInteger(formData, "tare", "Tare");
  const gvm = getInteger(formData, "gvm", "GVM", false);
  const year = getInteger(formData, "yearManufactured", "Year manufactured");

  if (
    modelCode <= 0 ||
    typeCode <= 0 ||
    statusCode <= 0 ||
    locationCode <= 0 ||
    takeOnOdo < 0 ||
    currentOdo < 0 ||
    tare < 0 ||
    (gvm !== null && gvm < 0) ||
    year < 1900 ||
    year > new Date().getFullYear() + 1
  ) {
    throw new VehicleFormValidationError(
      "Enter valid reference values and non-negative vehicle measurements.",
    );
  }

  return {
    model_code: modelCode,
    type_code: typeCode,
    vehicle_status_code: statusCode,
    location_code: locationCode,
    fleet_number: fleetNumber,
    registration_number: registrationNumber,
    engine_number_1: engineNumber,
    chassis_number: chassisNumber,
    take_on_odo: takeOnOdo,
    current_odo: currentOdo,
    tare,
    gvm,
    year_manufactured: year,
    colour,
    ifms_vehicle_register_number:
      getText(formData, "ifmsVehicleRegisterNumber").toUpperCase() || null,
    natis_model_number: getText(formData, "natisModelNumber").toUpperCase() || null,
    recalculate_tariff: formData.get("recalculateTariff") === "on",
  };
}

function updateErrorMessage(error: VehicleEditApiError) {
  if (error.reason === "unauthorized") {
    return "Your session has expired or you are no longer allowed to edit vehicles. Sign in again.";
  }

  if (error.reason === "forbidden") {
    return "You do not have permission to edit Vehicle Master records.";
  }

  if (error.reason === "not-found") {
    return "The vehicle could not be found. Return to search and choose another record.";
  }

  if (error.reason === "unavailable") {
    return `The vehicle service is temporarily unavailable. ${error.message}`;
  }

  return error.message || "The vehicle service returned an unexpected response. Please try again.";
}

export async function updateVehicleAction(
  _previousState: VehicleEditActionState = { status: "idle" },
  formData: FormData,
): Promise<VehicleEditActionState> {
  const vmfCode = Number(getText(formData, "vmfCode"));
  if (!Number.isInteger(vmfCode) || vmfCode <= 0) {
    return { status: "error", message: "The selected vehicle is invalid." };
  }

  const access = await authorizeVehicleEdit();
  if (!access.ok) {
    return { status: "error", message: access.message };
  }

  let request: VehicleUpdateRequest;
  try {
    request = buildUpdateRequest(formData);
  } catch (error) {
    if (error instanceof VehicleFormValidationError) {
      return { status: "error", message: error.message };
    }

    throw error;
  }

  try {
    await updateVehicleAgainstApi(vmfCode, request);
  } catch (error) {
    if (error instanceof VehicleEditApiError) {
      return { status: "error", message: updateErrorMessage(error) };
    }

    console.error(
      "FIS vehicle update failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return { status: "error", message: "Vehicle update failed. Please try again." };
  }

  redirect(`/vehicles/edit?updated=${encodeURIComponent(vmfCode)}`);
}
