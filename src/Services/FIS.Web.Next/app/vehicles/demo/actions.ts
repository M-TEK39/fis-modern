"use server";

import { revalidatePath } from "next/cache";

import { hasDemoVehicleRole } from "@/app/vehicles/demo/access";
import {
  createDemoVehicle,
  deleteDemoVehicle,
  DemoVehicleApiError,
  getDemoVehicle,
  getDemoVehicleReport,
  searchDemoVehicles,
  updateDemoVehicle,
  type DemoVehicleRecord,
  type DemoVehicleSearchMode,
  type DemoVehicleWriteInput,
} from "@/lib/api-demo-vehicles";
import { getSession } from "@/lib/session";

export type DemoVehicleActionState = {
  status: "idle" | "success" | "error";
  message?: string;
  vehicle?: DemoVehicleRecord;
  matches?: DemoVehicleRecord[];
};

async function authorizeDemoVehicles() {
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
    return { ok: false as const, message: "You do not have permission to maintain demo vehicles." };
  }
  return { ok: true as const };
}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function validateLength(value: string, label: string, maxLength: number) {
  if (value.length > maxLength)
    throw new Error(`${label} must be ${maxLength} characters or fewer.`);
}

function parseOptionalInteger(value: string, label: string, min: number, max: number) {
  if (!value || (value === "0" && label === "Site")) return "";
  if (!/^-?\d+$/.test(value)) throw new Error(`${label} must be a whole number.`);
  const parsed = Number(value);
  if (!Number.isSafeInteger(parsed) || parsed < min || parsed > max) {
    throw new Error(`${label} is outside the legacy database range.`);
  }
  return value;
}

function readWriteInput(formData: FormData): DemoVehicleWriteInput {
  const ggNumber = getText(formData, "ggNumber");
  const registrationNumber = getText(formData, "registrationNumber");
  const modelDescription = getText(formData, "modelDescription");
  const bankCode = getText(formData, "bankCode");
  const colour = getText(formData, "colour");
  const engineNumber = getText(formData, "engineNumber");
  const chassisNumber = getText(formData, "chassisNumber");

  if (registrationNumber.length < 7 || registrationNumber.length > 8) {
    throw new Error("A valid registration number between 7 and 8 characters is required.");
  }
  if (!modelDescription) throw new Error("A make and model description is required.");
  validateLength(ggNumber, "GG number", 7);
  validateLength(modelDescription, "Make and model", 100);
  validateLength(bankCode, "Bank code", 12);
  validateLength(colour, "Colour", 25);
  validateLength(engineNumber, "Engine number", 50);
  validateLength(chassisNumber, "Chassis number", 50);

  return {
    ggNumber,
    registrationNumber,
    modelDescription,
    siteCode: parseOptionalInteger(getText(formData, "siteCode"), "Site", -32768, 32767),
    yearManufactured: parseOptionalInteger(
      getText(formData, "yearManufactured"),
      "Year Manufactured",
      -2147483648,
      2147483647,
    ),
    bankCode,
    tank: parseOptionalInteger(getText(formData, "tank"), "Tank capacity", -32768, 32767),
    colour,
    engineNumber,
    chassisNumber,
  };
}

function apiErrorMessage(error: unknown, fallback: string) {
  if (!(error instanceof DemoVehicleApiError)) return fallback;
  if (error.reason === "unauthorized")
    return "Your session has expired. Sign in again before continuing.";
  if (error.reason === "not-found") return "The selected demo vehicle could not be found.";
  if (error.reason === "unavailable")
    return "The demo vehicle service is temporarily unavailable. Please try again.";
  return error.message || fallback;
}

export async function createDemoVehicleAction(
  _previousState: DemoVehicleActionState,
  formData: FormData,
): Promise<DemoVehicleActionState> {
  const access = await authorizeDemoVehicles();
  if (!access.ok) return { status: "error", message: access.message };

  try {
    const vehicle = await createDemoVehicle(readWriteInput(formData));
    revalidatePath("/vehicles/demo/report");
    return { status: "success", message: "Demo vehicle added successfully.", vehicle };
  } catch (error) {
    console.error(
      "FIS demo vehicle create failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return {
      status: "error",
      message:
        error instanceof Error && !(error instanceof DemoVehicleApiError)
          ? error.message
          : apiErrorMessage(error, "The demo vehicle could not be added."),
    };
  }
}

export async function updateDemoVehicleAction(
  _previousState: DemoVehicleActionState,
  formData: FormData,
): Promise<DemoVehicleActionState> {
  const access = await authorizeDemoVehicles();
  if (!access.ok) return { status: "error", message: access.message };

  const demoVehicleCode = Number(getText(formData, "demoVehicleCode"));
  if (!Number.isInteger(demoVehicleCode) || demoVehicleCode < 1) {
    return { status: "error", message: "The selected demo vehicle is invalid." };
  }

  try {
    const vehicle = await updateDemoVehicle(demoVehicleCode, readWriteInput(formData));
    revalidatePath("/vehicles/demo/report");
    return { status: "success", message: "Demo vehicle updated successfully.", vehicle };
  } catch (error) {
    console.error(
      "FIS demo vehicle update failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return {
      status: "error",
      message:
        error instanceof Error && !(error instanceof DemoVehicleApiError)
          ? error.message
          : apiErrorMessage(error, "The demo vehicle could not be updated."),
    };
  }
}

export async function searchDemoVehiclesAction(
  formData: FormData,
): Promise<DemoVehicleActionState> {
  const access = await authorizeDemoVehicles();
  if (!access.ok) return { status: "error", message: access.message };

  const mode = getText(formData, "mode").toUpperCase();
  const search = getText(formData, "search");
  if (mode !== "GG" && mode !== "GP")
    return { status: "error", message: "Choose GG or GP before searching." };
  if (!search) return { status: "error", message: "Enter a GG or GP number before searching." };

  try {
    const matches = await searchDemoVehicles(search, mode as DemoVehicleSearchMode);
    return {
      status: "success",
      matches,
      message: matches.length === 0 ? "No demo vehicle matched that number." : undefined,
    };
  } catch (error) {
    console.error(
      "FIS demo vehicle search failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return {
      status: "error",
      message: apiErrorMessage(error, "The demo vehicle search could not be completed."),
    };
  }
}

export async function loadDemoVehicleAction(formData: FormData): Promise<DemoVehicleActionState> {
  const access = await authorizeDemoVehicles();
  if (!access.ok) return { status: "error", message: access.message };

  const demoVehicleCode = Number(getText(formData, "demoVehicleCode"));
  if (!Number.isInteger(demoVehicleCode) || demoVehicleCode < 1) {
    return { status: "error", message: "The selected demo vehicle is invalid." };
  }

  try {
    const vehicle = await getDemoVehicle(demoVehicleCode);
    return vehicle
      ? { status: "success", vehicle }
      : { status: "error", message: "The selected demo vehicle could not be found." };
  } catch (error) {
    console.error(
      "FIS demo vehicle load failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return {
      status: "error",
      message: apiErrorMessage(error, "The demo vehicle could not be loaded."),
    };
  }
}

export async function deleteDemoVehicleAction(formData: FormData): Promise<DemoVehicleActionState> {
  const access = await authorizeDemoVehicles();
  if (!access.ok) return { status: "error", message: access.message };

  const demoVehicleCode = Number(getText(formData, "demoVehicleCode"));
  if (!Number.isInteger(demoVehicleCode) || demoVehicleCode < 1) {
    return { status: "error", message: "The selected demo vehicle is invalid." };
  }

  try {
    await deleteDemoVehicle(demoVehicleCode);
    revalidatePath("/vehicles/demo/report");
    return { status: "success", message: "Demo vehicle deleted successfully." };
  } catch (error) {
    console.error(
      "FIS demo vehicle delete failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return {
      status: "error",
      message: apiErrorMessage(error, "The demo vehicle could not be deleted."),
    };
  }
}

export async function loadDemoVehicleReportAction(): Promise<DemoVehicleActionState> {
  const access = await authorizeDemoVehicles();
  if (!access.ok) return { status: "error", message: access.message };

  try {
    return { status: "success", matches: await getDemoVehicleReport() };
  } catch (error) {
    console.error(
      "FIS demo vehicle report failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return {
      status: "error",
      message: apiErrorMessage(error, "The demo vehicle report could not be loaded."),
    };
  }
}
