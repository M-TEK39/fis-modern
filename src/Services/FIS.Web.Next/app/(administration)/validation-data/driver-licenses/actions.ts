"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { hasLegacyRole } from "@/app/(administration)/drivers/access";
import {
  createDriverLicence,
  deleteDriverLicence,
  DriverLicenceApiError,
  getDriverLicenceDeleteCheck,
  updateDriverLicence,
  type DriverLicenceWriteInput,
} from "@/lib/api/reference-data/api-driver-licences";
import { getSession } from "@/lib/auth/session";

export type DriverLicenceActionState = {
  status: "idle" | "error";
  message?: string;
};

const initialState: DriverLicenceActionState = { status: "idle" };
class DriverLicenceValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getInput(formData: FormData): DriverLicenceWriteInput {
  const description = getText(formData, "description");
  if (!description || description.length > 30) {
    throw new DriverLicenceValidationError(
      "Driver licence description is required and must be 30 characters or fewer.",
    );
  }
  return { description };
}

function getCode(formData: FormData) {
  const value = getText(formData, "licenceCode");
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0 || parsed > 32767) {
    throw new DriverLicenceValidationError("Driver licence code is invalid.");
  }
  return parsed;
}

async function authorizeDriverLicenceMaintenance() {
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
  if (!hasLegacyRole(session.roles, "Validation")) {
    return {
      ok: false as const,
      message: "You do not have permission to maintain driver licences.",
    };
  }
  return { ok: true as const };
}

function apiErrorMessage(error: unknown, operation: string) {
  if (error instanceof DriverLicenceApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable")
      return `The driver licence ${operation} service is temporarily unavailable. Please try again.`;
    return error.message;
  }
  return `The driver licence could not be ${operation}. Please try again.`;
}

function revalidateDriverLicenceRoutes() {
  revalidatePath("/validation-data");
  revalidatePath("/validation-data/driver-licenses");
  revalidatePath("/Validation/MNT_DriversLicence.aspx");
}

export async function createDriverLicenceAction(
  _previousState: DriverLicenceActionState = initialState,
  formData: FormData,
): Promise<DriverLicenceActionState> {
  const access = await authorizeDriverLicenceMaintenance();
  if (!access.ok) return { status: "error", message: access.message };

  try {
    const created = await createDriverLicence(getInput(formData));
    if (!created)
      throw new DriverLicenceApiError(
        "invalid-response",
        "The FIS API did not return the created driver licence.",
      );
  } catch (error) {
    return {
      status: "error",
      message:
        error instanceof DriverLicenceValidationError
          ? error.message
          : apiErrorMessage(error, "created"),
    };
  }

  revalidateDriverLicenceRoutes();
  redirect("/validation-data/driver-licenses?saved=created");
}

export async function updateDriverLicenceAction(
  _previousState: DriverLicenceActionState = initialState,
  formData: FormData,
): Promise<DriverLicenceActionState> {
  const access = await authorizeDriverLicenceMaintenance();
  if (!access.ok) return { status: "error", message: access.message };

  let licenceCode = 0;
  try {
    licenceCode = getCode(formData);
    const updated = await updateDriverLicence(licenceCode, getInput(formData));
    if (!updated)
      throw new DriverLicenceApiError(
        "invalid-response",
        "The FIS API did not return the updated driver licence.",
      );
  } catch (error) {
    return {
      status: "error",
      message:
        error instanceof DriverLicenceValidationError
          ? error.message
          : apiErrorMessage(error, "updated"),
    };
  }

  revalidateDriverLicenceRoutes();
  redirect(
    `/validation-data/driver-licenses?saved=updated&licenceCode=${encodeURIComponent(String(licenceCode))}`,
  );
}

export async function deleteDriverLicenceAction(formData: FormData) {
  const access = await authorizeDriverLicenceMaintenance();
  let licenceCode = 0;
  try {
    licenceCode = getCode(formData);
  } catch (error) {
    redirect(
      `/validation-data/driver-licenses?error=${encodeURIComponent(error instanceof Error ? error.message : "Driver licence code is invalid.")}`,
    );
  }

  if (!access.ok) {
    redirect(
      `/Validation/MNT_DriversLicence_Del_Check.aspx?code=${licenceCode}&error=${encodeURIComponent(access.message)}`,
    );
  }

  let dependencies;
  try {
    dependencies = await getDriverLicenceDeleteCheck(licenceCode);
  } catch (error) {
    redirect(
      `/Validation/MNT_DriversLicence_Del_Check.aspx?code=${licenceCode}&error=${encodeURIComponent(apiErrorMessage(error, "checked"))}`,
    );
  }

  if (!dependencies.canDelete || dependencies.modelCount > 0) {
    redirect(
      `/Validation/MNT_DriversLicence_Del_Check.aspx?code=${licenceCode}&error=${encodeURIComponent("This driver licence cannot be deleted while models are linked to it.")}`,
    );
  }

  try {
    await deleteDriverLicence(licenceCode);
  } catch (error) {
    redirect(
      `/Validation/MNT_DriversLicence_Del_Check.aspx?code=${licenceCode}&error=${encodeURIComponent(apiErrorMessage(error, "deleted"))}`,
    );
  }

  revalidateDriverLicenceRoutes();
  redirect("/validation-data/driver-licenses?saved=deleted");
}
