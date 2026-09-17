"use server";

import { revalidatePath } from "next/cache";

import {
  addVehicleAuthorizationComment,
  approveVehicleAuthorization,
  rejectVehicleAuthorization,
  VehicleAuthorizationApiError,
} from "@/lib/api/vehicles/api-vehicle-authorization";
import {
  hasVehicleInceptionAuthorizerRole,
} from "@/app/(fleet-operations)/vehicles/access";
import { getSession } from "@/lib/auth/session";

export type VehicleAuthorizationActionState = {
  status: "idle" | "success" | "error";
  message?: string;
};

const initialStatus: VehicleAuthorizationActionState = { status: "idle" };

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

async function authorizeAction() {
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

  if (!hasVehicleInceptionAuthorizerRole(session.roles)) {
    return {
      ok: false as const,
      message: "You do not have permission to authorize captured vehicles.",
    };
  }

  return { ok: true as const };
}

function apiErrorMessage(error: VehicleAuthorizationApiError) {
  if (error.reason === "unauthorized") {
    return "Your session has expired or you are no longer allowed to authorize vehicles. Sign in again.";
  }

  if (error.reason === "unavailable") {
    return "The vehicle authorization service is temporarily unavailable. Please try again.";
  }

  if (error.reason === "forbidden") {
    return "You do not have permission to perform this vehicle authorization action.";
  }

  return error.message || "The vehicle authorization service returned an unexpected response.";
}

export async function vehicleAuthorizationAction(
  _previousState: VehicleAuthorizationActionState = initialStatus,
  formData: FormData,
): Promise<VehicleAuthorizationActionState> {
  const intent = getText(formData, "intent");
  const id = Number(getText(formData, "id"));

  if (!Number.isInteger(id) || id <= 0) {
    return { status: "error", message: "The selected vehicle authorization is invalid." };
  }

  if (!["approve", "reject", "comment"].includes(intent)) {
    return { status: "error", message: "Select a valid vehicle authorization action." };
  }

  const access = await authorizeAction();
  if (!access.ok) {
    return { status: "error", message: access.message };
  }

  const comment = getText(formData, "comment");
  if (!comment) {
    return {
      status: "error",
      message:
        intent === "comment"
          ? "Comment cannot be empty."
          : "Please supply an authorizer comment before continuing.",
    };
  }

  try {
    let result;
    if (intent === "approve") {
      result = await approveVehicleAuthorization(id, comment);
    } else if (intent === "reject") {
      result = await rejectVehicleAuthorization(id, comment);
    } else {
      result = await addVehicleAuthorizationComment(id, comment);
    }

    revalidatePath("/vehicles/authorize");
    return { status: "success", message: result.message };
  } catch (error) {
    if (error instanceof VehicleAuthorizationApiError) {
      return { status: "error", message: apiErrorMessage(error) };
    }

    console.error(
      "FIS vehicle authorization action failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return { status: "error", message: "Vehicle authorization failed. Please try again." };
  }
}
