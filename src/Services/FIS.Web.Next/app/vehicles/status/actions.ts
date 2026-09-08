"use server";

import {
  addVehicleRemarkAgainstApi,
  getVehicleStatusReport,
  resolveVehicleRemarkAgainstApi,
  VehicleStatusApiError,
  type VehicleStatusReport,
  type VehicleStatusReportFilters,
} from "@/lib/api-vehicle-status";
import { getSession } from "@/lib/session";

const VEHICLE_MANAGEMENT_PERMISSION = 1;
const REMARK_CATEGORIES = new Set([
  "General",
  "Missing",
  "UnderInvestigation",
  "AccidentHold",
  "Other",
]);

export type VehicleStatusReportActionResult = {
  status: "success" | "error";
  message?: string;
  report?: VehicleStatusReport;
};

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

async function authorizeReport() {
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
      message: "You do not have permission to view vehicle status reports.",
    };
  }

  return { ok: true as const };
}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getOptionalCode(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  if (!value) {
    return undefined;
  }

  const code = Number(value);
  if (!Number.isInteger(code) || code < 1) {
    throw new Error(`${label} must be a whole number.`);
  }

  return code;
}

function getReportFilters(formData: FormData): VehicleStatusReportFilters {
  return {
    search: getText(formData, "search") || undefined,
    locationCode: getOptionalCode(formData, "locationCode", "Site"),
    typeCode: getOptionalCode(formData, "typeCode", "Type"),
    makeCode: getOptionalCode(formData, "makeCode", "Make"),
    vehicleStatusCode: getOptionalCode(formData, "vehicleStatusCode", "Status"),
  };
}

function apiErrorMessage(error: unknown, fallback: string) {
  if (!(error instanceof VehicleStatusApiError)) {
    return fallback;
  }

  if (error.reason === "unauthorized") {
    return "Your session has expired. Sign in again before continuing.";
  }

  if (error.reason === "unavailable") {
    return "The vehicle status service is temporarily unavailable. Please try again.";
  }

  return error.message || fallback;
}

export async function loadVehicleStatusReportAction(
  formData: FormData,
): Promise<VehicleStatusReportActionResult> {
  const access = await authorizeReport();
  if (!access.ok) {
    return { status: "error", message: access.message };
  }

  try {
    const report = await getVehicleStatusReport(getReportFilters(formData));
    return { status: "success", report };
  } catch (error) {
    console.error(
      "FIS vehicle status report request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return {
      status: "error",
      message: apiErrorMessage(error, "The vehicle status report could not be loaded."),
    };
  }
}

export async function submitVehicleStatusRemarkAction(
  formData: FormData,
): Promise<VehicleStatusReportActionResult> {
  const access = await authorizeReport();
  if (!access.ok) {
    return { status: "error", message: access.message };
  }

  const operation = getText(formData, "operation");
  if (operation !== "add" && operation !== "resolve") {
    return { status: "error", message: "The vehicle remark operation is invalid." };
  }
  const remarkText = getText(formData, "remarkText");
  if (!remarkText) {
    return {
      status: "error",
      message:
        operation === "resolve" ? "Resolution notes are required." : "Remark text is required.",
    };
  }

  if (remarkText.length > 500) {
    return { status: "error", message: "Remark text cannot exceed 500 characters." };
  }

  const vmfCode = Number(getText(formData, "vmfCode"));
  if (!Number.isInteger(vmfCode) || vmfCode < 1) {
    return { status: "error", message: "The selected vehicle is invalid." };
  }

  try {
    if (operation === "resolve") {
      const remarkId = Number(getText(formData, "remarkId"));
      if (!Number.isInteger(remarkId) || remarkId < 1) {
        return { status: "error", message: "The selected vehicle remark is invalid." };
      }

      await resolveVehicleRemarkAgainstApi(vmfCode, remarkId, remarkText);
    } else {
      const category = getText(formData, "remarkCategory") || "General";
      if (!REMARK_CATEGORIES.has(category)) {
        return { status: "error", message: "The selected remark category is invalid." };
      }

      await addVehicleRemarkAgainstApi(vmfCode, category, remarkText);
    }

    const report = await getVehicleStatusReport(getReportFilters(formData));
    return {
      status: "success",
      message:
        operation === "resolve"
          ? "Vehicle remark resolved successfully."
          : "Vehicle remark added successfully.",
      report,
    };
  } catch (error) {
    console.error(
      "FIS vehicle status remark request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return {
      status: "error",
      message: apiErrorMessage(error, "The vehicle remark could not be saved."),
    };
  }
}
