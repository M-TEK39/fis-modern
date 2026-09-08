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

function getOptionalDate(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  return value ? getDate(formData, key, label) : null;
}

function getOptionalDecimal(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  if (!value) {
    return null;
  }

  const parsed = Number(value);
  if (!Number.isFinite(parsed)) {
    throw new VehicleFormValidationError(`${label} must be a valid amount.`);
  }

  return parsed;
}

function validateAndBuildRequest(
  formData: FormData,
): { request: CreateVehicleRequest } | { error: string } {
  try {
    const fleetNumber = getText(formData, "fleetNumber").toUpperCase() || null;
    const gpNumber = getText(formData, "gpNumber").toUpperCase() || null;
    const registrationNumber = null;
    const engineNumber = getText(formData, "engineNumber").toUpperCase();
    const chassisNumber = getText(formData, "chassisNumber").toUpperCase();
    const colour = getText(formData, "colour");
    const purchaseFrom = getText(formData, "purchaseFrom").toUpperCase();
    const comment = getText(formData, "comment");
    const replacedGgNumber = getText(formData, "replacedGgNumber").toUpperCase() || null;
    const invoiceNumber = getText(formData, "invoiceNumber").toUpperCase() || null;
    const fleetNotes = getText(formData, "fleetNotes") || null;
    const damagesComment = getText(formData, "damagesComment") || null;

    if (!engineNumber || !chassisNumber || !colour || !purchaseFrom || !comment) {
      return { error: "Complete all required vehicle identity, purchase, and comment fields." };
    }

    const tooLong = [
      ["Current GG number", fleetNumber, 20],
      ["Replace GG number", replacedGgNumber, 20],
      ["Colour", colour, 15],
      ["Engine number", engineNumber, 60],
      ["VIN / chassis number", chassisNumber, 60],
      ["Capturer's comment", comment, 90],
      ["Purchased from", purchaseFrom, 60],
      ["Invoice number", invoiceNumber, 60],
      ["GP number", gpNumber, 9],
      ["Fleet notes", fleetNotes, 255],
      ["Damage details", damagesComment, 355],
    ] as const satisfies readonly (readonly [string, string | null, number])[];
    const lengthError = tooLong.find(
      ([, value, maximum]) => value !== null && value.length > maximum,
    );
    if (lengthError) {
      return { error: `${lengthError[0]} cannot exceed ${lengthError[2]} characters.` };
    }

    if (engineNumber === chassisNumber) {
      return { error: "Chassis and engine numbers must be different." };
    }

    if (fleetNumber && !isValidGgNumber(fleetNumber)) {
      return { error: "Current GG number must use the legacy format GVN001G." };
    }

    if (replacedGgNumber && !isValidGgNumber(replacedGgNumber)) {
      return { error: "Replace GG number must use the legacy format GVN001G." };
    }

    const modelCode = getInteger(formData, "modelCode", "Model");
    const typeCode = getInteger(formData, "typeCode", "Vehicle type");
    const statusCode = getInteger(formData, "statusCode", "Vehicle status");
    const locationCode = getInteger(formData, "locationCode", "Location");
    const sourceCode = getInteger(formData, "sourceCode", "Hired from");
    const siteCode = getInteger(formData, "siteCode", "Site allocation");
    const takeOnOdo = getInteger(formData, "takeOnOdo", "Take-on odometer");
    const year = getInteger(formData, "yearManufactured", "Year manufactured");
    const purchaseAmount = getDecimal(formData, "purchaseAmount", "Purchase amount");
    const takeOnDate = getDate(formData, "takeOnDate", "Take-on date");
    const purchaseDate = getDate(formData, "purchaseDate", "Purchase date");
    const maintenanceTypeCode = getInteger(
      formData,
      "maintenanceTypeCode",
      "Maintenance type",
      false,
    );
    const maintenanceStartDate = getOptionalDate(
      formData,
      "maintenanceStartDate",
      "Maintenance start date",
    );
    const maintenancePeriodMonths = getInteger(
      formData,
      "maintenancePeriodMonths",
      "Maintenance period",
      false,
    );
    const maintenanceKilos = getInteger(formData, "maintenanceKilos", "Maintenance kilos", false);
    const maintenanceValue = getOptionalDecimal(formData, "maintenanceValue", "Maintenance value");
    const extraCodes = formData.getAll("extraCodes").flatMap((value) => {
      const parsed = Number(value);
      return typeof value === "string" && Number.isInteger(parsed) && parsed > 0 ? [parsed] : [];
    });
    const damageStatus = getText(formData, "damageStatus") || "N";

    if (
      modelCode <= 0 ||
      typeCode <= 0 ||
      statusCode < 0 ||
      locationCode <= 0 ||
      sourceCode <= 0 ||
      siteCode <= 0 ||
      takeOnOdo < 0 ||
      year < 1900 ||
      purchaseAmount < 5000 ||
      purchaseAmount > 9999999 ||
      (maintenanceTypeCode !== null && maintenanceTypeCode <= 0) ||
      (maintenancePeriodMonths !== null && maintenancePeriodMonths < 0) ||
      (maintenanceKilos !== null && maintenanceKilos < 0) ||
      (maintenanceValue !== null && maintenanceValue < 0) ||
      !["Y", "N"].includes(damageStatus)
    ) {
      return { error: "Enter valid positive reference values and non-negative measurements." };
    }

    if (typeCode === 1 || typeCode === 2 || typeCode === 3) {
      if (sourceCode !== 1) {
        return {
          error:
            "VIP Services, Permanent Hire, and Pool Vehicle entries must use g-Fleet Normal as Hired From.",
        };
      }
    } else if (typeCode === 4) {
      if (sourceCode !== 2 && sourceCode !== 3) {
        return { error: "Lease entries must use SMMT-Lease or g-Fleet Lease as Hired From." };
      }
    } else if (typeCode === 5) {
      if (sourceCode !== 4 && sourceCode !== 5) {
        return { error: "Rental entries must use a rental vehicle source." };
      }
    }

    if (maintenanceTypeCode !== null && (maintenanceValue === null || maintenanceValue <= 0)) {
      return {
        error: "Maintenance value must be greater than zero when a maintenance option is selected.",
      };
    }

    if (maintenanceTypeCode !== null && !maintenanceStartDate) {
      return { error: "Maintenance start date is required when a maintenance option is selected." };
    }

    if (
      maintenanceTypeCode !== null &&
      maintenanceStartDate &&
      Number(maintenanceStartDate.slice(0, 4)) < year
    ) {
      return {
        error: "Maintenance start date cannot be earlier than the vehicle's manufactured year.",
      };
    }

    if (damageStatus === "Y" && !damagesComment) {
      return { error: "Damage details are required when the vehicle has damage." };
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
        fleet_number: fleetNumber,
        registration_number: registrationNumber,
        chassis_number: chassisNumber,
        engine_number: engineNumber,
        model_code: modelCode,
        colour,
        year_manufactured: year,
        location_code: locationCode,
        vehicle_status_code: statusCode,
        type_code: typeCode,
        vs_code: sourceCode,
        take_on_date: takeOnDate,
        take_on_odo: takeOnOdo,
        purchase_date: purchaseDate,
        purchase_amount: purchaseAmount,
        purchase_from: purchaseFrom,
        replaced_gg_number: replacedGgNumber,
        site_code: siteCode,
        invoice_number: invoiceNumber,
        gp_number: gpNumber,
        comment,
        damage_status: damageStatus as "Y" | "N",
        damages_comment: damagesComment,
        fleet_notes: fleetNotes,
        extra_codes: extraCodes,
        maintenance_type_code: maintenanceTypeCode,
        maintenance_start_date: maintenanceStartDate,
        maintenance_period_months: maintenancePeriodMonths,
        maintenance_kilos: maintenanceKilos,
        maintenance_value: maintenanceValue,
      },
    };
  } catch (error) {
    if (error instanceof VehicleFormValidationError) {
      return { error: error.message };
    }

    throw error;
  }
}

function isValidGgNumber(value: string) {
  return /^G[A-Z]{2}[0-9]{3}G$/i.test(value.trim());
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

    console.error(
      "FIS vehicle creation failed",
      error instanceof Error ? error.message : "unknown error",
    );
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
    return {
      status: "error",
      message: "Enter a VIN, engine, GG, or invoice number to search.",
      results: [],
    };
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

    console.error(
      "FIS vehicle search failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return { status: "error", message: "Vehicle search failed. Please try again.", results: [] };
  }
}
