"use server";

import { redirect } from "next/navigation";

import {
  createVehicleAgainstApi,
  searchVehiclesAgainstApi,
  type CreateVehicleRequest,
  type VehicleSearchResult,
  VehicleCreateApiError,
} from "@/lib/api-vehicle-create";

export type CreateVehicleActionState = {
  status: "idle" | "error";
  message?: string;
};

const initialStatus: CreateVehicleActionState = { status: "idle" };

export type SearchVehicleActionState = {
  status: "idle" | "success" | "error";
  message?: string;
  results: VehicleSearchResult[];
};

class VehicleFormValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getInteger(formData: FormData, key: string, label: string): number;
function getInteger(formData: FormData, key: string, label: string, required: false): number | null;
function getInteger(formData: FormData, key: string, label: string, required = true): number | null {
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

function getDecimal(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  if (!value) {
    throw new VehicleFormValidationError(`${label} is required.`);
  }

  const parsed = Number(value);
  if (!Number.isFinite(parsed)) {
    throw new VehicleFormValidationError(`${label} must be a valid amount.`);
  }

  return parsed;
}

function getDate(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    throw new VehicleFormValidationError(`${label} is required.`);
  }

  const parsed = new Date(`${value}T00:00:00Z`);
  if (Number.isNaN(parsed.getTime())) {
    throw new VehicleFormValidationError(`${label} is invalid.`);
  }

  return value;
}

function validateAndBuildRequest(formData: FormData): { request: CreateVehicleRequest } | { error: string } {
  try {
    const fleetNumber = getText(formData, "fleetNumber").toUpperCase();
    const registrationNumber = getText(formData, "registrationNumber").toUpperCase();
    const engineNumber = getText(formData, "engineNumber").toUpperCase();
    const chassisNumber = getText(formData, "chassisNumber").toUpperCase();
    const colour = getText(formData, "colour");

    if (!fleetNumber || !registrationNumber || !engineNumber || !chassisNumber || !colour) {
      return { error: "Complete all required vehicle identity and colour fields." };
    }

    if (engineNumber === chassisNumber) {
      return { error: "Chassis and engine numbers must be different." };
    }

    const modelCode = getInteger(formData, "modelCode", "Model");
    const typeCode = getInteger(formData, "typeCode", "Vehicle type");
    const statusCode = getInteger(formData, "statusCode", "Vehicle status");
    const locationCode = getInteger(formData, "locationCode", "Location");
    const takeOnOdo = getInteger(formData, "takeOnOdo", "Take-on odometer");
    const tare = getInteger(formData, "tare", "Tare");
    const gvm = getInteger(formData, "gvm", "GVM", false);
    const year = getInteger(formData, "yearManufactured", "Year manufactured");
    const purchaseAmount = getDecimal(formData, "purchaseAmount", "Purchase amount");
    const takeOnDate = getDate(formData, "takeOnDate", "Take-on date");
    const purchaseDate = getDate(formData, "purchaseDate", "Purchase date");

    if (
      modelCode <= 0 ||
      typeCode <= 0 ||
      statusCode <= 0 ||
      locationCode <= 0 ||
      takeOnOdo < 0 ||
      tare < 0 ||
      (gvm !== null && gvm < 0) ||
      year < 1900 ||
      purchaseAmount < 0
    ) {
      return { error: "Enter valid positive reference values and non-negative measurements." };
    }

    const today = new Date().toISOString().slice(0, 10);
    if (takeOnDate > today || purchaseDate > today) {
      return { error: "Take-on and purchase dates cannot be in the future." };
    }

    if (purchaseDate < takeOnDate) {
      return { error: "Purchase date cannot be before the take-on date." };
    }

    return {
      request: {
        model_code: modelCode,
        type_code: typeCode,
        vehicle_status_code: statusCode,
        location_code: locationCode,
        fleet_number: fleetNumber,
        registration_number: registrationNumber,
        engine_number_1: engineNumber,
        chassis_number: chassisNumber,
        take_on_date: takeOnDate,
        take_on_odo: takeOnOdo,
        current_odo: takeOnOdo,
        tare,
        gvm,
        year_manufactured: year,
        colour,
        purchase_date: purchaseDate,
        purchase_amount: purchaseAmount,
        ifms_vehicle_register_number: getText(formData, "ifmsVehicleRegisterNumber").toUpperCase() || null,
        natis_model_number: getText(formData, "natisModelNumber").toUpperCase() || null,
        recalculate_tariff: formData.get("recalculateTariff") === "on",
      },
    };
  } catch (error) {
    if (error instanceof VehicleFormValidationError) {
      return { error: error.message };
    }

    throw error;
  }
}

export async function createVehicleAction(
  _previousState: CreateVehicleActionState = initialStatus,
  formData: FormData,
): Promise<CreateVehicleActionState> {
  const parsed = validateAndBuildRequest(formData);
  if ("error" in parsed) {
    return { status: "error", message: parsed.error };
  }

  try {
    await createVehicleAgainstApi(parsed.request);
  } catch (error) {
    if (error instanceof VehicleCreateApiError) {
      return {
        status: "error",
        message:
          error.reason === "unauthorized"
            ? "Your session has expired. Sign in again before creating a vehicle."
            : error.reason === "unavailable"
              ? "The vehicle service is temporarily unavailable. Please try again."
              : "The vehicle service returned an unexpected response. Please try again.",
      };
    }

    console.error("FIS vehicle creation failed", error instanceof Error ? error.message : "unknown error");
    return { status: "error", message: "Vehicle creation failed. Please try again." };
  }

  redirect("/vehicles");
}

export async function searchVehicleAction(
  _previousState: SearchVehicleActionState,
  formData: FormData,
): Promise<SearchVehicleActionState> {
  if (formData.get("intent") === "reset") {
    return { status: "idle", results: [] };
  }

  const searchTerm = getText(formData, "quickSearch");
  if (!searchTerm) {
    return { status: "error", message: "Enter a VIN, engine, GG, or invoice number to search.", results: [] };
  }

  try {
    const results = await searchVehiclesAgainstApi(searchTerm);
    return results.length > 0
      ? { status: "success", results }
      : { status: "success", message: "No matching vehicles found.", results: [] };
  } catch (error) {
    if (error instanceof VehicleCreateApiError) {
      return {
        status: "error",
        message:
          error.reason === "unauthorized"
            ? "Your session has expired. Sign in again before searching."
            : error.reason === "unavailable"
              ? "The vehicle search service is temporarily unavailable. Please try again."
              : "The vehicle search service returned an unexpected response. Please try again.",
        results: [],
      };
    }

    console.error("FIS vehicle search failed", error instanceof Error ? error.message : "unknown error");
    return { status: "error", message: "Vehicle search failed. Please try again.", results: [] };
  }
}
