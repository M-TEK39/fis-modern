"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import {
  createMake,
  deleteMake,
  getMakeDeleteCheck,
  MakeApiError,
  updateMake,
} from "@/lib/api-makes";
import { getSession } from "@/lib/session";

export type MakeActionState = {
  status: "idle" | "error";
  message?: string;
};

const initialState: MakeActionState = { status: "idle" };

class MakeValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getRequiredDescription(formData: FormData) {
  const value = getText(formData, "makeDescription");
  if (!value) throw new MakeValidationError("Make name is required.");
  if (value.length > 60) throw new MakeValidationError("Make name must be 60 characters or fewer.");
  return value;
}

function getMakeCode(formData: FormData) {
  const value = getText(formData, "makeCode");
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0 || parsed > 32767) {
    throw new MakeValidationError("Make code is invalid.");
  }
  return parsed;
}

async function authorizeMakeMaintenance() {
  const session = await getSession();
  if (session.status === "unavailable") {
    return { ok: false as const, message: "The sign-in service is temporarily unavailable. Please try again." };
  }
  if (session.status !== "authenticated") {
    return { ok: false as const, message: "Your session has expired. Sign in again before continuing." };
  }
  if (!hasVehicleManagementPermission(session.accessLevel)) {
    return { ok: false as const, message: "You do not have permission to maintain vehicle makes." };
  }
  return { ok: true as const };
}

function apiErrorMessage(error: unknown, operation: string) {
  if (error instanceof MakeApiError) {
    if (error.reason === "unauthorized") return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable") return `The vehicle make ${operation} service is temporarily unavailable. Please try again.`;
    return error.message;
  }
  return `The vehicle make could not be ${operation}. Please try again.`;
}

export async function createMakeAction(
  _previousState: MakeActionState = initialState,
  formData: FormData,
): Promise<MakeActionState> {
  const access = await authorizeMakeMaintenance();
  if (!access.ok) return { status: "error", message: access.message };

  try {
    await createMake(getRequiredDescription(formData));
  } catch (error) {
    return { status: "error", message: error instanceof MakeValidationError ? error.message : apiErrorMessage(error, "created") };
  }

  revalidatePath("/validation-data/makes");
  redirect("/validation-data/makes?saved=created");
}

export async function updateMakeAction(
  _previousState: MakeActionState = initialState,
  formData: FormData,
): Promise<MakeActionState> {
  const access = await authorizeMakeMaintenance();
  if (!access.ok) return { status: "error", message: access.message };

  try {
    await updateMake(getMakeCode(formData), getRequiredDescription(formData));
  } catch (error) {
    return { status: "error", message: error instanceof MakeValidationError ? error.message : apiErrorMessage(error, "updated") };
  }

  revalidatePath("/validation-data/makes");
  redirect("/validation-data/makes?saved=updated");
}

export async function deleteMakeAction(formData: FormData) {
  const access = await authorizeMakeMaintenance();
  let makeCode: number;
  try {
    makeCode = getMakeCode(formData);
  } catch (error) {
    redirect(`/validation-data/makes?error=${encodeURIComponent(error instanceof Error ? error.message : "Make code is invalid.")}`);
  }
  if (!access.ok) redirect(`/Validation/MNT_Make_Del_Check.aspx?code=${encodeURIComponent(String(makeCode!))}&error=${encodeURIComponent(access.message)}`);

  let dependencies;
  try {
    dependencies = await getMakeDeleteCheck(makeCode!);
  } catch (error) {
    redirect(`/Validation/MNT_Make_Del_Check.aspx?code=${makeCode!}&error=${encodeURIComponent(apiErrorMessage(error, "deleted"))}`);
  }
  if (!dependencies!.canDelete) {
    redirect(`/Validation/MNT_Make_Del_Check.aspx?code=${makeCode}&error=${encodeURIComponent("This make cannot be deleted while models are linked to it.")}`);
  }

  try {
    await deleteMake(makeCode!);
  } catch (error) {
    redirect(`/Validation/MNT_Make_Del_Check.aspx?code=${makeCode!}&error=${encodeURIComponent(apiErrorMessage(error, "deleted"))}`);
  }

  revalidatePath("/validation-data/makes");
  redirect("/validation-data/makes?saved=deleted");
}
