"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { hasRole } from "@/app/drivers/access";
import { ModelApiError, updateModelLicenceFee } from "@/lib/api-models";
import { getSession } from "@/lib/session";

class ModelFeeValidationError extends Error {}

function positiveCode(formData: FormData, key: string, label: string) {
  const value = formData.get(key);
  const parsed = typeof value === "string" ? Number(value.trim()) : NaN;
  if (!Number.isSafeInteger(parsed) || parsed <= 0 || parsed > 32767) throw new ModelFeeValidationError(`${label} must be a positive code.`);
  return parsed;
}

function message(error: unknown) {
  if (error instanceof ModelFeeValidationError) return error.message;
  if (error instanceof ModelApiError) {
    if (error.reason === "unauthorized") return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable") return "The model licence fee service is temporarily unavailable. Please try again.";
    return error.message;
  }
  return "The model licence fee could not be updated. Please try again.";
}

export async function updateModelLicenceFeeAction(formData: FormData) {
  const path = "/licenses/model-fees";
  const session = await getSession();
  if (session.status !== "authenticated") redirect(`${path}?error=${encodeURIComponent("Your session has expired. Sign in again before continuing.")}`);
  if (!hasRole(session.roles, "Licence")) redirect(`${path}?error=${encodeURIComponent("You do not have permission to maintain Licence Fees.")}`);

  let modelCode = 0;
  try {
    modelCode = positiveCode(formData, "modelCode", "Model code");
    const licenceFeeCode = positiveCode(formData, "licenceFeeCode", "Licence fee code");
    if (!await updateModelLicenceFee(modelCode, licenceFeeCode)) throw new ModelApiError("invalid-response", "The model update response was invalid.");
  } catch (error) {
    redirect(`${path}?modelCode=${encodeURIComponent(String(modelCode))}&error=${encodeURIComponent(message(error))}`);
  }

  revalidatePath(path);
  revalidatePath("/licenses");
  revalidatePath("/validation-data/models");
  redirect(`${path}?modelCode=${encodeURIComponent(String(modelCode))}&saved=1`);
}
