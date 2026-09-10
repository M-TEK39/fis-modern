"use server";

import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";

import { createGgBlock, GgBlockApiError } from "@/lib/api/vehicles/api-gg-blocks";
import { getSession } from "@/lib/auth/session";

const VEHICLE_MANAGEMENT_PERMISSION = 1;
const GgNumberPattern = /^[A-Z]{3}[0-9]{3}G$/;
const ALLOWED_RETURN_PATHS = [
  "/vehicles/gg-block-numbers",
  "/Master-File/Add_GGBlockNumbers.aspx",
] as const;

export type GgBlockActionState = {
  status: "idle" | "error";
  message?: string;
  startGgNumber?: string;
  endGgNumber?: string;
};

const initialState: GgBlockActionState = { status: "idle" };

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim().toUpperCase() : "";
}

function getRawText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function hasVehicleManagementPermission(accessLevel?: string) {
  if (!accessLevel) {
    return false;
  }

  try {
    return (
      (BigInt(accessLevel) & BigInt(VEHICLE_MANAGEMENT_PERMISSION)) ===
      BigInt(VEHICLE_MANAGEMENT_PERMISSION)
    );
  } catch {
    return false;
  }
}

function getReturnPath(value: string) {
  return (ALLOWED_RETURN_PATHS as readonly string[]).includes(value)
    ? value
    : ALLOWED_RETURN_PATHS[0];
}

function errorState(
  message: string,
  startGgNumber: string,
  endGgNumber: string,
): GgBlockActionState {
  return { status: "error", message, startGgNumber, endGgNumber };
}

export async function createGgBlockAction(
  _previousState: GgBlockActionState = initialState,
  formData: FormData,
): Promise<GgBlockActionState> {
  const startGgNumber = getText(formData, "startGgNumber");
  const endGgNumber = getText(formData, "endGgNumber");
  const submittedReturnPath = getRawText(formData, "returnPath");
  const returnPath = getReturnPath(
    submittedReturnPath.toLowerCase() === "/master-file/add_ggblocknumbers.aspx"
      ? "/Master-File/Add_GGBlockNumbers.aspx"
      : submittedReturnPath,
  );

  const session = await getSession();
  if (session.status === "unavailable") {
    return errorState(
      "The sign-in service is temporarily unavailable. Please try again.",
      startGgNumber,
      endGgNumber,
    );
  }

  if (session.status !== "authenticated") {
    return errorState(
      "Your session has expired. Sign in again before continuing.",
      startGgNumber,
      endGgNumber,
    );
  }

  if (!hasVehicleManagementPermission(session.accessLevel)) {
    return errorState(
      "You do not have permission to maintain GG block numbers.",
      startGgNumber,
      endGgNumber,
    );
  }

  if (!GgNumberPattern.test(startGgNumber) || !GgNumberPattern.test(endGgNumber)) {
    return errorState("GG numbers must use the format ABC123G.", startGgNumber, endGgNumber);
  }

  if (
    startGgNumber.slice(0, 3) !== endGgNumber.slice(0, 3) ||
    startGgNumber[6] !== endGgNumber[6]
  ) {
    return errorState(
      "The start and end GG numbers must use the same prefix and suffix.",
      startGgNumber,
      endGgNumber,
    );
  }

  const startNumber = Number(startGgNumber.slice(3, 6));
  const endNumber = Number(endGgNumber.slice(3, 6));
  if (endNumber <= startNumber) {
    return errorState(
      "The end GG number must be greater than the start GG number.",
      startGgNumber,
      endGgNumber,
    );
  }

  try {
    await createGgBlock(startGgNumber, endGgNumber);
  } catch (error) {
    if (error instanceof GgBlockApiError) {
      if (error.reason === "conflict") {
        return errorState(
          "The requested GG block range falls within an existing GG block range.",
          startGgNumber,
          endGgNumber,
        );
      }

      if (error.reason === "unauthorized") {
        return errorState(
          "Your session has expired. Sign in again before continuing.",
          startGgNumber,
          endGgNumber,
        );
      }

      if (error.reason === "unavailable") {
        return errorState(
          "The GG block service is temporarily unavailable. Please try again.",
          startGgNumber,
          endGgNumber,
        );
      }

      return errorState(error.message, startGgNumber, endGgNumber);
    }

    console.error(
      "FIS GG block creation failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return errorState(
      "The GG block could not be saved. Please try again.",
      startGgNumber,
      endGgNumber,
    );
  }

  revalidatePath(returnPath);
  redirect(`${returnPath}?saved=1`);
}
