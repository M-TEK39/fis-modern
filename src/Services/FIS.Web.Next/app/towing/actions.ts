"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  createTowTruckAgainstApi,
  createTowingAgainstApi,
  deleteTowTruckAgainstApi,
  deleteTowingAgainstApi,
  TowingApiError,
  updateTowTruckAgainstApi,
  updateTowingAgainstApi,
  type TowTruckRequest,
  type TowingRequest,
} from "@/lib/api-towing";
import { getSession } from "@/lib/session";

const TOWING_ROLE = "Towing";

class TowingValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getReturnPath(formData: FormData, fallback: string) {
  const value = getText(formData, "returnPath");
  return value.startsWith("/towing") && !value.startsWith("//") ? value : fallback;
}

function getRequiredInteger(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0) {
    throw new TowingValidationError(`${label} must be a positive whole number.`);
  }
  return parsed;
}

function getOptionalInteger(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  if (!value) return null;
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed < 0) {
    throw new TowingValidationError(`${label} must be a non-negative whole number.`);
  }
  return parsed;
}

function getOptionalDecimal(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  if (!value) return null;
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 0) {
    throw new TowingValidationError(`${label} must be a non-negative number.`);
  }
  return parsed;
}

function getOptionalText(formData: FormData, key: string, label: string, maxLength: number) {
  const value = getText(formData, key);
  if (value.length > maxLength) {
    throw new TowingValidationError(`${label} must be ${maxLength} characters or fewer.`);
  }
  return value || null;
}

function getDate(formData: FormData, key: string, label: string, required = false) {
  const value = getText(formData, key);
  if (!value && !required) return null;
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    throw new TowingValidationError(`${label} is invalid.`);
  }
  const [year, month, day] = value.split("-").map(Number);
  const parsed = new Date(Date.UTC(year, month - 1, day));
  if (
    parsed.getUTCFullYear() !== year ||
    parsed.getUTCMonth() !== month - 1 ||
    parsed.getUTCDate() !== day
  ) {
    throw new TowingValidationError(`${label} is invalid.`);
  }
  return `${value}T00:00:00.000Z`;
}

function getTime(formData: FormData) {
  const value = getText(formData, "requestTime");
  if (!value) return `${new Date().toISOString().slice(0, 11)}00:00:00.000Z`;
  if (!/^([01]\d|2[0-3]):[0-5]\d$/.test(value)) {
    throw new TowingValidationError("Request time must use the hh:mm format.");
  }
  const date = getText(formData, "requestDate");
  return `${date}T${value}:00.000Z`;
}

async function authorizeTowing() {
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
  if (
    !session.roles.some(
      (role) => role.localeCompare(TOWING_ROLE, undefined, { sensitivity: "accent" }) === 0,
    )
  ) {
    return {
      ok: false as const,
      message: "You do not have permission to maintain road side assistance records.",
    };
  }
  return { ok: true as const };
}

function apiErrorMessage(error: unknown, operation: string) {
  if (error instanceof TowingApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable")
      return `The towing ${operation} service is temporarily unavailable. Please try again.`;
    if (error.reason === "not-found")
      return "The towing record was not found. Return to the Towing menu and try again.";
  }
  return `The towing record could not be ${operation}. Please try again.`;
}

function redirectError(returnPath: string, message: string) {
  redirect(
    `${returnPath}${returnPath.includes("?") ? "&" : "?"}error=${encodeURIComponent(message)}`,
  );
}

function getTowingCode(formData: FormData) {
  const value = getText(formData, "towingCode");
  if (!value) return null;
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    throw new TowingValidationError("The towing record is invalid.");
  }
  return parsed;
}

function buildTowingRequest(formData: FormData): TowingRequest {
  const requestDate = getDate(formData, "requestDate", "Request date", true);
  return {
    vmf_code: getRequiredInteger(formData, "vmfCode", "Vehicle"),
    Call_refer: getOptionalDecimal(formData, "callReference", "Call reference"),
    Tow_request_date: requestDate,
    Tow_request_time: getTime(formData),
    Tow_location_start: getOptionalText(formData, "locationStart", "Location of vehicle", 50),
    Vehicle_problem: getOptionalText(formData, "vehicleProblem", "Vehicle problem", 30),
    Keys: getOptionalText(formData, "keys", "Keys", 200),
    Site_code: getOptionalInteger(formData, "siteCode", "Site"),
    Contact_person_name: getOptionalText(formData, "contactPersonName", "Contact name", 30),
    Contact_person_tel: getOptionalText(formData, "contactPersonTel", "Contact telephone", 30),
    Contact_person_cell: getOptionalText(formData, "contactPersonCell", "Contact cell", 10),
    Person_at_vehicle_name: getOptionalText(
      formData,
      "personAtVehicleName",
      "Person with vehicle",
      30,
    ),
    Person_at_vehicle_cell: getOptionalText(formData, "personAtVehicleCell", "Person cell", 10),
    Remaks: getOptionalText(formData, "remarks", "Remarks", 50),
    Tow_Truck_code: getOptionalInteger(formData, "towTruckCode", "Tow truck"),
  };
}

export async function saveTowingAction(formData: FormData) {
  const returnPath = getReturnPath(formData, "/towing/request");
  const access = await authorizeTowing();
  if (!access.ok) redirectError(returnPath, access.message);

  let towingCode: number | null = null;
  let request: TowingRequest;
  try {
    towingCode = getTowingCode(formData);
    request = buildTowingRequest(formData);
  } catch (error) {
    redirectError(
      returnPath,
      error instanceof TowingValidationError ? error.message : "The towing form is invalid.",
    );
  }

  try {
    if (towingCode === null) {
      await createTowingAgainstApi(request!);
    } else {
      await updateTowingAgainstApi(towingCode, request!);
    }
  } catch (error) {
    redirectError(returnPath, apiErrorMessage(error, towingCode === null ? "captured" : "updated"));
  }

  revalidatePath("/towing");
  revalidatePath("/towing/request");
  revalidatePath("/towing/tow-truck-data");
  redirect(`${returnPath}?${towingCode === null ? "saved=1" : "updated=1"}`);
}

export async function deleteTowingAction(formData: FormData) {
  const returnPath = getReturnPath(formData, "/towing/request");
  const access = await authorizeTowing();
  if (!access.ok) redirectError(returnPath, access.message);

  let towingCode = 0;
  try {
    const value = getTowingCode(formData);
    if (value === null) throw new TowingValidationError("The towing record is invalid.");
    towingCode = value;
  } catch (error) {
    redirectError(
      returnPath,
      error instanceof TowingValidationError ? error.message : "The towing record is invalid.",
    );
  }

  try {
    await deleteTowingAgainstApi(towingCode!);
  } catch (error) {
    redirectError(returnPath, apiErrorMessage(error, "deleted"));
  }

  revalidatePath("/towing");
  revalidatePath("/towing/request");
  redirect(`${returnPath}?deleted=1`);
}

function buildTowTruckRequest(formData: FormData): TowTruckRequest {
  const name = getOptionalText(formData, "towName", "Tow truck name", 30);
  if (!name) throw new TowingValidationError("Tow truck name is required.");
  return {
    TowName: name,
    TowArea: getOptionalText(formData, "towArea", "Operating area", 255),
    TowTel: getOptionalText(formData, "towTel", "Telephone", 50),
    TowFax: getOptionalText(formData, "towFax", "Fax", 50),
  };
}

function getTowCode(formData: FormData) {
  const value = getText(formData, "towCode");
  if (!value) return null;
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0)
    throw new TowingValidationError("The tow truck record is invalid.");
  return parsed;
}

export async function saveTowTruckAction(formData: FormData) {
  const returnPath = getReturnPath(formData, "/towing/tow-truck-data");
  const access = await authorizeTowing();
  if (!access.ok) redirectError(returnPath, access.message);

  let towCode: number | null = null;
  let request: TowTruckRequest;
  try {
    towCode = getTowCode(formData);
    request = buildTowTruckRequest(formData);
  } catch (error) {
    redirectError(
      returnPath,
      error instanceof TowingValidationError ? error.message : "The tow truck form is invalid.",
    );
  }

  try {
    if (towCode === null) {
      await createTowTruckAgainstApi(request!);
    } else {
      await updateTowTruckAgainstApi(towCode, request!);
    }
  } catch (error) {
    redirectError(returnPath, apiErrorMessage(error, towCode === null ? "captured" : "updated"));
  }

  revalidatePath("/towing");
  revalidatePath("/towing/tow-truck-data");
  redirect(`${returnPath}?${towCode === null ? "saved=1" : "updated=1"}`);
}

export async function deleteTowTruckAction(formData: FormData) {
  const returnPath = getReturnPath(formData, "/towing/tow-truck-data");
  const access = await authorizeTowing();
  if (!access.ok) redirectError(returnPath, access.message);

  let towCode = 0;
  try {
    const value = getTowCode(formData);
    if (value === null) throw new TowingValidationError("The tow truck record is invalid.");
    towCode = value;
  } catch (error) {
    redirectError(
      returnPath,
      error instanceof TowingValidationError ? error.message : "The tow truck record is invalid.",
    );
  }

  try {
    await deleteTowTruckAgainstApi(towCode!);
  } catch (error) {
    redirectError(returnPath, apiErrorMessage(error, "deleted"));
  }

  revalidatePath("/towing");
  revalidatePath("/towing/tow-truck-data");
  redirect(`${returnPath}?deleted=1`);
}
