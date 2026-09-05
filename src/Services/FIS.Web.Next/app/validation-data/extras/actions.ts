"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import {
  createExtraCode,
  deleteExtraCode,
  ExtraCodeApiError,
  getExtraCodeDeleteCheck,
  type ExtraCodeWriteInput,
} from "@/lib/api-extra-codes";
import { getSession } from "@/lib/session";

export type ExtraCodeActionState = { status: "idle" | "error"; message?: string };
const initialState: ExtraCodeActionState = { status: "idle" };
class ExtraCodeValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getInput(formData: FormData): ExtraCodeWriteInput {
  const description = getText(formData, "description");
  if (!description || description.length > 50) {
    throw new ExtraCodeValidationError("Extra description is required and must be 50 characters or fewer.");
  }
  return { description };
}

function getCode(formData: FormData) {
  const value = getText(formData, "extraCode");
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0 || parsed > 32767) {
    throw new ExtraCodeValidationError("Extra code is invalid.");
  }
  return parsed;
}

async function authorizeExtraCodeMaintenance() {
  const session = await getSession();
  if (session.status === "unavailable") return { ok: false as const, message: "The sign-in service is temporarily unavailable. Please try again." };
  if (session.status !== "authenticated") return { ok: false as const, message: "Your session has expired. Sign in again before continuing." };
  if (!hasVehicleManagementPermission(session.accessLevel)) return { ok: false as const, message: "You do not have permission to maintain extras." };
  return { ok: true as const };
}

function apiErrorMessage(error: unknown, operation: string) {
  if (error instanceof ExtraCodeApiError) {
    if (error.reason === "unauthorized") return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable") return `The extra code ${operation} service is temporarily unavailable. Please try again.`;
    if (error.fleetNumbers.length > 0) {
      return `This extra is linked to vehicles ${error.fleetNumbers.join(", ")}. Remove it from those vehicles before deleting it.`;
    }
    return error.message;
  }
  return `The extra code could not be ${operation}. Please try again.`;
}

function revalidateExtraCodeRoutes() {
  revalidatePath("/validation-data");
  revalidatePath("/validation-data/extras");
  revalidatePath("/Validation/MNT_Extras.aspx");
}

export async function createExtraCodeAction(
  _previousState: ExtraCodeActionState = initialState,
  formData: FormData,
): Promise<ExtraCodeActionState> {
  const access = await authorizeExtraCodeMaintenance();
  if (!access.ok) return { status: "error", message: access.message };
  try {
    const created = await createExtraCode(getInput(formData));
    if (!created) throw new ExtraCodeApiError("invalid-response", "The FIS API did not return the created extra code.");
  } catch (error) {
    return { status: "error", message: error instanceof ExtraCodeValidationError ? error.message : apiErrorMessage(error, "creation") };
  }
  revalidateExtraCodeRoutes();
  redirect("/validation-data/extras?saved=created");
}

export async function deleteExtraCodeAction(formData: FormData) {
  const access = await authorizeExtraCodeMaintenance();
  let extraCode = 0;
  try {
    extraCode = getCode(formData);
  } catch (error) {
    redirect(`/validation-data/extras?error=${encodeURIComponent(error instanceof Error ? error.message : "Extra code is invalid.")}`);
  }

  if (!access.ok) {
    redirect(`/validation-data/extras/delete?code=${extraCode}&error=${encodeURIComponent(access.message)}`);
  }

  let dependencies;
  try {
    dependencies = await getExtraCodeDeleteCheck(extraCode);
  } catch (error) {
    redirect(`/validation-data/extras/delete?code=${extraCode}&error=${encodeURIComponent(apiErrorMessage(error, "dependency check"))}`);
  }

  if (!dependencies.checkAvailable) {
    redirect(`/validation-data/extras/delete?code=${extraCode}&error=${encodeURIComponent("Extra dependencies could not be verified, so the extra was not deleted.")}`);
  }
  if (!dependencies.canDelete || dependencies.vehicleCount > 0) {
    const message = dependencies.fleetNumbers.length > 0
      ? `This extra is linked to vehicles ${dependencies.fleetNumbers.join(", ")}. Remove it from those vehicles before deleting it.`
      : "Remove this extra from the linked vehicle data before deleting it.";
    redirect(`/validation-data/extras/delete?code=${extraCode}&error=${encodeURIComponent(message)}`);
  }

  try {
    await deleteExtraCode(extraCode);
  } catch (error) {
    redirect(`/validation-data/extras/delete?code=${extraCode}&error=${encodeURIComponent(apiErrorMessage(error, "deletion"))}`);
  }
  revalidateExtraCodeRoutes();
  redirect("/validation-data/extras?saved=deleted");
}
