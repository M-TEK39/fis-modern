"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  createVehicleSource,
  updateVehicleSource,
  VehicleSourceApiError,
} from "@/lib/api/vehicles/api-vehicle-sources";
import { hasVehicleMasterRole } from "@/app/(fleet-operations)/vehicles/access";
import { getSession } from "@/lib/auth/session";

const ALLOWED_RETURN_PATHS = [
  "/vehicles/source-maintenance",
  "/Master-File/Vehicle_Source.aspx",
  "/Master-File/Add_Vehicle_Source.aspx",
  "/Master-File/Edit_Vehicle_Source.aspx",
  "/Master-File/Edit_Vehicle_Source_2.aspx",
] as const;

export type VehicleSourceActionState = {
  status: "idle" | "error";
  message?: string;
};

const initialState: VehicleSourceActionState = { status: "idle" };

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getReturnPath(value: string) {
  return (
    ALLOWED_RETURN_PATHS.find((path) => path.toLowerCase() === value.toLowerCase()) ??
    ALLOWED_RETURN_PATHS[0]
  );
}

function errorState(message: string): VehicleSourceActionState {
  return { status: "error", message };
}

export async function saveVehicleSourceAction(
  _previousState: VehicleSourceActionState = initialState,
  formData: FormData,
): Promise<VehicleSourceActionState> {
  const returnPath = getReturnPath(getText(formData, "returnPath"));
  const rawSourceCode = getText(formData, "vsCode");
  const name = getText(formData, "name").toUpperCase();
  const physicalAddress = getText(formData, "physicalAddress");
  const postalAddress = getText(formData, "postalAddress");
  const telephoneNumber = getText(formData, "telephoneNumber");
  const faxNumber = getText(formData, "faxNumber");
  const emailAddress = getText(formData, "emailAddress");
  const contactPerson = getText(formData, "contactPerson");

  const session = await getSession();
  if (session.status === "unavailable") {
    return errorState("The sign-in service is temporarily unavailable. Please try again.");
  }

  if (session.status !== "authenticated") {
    return errorState("Your session has expired. Sign in again before continuing.");
  }

  if (!hasVehicleMasterRole(session.roles)) {
    return errorState("You do not have permission to maintain vehicle sources.");
  }

  const sourceCode = rawSourceCode ? Number(rawSourceCode) : null;
  if (
    sourceCode !== null &&
    (!Number.isInteger(sourceCode) || sourceCode < 0 || sourceCode > 255)
  ) {
    return errorState("The selected vehicle source is invalid.");
  }

  const input = {
    name,
    physicalAddress,
    postalAddress,
    telephoneNumber,
    faxNumber,
    emailAddress,
    contactPerson,
  };

  try {
    if (sourceCode === null) {
      await createVehicleSource(input);
    } else {
      await updateVehicleSource(sourceCode, input);
    }
  } catch (error) {
    if (error instanceof VehicleSourceApiError) {
      if (error.reason === "unauthorized") {
        return errorState("Your session has expired. Sign in again before continuing.");
      }

      if (error.reason === "conflict") {
        return errorState(error.message);
      }

      if (error.reason === "not-found") {
        return errorState(
          "The selected vehicle source no longer exists. Reload the list and try again.",
        );
      }

      if (error.reason === "unavailable") {
        return errorState(
          "The vehicle source service is temporarily unavailable. Please try again.",
        );
      }

      return errorState(error.message);
    }

    console.error(
      "FIS vehicle source save failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return errorState("The vehicle source could not be saved. Please try again.");
  }

  revalidatePath(returnPath);
  redirect(`${returnPath}?saved=1`);
}
