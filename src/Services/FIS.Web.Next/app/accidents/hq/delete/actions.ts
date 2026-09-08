"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  AccidentApiError,
  deleteAccidentAgainstApi,
  getAccidentForEdit,
} from "@/lib/api-accidents";
import { getSession } from "@/lib/session";

const ACCIDENTS_ROLE = "Accidents";

export type HqDeleteActionState = {
  status: "idle" | "error";
  message?: string;
};

const initialStatus: HqDeleteActionState = { status: "idle" };

function getAccidentCode(formData: FormData) {
  const value = formData.get("accidentCode");
  const parsed = typeof value === "string" ? Number(value) : NaN;
  return typeof value === "string" && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function apiErrorMessage(error: AccidentApiError) {
  if (error.reason === "unauthorized")
    return "Your session has expired or you are no longer allowed to delete HQ accidents. Sign in again.";
  if (error.reason === "not-found") return "That accident record no longer exists.";
  if (error.reason === "unavailable")
    return "The accident service is temporarily unavailable. Please try again.";
  return "The accident service rejected the delete request. Please try again.";
}

export async function deleteHqAccidentAction(
  _previousState: HqDeleteActionState = initialStatus,
  formData: FormData,
): Promise<HqDeleteActionState> {
  const session = await getSession();
  if (session.status === "unavailable")
    return {
      status: "error",
      message: "The sign-in service is temporarily unavailable. Please try again.",
    };
  if (session.status !== "authenticated")
    return {
      status: "error",
      message: "Your session has expired. Sign in again before deleting an accident.",
    };
  if (!hasRole(session.roles, ACCIDENTS_ROLE))
    return { status: "error", message: "You do not have permission to delete HQ accidents." };

  const accidentCode = getAccidentCode(formData);
  if (accidentCode === null)
    return { status: "error", message: "A valid accident record is required." };

  try {
    const existing = await getAccidentForEdit(accidentCode);
    if (existing.isDeleted)
      return { status: "error", message: "That accident record is already deleted." };
    await deleteAccidentAgainstApi(accidentCode);
  } catch (error) {
    if (error instanceof AccidentApiError)
      return { status: "error", message: apiErrorMessage(error) };
    console.error(
      "FIS HQ accident deletion failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return { status: "error", message: "Accident deletion failed. Please try again." };
  }

  revalidatePath("/accidents/hq");
  revalidatePath("/accidents/hq/delete");
  redirect("/accidents/hq/delete?deleted=1");
}
