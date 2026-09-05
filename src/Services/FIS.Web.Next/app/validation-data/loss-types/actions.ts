"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import {
  createLossType,
  deleteLossType,
  getLossTypeDeleteCheck,
  LossTypeApiError,
  updateLossType,
  type LossTypeWriteInput,
} from "@/lib/api-loss-types";
import { getSession } from "@/lib/session";

export type LossTypeActionState = {
  status: "idle" | "error";
  message?: string;
};

const initialState: LossTypeActionState = { status: "idle" };
class LossTypeValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getInput(formData: FormData): LossTypeWriteInput {
  const description = getText(formData, "description");
  if (!description || description.length > 30) {
    throw new LossTypeValidationError("Loss description is required and must be 30 characters or fewer.");
  }
  return { description };
}

function getCode(formData: FormData) {
  const value = getText(formData, "lossTypeCode");
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0 || parsed > 32767) {
    throw new LossTypeValidationError("Loss type code is invalid.");
  }
  return parsed;
}

async function authorizeLossTypeMaintenance() {
  const session = await getSession();
  if (session.status === "unavailable") {
    return { ok: false as const, message: "The sign-in service is temporarily unavailable. Please try again." };
  }
  if (session.status !== "authenticated") {
    return { ok: false as const, message: "Your session has expired. Sign in again before continuing." };
  }
  if (!hasVehicleManagementPermission(session.accessLevel)) {
    return { ok: false as const, message: "You do not have permission to maintain loss descriptions." };
  }
  return { ok: true as const };
}

function apiErrorMessage(error: unknown, operation: string) {
  if (error instanceof LossTypeApiError) {
    if (error.reason === "unauthorized") return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable") return `The loss description ${operation} service is temporarily unavailable. Please try again.`;
    return error.message;
  }
  return `The loss description could not be ${operation}. Please try again.`;
}

function revalidateLossTypeRoutes() {
  revalidatePath("/validation-data");
  revalidatePath("/validation-data/loss-types");
  revalidatePath("/Validation/MNT_Loss_Type.aspx");
}

export async function createLossTypeAction(
  _previousState: LossTypeActionState = initialState,
  formData: FormData,
): Promise<LossTypeActionState> {
  const access = await authorizeLossTypeMaintenance();
  if (!access.ok) return { status: "error", message: access.message };

  try {
    const created = await createLossType(getInput(formData));
    if (!created) throw new LossTypeApiError("invalid-response", "The FIS API did not return the created loss description.");
  } catch (error) {
    return { status: "error", message: error instanceof LossTypeValidationError ? error.message : apiErrorMessage(error, "created") };
  }

  revalidateLossTypeRoutes();
  redirect("/validation-data/loss-types?saved=created");
}

export async function updateLossTypeAction(
  _previousState: LossTypeActionState = initialState,
  formData: FormData,
): Promise<LossTypeActionState> {
  const access = await authorizeLossTypeMaintenance();
  if (!access.ok) return { status: "error", message: access.message };

  let lossTypeCode = 0;
  try {
    lossTypeCode = getCode(formData);
    const updated = await updateLossType(lossTypeCode, getInput(formData));
    if (!updated) throw new LossTypeApiError("invalid-response", "The FIS API did not return the updated loss description.");
  } catch (error) {
    return { status: "error", message: error instanceof LossTypeValidationError ? error.message : apiErrorMessage(error, "updated") };
  }

  revalidateLossTypeRoutes();
  redirect(`/validation-data/loss-types?saved=updated&lossTypeCode=${encodeURIComponent(String(lossTypeCode))}`);
}

export async function deleteLossTypeAction(formData: FormData) {
  const access = await authorizeLossTypeMaintenance();
  let lossTypeCode = 0;
  try {
    lossTypeCode = getCode(formData);
  } catch (error) {
    redirect(`/validation-data/loss-types?error=${encodeURIComponent(error instanceof Error ? error.message : "Loss type code is invalid.")}`);
  }

  if (!access.ok) {
    redirect(`/validation-data/loss-types/delete?code=${lossTypeCode}&error=${encodeURIComponent(access.message)}`);
  }

  let dependencies;
  try {
    dependencies = await getLossTypeDeleteCheck(lossTypeCode);
  } catch (error) {
    redirect(`/validation-data/loss-types/delete?code=${lossTypeCode}&error=${encodeURIComponent(apiErrorMessage(error, "dependency check"))}`);
  }

  if (!dependencies.checkAvailable) {
    redirect(`/validation-data/loss-types/delete?code=${lossTypeCode}&error=${encodeURIComponent("Loss records could not be verified, so the loss description was not deleted.")}`);
  }
  if (!dependencies.canDelete || dependencies.lossCount > 0) {
    redirect(`/validation-data/loss-types/delete?code=${lossTypeCode}&error=${encodeURIComponent("Delete or change the linked loss records before deleting this loss description.")}`);
  }

  try {
    await deleteLossType(lossTypeCode);
  } catch (error) {
    redirect(`/validation-data/loss-types/delete?code=${lossTypeCode}&error=${encodeURIComponent(apiErrorMessage(error, "deleted"))}`);
  }

  revalidateLossTypeRoutes();
  redirect("/validation-data/loss-types?saved=deleted");
}
