"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import {
  createFuelType,
  createLicenseType,
  createUnitOfMeasure,
  createVehicleType,
  deleteFuelType,
  deleteLicenseType,
  deleteUnitOfMeasure,
  deleteVehicleType,
  ReferenceDataApiError,
  updateFuelType,
  updateLicenseType,
  updateUnitOfMeasure,
  updateVehicleType,
} from "@/lib/api-reference-data";
import { getSession } from "@/lib/session";

export type ReferenceDataActionState = { status: "idle" | "error"; message?: string };
export type ReferenceDataAction = (previousState: ReferenceDataActionState, formData: FormData) => Promise<ReferenceDataActionState>;
export type ReferenceDataDeleteAction = (formData: FormData) => Promise<void>;

const initialState: ReferenceDataActionState = { status: "idle" };
class ReferenceDataValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getCode(formData: FormData, key: string) {
  const value = getText(formData, key);
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0 || parsed > 32767) throw new ReferenceDataValidationError("The reference-data code is invalid.");
  return parsed;
}

function getDescription(formData: FormData, label: string, maxLength: number) {
  const value = getText(formData, "description");
  if (!value || value.length > maxLength) throw new ReferenceDataValidationError(`${label} is required and must be ${maxLength} characters or fewer.`);
  return value;
}

function getOptionalText(formData: FormData, key: string, maxLength: number) {
  const value = getText(formData, key);
  if (value.length > maxLength) throw new ReferenceDataValidationError(`${key} must be ${maxLength} characters or fewer.`);
  return value || null;
}

function getRate(formData: FormData) {
  const value = getText(formData, "ratePerLitre");
  if (!value) return null;
  const parsed = Number(value);
  if (!/^\d+(\.\d{1,2})?$/.test(value) || !Number.isFinite(parsed) || parsed < 0 || parsed > 999999.99) {
    throw new ReferenceDataValidationError("Rate per litre must be a valid amount with no more than two decimal places.");
  }
  return parsed;
}

async function authorize() {
  const session = await getSession();
  if (session.status === "unavailable") return { ok: false as const, message: "The sign-in service is temporarily unavailable. Please try again." };
  if (session.status !== "authenticated") return { ok: false as const, message: "Your session has expired. Sign in again before continuing." };
  if (!hasVehicleManagementPermission(session.accessLevel)) return { ok: false as const, message: "You do not have permission to maintain reference data." };
  return { ok: true as const };
}

function apiErrorMessage(error: unknown, operation: string) {
  if (error instanceof ReferenceDataApiError) {
    if (error.reason === "unauthorized") return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable") return `The reference-data ${operation} service is temporarily unavailable. Please try again.`;
    return error.message;
  }
  return `The reference-data item could not be ${operation}. Please try again.`;
}

function revalidateReferenceData() {
  revalidatePath("/reference-data");
  revalidatePath("/validation-data");
}

function requireMutationResult<T>(result: T | null, item: string) {
  if (result === null) throw new ReferenceDataApiError("invalid-response", `The FIS API did not return the ${item}.`);
  return result;
}

async function runMutation(
  formData: FormData,
  operation: string,
  mutate: () => Promise<unknown>,
  tab: string,
): Promise<ReferenceDataActionState> {
  const access = await authorize();
  if (!access.ok) return { status: "error", message: access.message };
  try {
    await mutate();
  } catch (error) {
    return { status: "error", message: error instanceof ReferenceDataValidationError ? error.message : apiErrorMessage(error, operation) };
  }
  revalidateReferenceData();
  redirect(`/reference-data?tab=${encodeURIComponent(tab)}&saved=${encodeURIComponent(operation)}`);
}

export async function createVehicleTypeAction(_previousState: ReferenceDataActionState = initialState, formData: FormData) {
  return runMutation(formData, "created", async () => requireMutationResult(await createVehicleType(getDescription(formData, "Vehicle type name", 255)), "created vehicle type"), "types");
}

export async function updateVehicleTypeAction(_previousState: ReferenceDataActionState = initialState, formData: FormData) {
  return runMutation(formData, "updated", async () => requireMutationResult(await updateVehicleType(getCode(formData, "typeCode"), getDescription(formData, "Vehicle type name", 255)), "updated vehicle type"), "types");
}

export async function createFuelTypeAction(_previousState: ReferenceDataActionState = initialState, formData: FormData) {
  return runMutation(formData, "created", async () => requireMutationResult(await createFuelType(getDescription(formData, "Fuel type name", 255), getRate(formData)), "created fuel type"), "fueltypes");
}

export async function updateFuelTypeAction(_previousState: ReferenceDataActionState = initialState, formData: FormData) {
  return runMutation(formData, "updated", async () => requireMutationResult(await updateFuelType(getCode(formData, "fuelTypeCode"), getDescription(formData, "Fuel type name", 255), getRate(formData)), "updated fuel type"), "fueltypes");
}

export async function createUnitOfMeasureAction(_previousState: ReferenceDataActionState = initialState, formData: FormData) {
  return runMutation(formData, "created", async () => requireMutationResult(await createUnitOfMeasure(getDescription(formData, "Unit description", 100), getOptionalText(formData, "abbreviation", 10), getOptionalText(formData, "category", 50)), "created unit of measure"), "units");
}

export async function updateUnitOfMeasureAction(_previousState: ReferenceDataActionState = initialState, formData: FormData) {
  return runMutation(formData, "updated", async () => requireMutationResult(await updateUnitOfMeasure(getCode(formData, "unitCode"), getDescription(formData, "Unit description", 100), getOptionalText(formData, "abbreviation", 10), getOptionalText(formData, "category", 50)), "updated unit of measure"), "units");
}

export async function createLicenseTypeAction(_previousState: ReferenceDataActionState = initialState, formData: FormData) {
  return runMutation(formData, "created", async () => requireMutationResult(await createLicenseType(getDescription(formData, "License type description", 255), getOptionalText(formData, "category", 20)), "created license type"), "licenses");
}

export async function updateLicenseTypeAction(_previousState: ReferenceDataActionState = initialState, formData: FormData) {
  return runMutation(formData, "updated", async () => requireMutationResult(await updateLicenseType(getCode(formData, "licenceCode"), getDescription(formData, "License type description", 255), getOptionalText(formData, "category", 20)), "updated license type"), "licenses");
}

async function runDelete(formData: FormData, deleteItem: () => Promise<void>, tab: string) {
  const access = await authorize();
  const codeKey = tab === "types" ? "typeCode" : tab === "fueltypes" ? "fuelTypeCode" : tab === "units" ? "unitCode" : "licenceCode";
  let code = 0;
  try {
    code = getCode(formData, codeKey);
  } catch (error) {
    redirect(`/reference-data?tab=${encodeURIComponent(tab)}&error=${encodeURIComponent(error instanceof Error ? error.message : "The reference-data code is invalid.")}`);
  }
  if (!access.ok) redirect(`/reference-data?tab=${encodeURIComponent(tab)}&error=${encodeURIComponent(access.message)}`);
  try {
    await deleteItem();
  } catch (error) {
    redirect(`/reference-data?tab=${encodeURIComponent(tab)}&error=${encodeURIComponent(apiErrorMessage(error, "deleted"))}`);
  }
  revalidateReferenceData();
  redirect(`/reference-data?tab=${encodeURIComponent(tab)}&saved=deleted&code=${encodeURIComponent(String(code))}`);
}

export async function deleteVehicleTypeAction(formData: FormData) { return runDelete(formData, () => deleteVehicleType(getCode(formData, "typeCode")), "types"); }
export async function deleteFuelTypeAction(formData: FormData) { return runDelete(formData, () => deleteFuelType(getCode(formData, "fuelTypeCode")), "fueltypes"); }
export async function deleteUnitOfMeasureAction(formData: FormData) { return runDelete(formData, () => deleteUnitOfMeasure(getCode(formData, "unitCode")), "units"); }
export async function deleteLicenseTypeAction(formData: FormData) { return runDelete(formData, () => deleteLicenseType(getCode(formData, "licenceCode")), "licenses"); }
