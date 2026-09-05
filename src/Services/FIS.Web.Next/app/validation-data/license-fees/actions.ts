"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import {
  createLicenseFee,
  deleteLicenseFee,
  getLicenseFeeDeleteCheck,
  LicenseFeeApiError,
  updateLicenseFee,
  type LicenseFeeWriteInput,
} from "@/lib/api-license-fees";
import { getSession } from "@/lib/session";

export type LicenseFeeActionState = { status: "idle" | "error"; message?: string };
const initialState: LicenseFeeActionState = { status: "idle" };
class LicenseFeeValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getFee(formData: FormData): number | null {
  const value = getText(formData, "fee");
  if (!value) return null;
  const parsed = Number(value);
  if (!/^\d+(\.\d{1,2})?$/.test(value) || !Number.isFinite(parsed) || parsed < 0 || Math.round(parsed * 100) !== parsed * 100) {
    throw new LicenseFeeValidationError("Yearly licence fee must be a non-negative number with no more than 2 decimal places.");
  }
  return parsed;
}

function getInput(formData: FormData): LicenseFeeWriteInput {
  const description = getText(formData, "description");
  if (!description || description.length > 50) {
    throw new LicenseFeeValidationError("Licence fee description is required and must be 50 characters or fewer.");
  }
  return { description, fee: getFee(formData) };
}

function getCode(formData: FormData) {
  const value = getText(formData, "licenceFeeCode");
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0 || parsed > 32767) {
    throw new LicenseFeeValidationError("Licence fee code is invalid.");
  }
  return parsed;
}

async function authorizeLicenseFeeMaintenance() {
  const session = await getSession();
  if (session.status === "unavailable") return { ok: false as const, message: "The sign-in service is temporarily unavailable. Please try again." };
  if (session.status !== "authenticated") return { ok: false as const, message: "Your session has expired. Sign in again before continuing." };
  if (!hasVehicleManagementPermission(session.accessLevel)) return { ok: false as const, message: "You do not have permission to maintain licence fees." };
  return { ok: true as const };
}

function apiErrorMessage(error: unknown, operation: string) {
  if (error instanceof LicenseFeeApiError) {
    if (error.reason === "unauthorized") return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable") return `The licence fee ${operation} service is temporarily unavailable. Please try again.`;
    return error.message;
  }
  return `The licence fee could not be ${operation}. Please try again.`;
}

function revalidateLicenseFeeRoutes() {
  revalidatePath("/validation-data");
  revalidatePath("/validation-data/license-fees");
  revalidatePath("/Validation/MNT_Licence_Fees.aspx");
}

export async function createLicenseFeeAction(_previousState: LicenseFeeActionState = initialState, formData: FormData): Promise<LicenseFeeActionState> {
  const access = await authorizeLicenseFeeMaintenance();
  if (!access.ok) return { status: "error", message: access.message };
  try {
    const created = await createLicenseFee(getInput(formData));
    if (!created) throw new LicenseFeeApiError("invalid-response", "The FIS API did not return the created licence fee.");
  } catch (error) {
    return { status: "error", message: error instanceof LicenseFeeValidationError ? error.message : apiErrorMessage(error, "created") };
  }
  revalidateLicenseFeeRoutes();
  redirect("/validation-data/license-fees?saved=created");
}

export async function updateLicenseFeeAction(_previousState: LicenseFeeActionState = initialState, formData: FormData): Promise<LicenseFeeActionState> {
  const access = await authorizeLicenseFeeMaintenance();
  if (!access.ok) return { status: "error", message: access.message };
  let licenceFeeCode = 0;
  try {
    licenceFeeCode = getCode(formData);
    const updated = await updateLicenseFee(licenceFeeCode, getInput(formData));
    if (!updated) throw new LicenseFeeApiError("invalid-response", "The FIS API did not return the updated licence fee.");
  } catch (error) {
    return { status: "error", message: error instanceof LicenseFeeValidationError ? error.message : apiErrorMessage(error, "updated") };
  }
  revalidateLicenseFeeRoutes();
  redirect(`/validation-data/license-fees?saved=updated&licenceFeeCode=${encodeURIComponent(String(licenceFeeCode))}`);
}

export async function deleteLicenseFeeAction(formData: FormData) {
  const access = await authorizeLicenseFeeMaintenance();
  let licenceFeeCode = 0;
  try {
    licenceFeeCode = getCode(formData);
  } catch (error) {
    redirect(`/validation-data/license-fees?error=${encodeURIComponent(error instanceof Error ? error.message : "Licence fee code is invalid.")}`);
  }
  if (!access.ok) redirect(`/Validation/MNT_Licence_Fee_Del_Check.aspx?code=${licenceFeeCode}&error=${encodeURIComponent(access.message)}`);

  let dependencies;
  try {
    dependencies = await getLicenseFeeDeleteCheck(licenceFeeCode);
  } catch (error) {
    redirect(`/Validation/MNT_Licence_Fee_Del_Check.aspx?code=${licenceFeeCode}&error=${encodeURIComponent(apiErrorMessage(error, "checked"))}`);
  }
  if (!dependencies.canDelete || dependencies.modelCount > 0) {
    redirect(`/Validation/MNT_Licence_Fee_Del_Check.aspx?code=${licenceFeeCode}&error=${encodeURIComponent("This licence fee cannot be deleted while models are linked to it.")}`);
  }
  try {
    await deleteLicenseFee(licenceFeeCode);
  } catch (error) {
    redirect(`/Validation/MNT_Licence_Fee_Del_Check.aspx?code=${licenceFeeCode}&error=${encodeURIComponent(apiErrorMessage(error, "deleted"))}`);
  }
  revalidateLicenseFeeRoutes();
  redirect("/validation-data/license-fees?saved=deleted");
}
