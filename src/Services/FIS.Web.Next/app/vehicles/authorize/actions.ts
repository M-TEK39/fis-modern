"use server";

import { revalidatePath } from "next/cache";

import {
  addVehicleAuthorizationComment,
  approveVehicleAuthorization,
  rejectVehicleAuthorization,
  VehicleAuthorizationApiError,
} from "@/lib/api-vehicle-authorization";
import { getSession } from "@/lib/session";

const VEHICLE_MANAGEMENT_PERMISSION = 1;
const INCEPTION_ROLES = ["vehicle inception capturer", "vehicle inception authorizer"];

export type VehicleAuthorizationActionState = {
  status: "idle" | "success" | "error";
  message?: string;
};

const initialStatus: VehicleAuthorizationActionState = { status: "idle" };

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some((candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0);
}

function hasVehicleManagementPermission(accessLevel?: string) {
  if (!accessLevel) {
    return false;
  }

  try {
    return (BigInt(accessLevel) & BigInt(VEHICLE_MANAGEMENT_PERMISSION)) === BigInt(VEHICLE_MANAGEMENT_PERMISSION);
  } catch {
    return false;
  }
}

function hasExplicitInceptionRole(roles: readonly string[]) {
  return roles.some((role) => INCEPTION_ROLES.some((candidate) => hasRole([role], candidate)));
}

async function authorizeAction() {
  const session = await getSession();

  if (session.status === "unavailable") {
    return { ok: false as const, message: "The sign-in service is temporarily unavailable. Please try again." };
  }

  if (session.status !== "authenticated") {
    return { ok: false as const, message: "Your session has expired. Sign in again before continuing." };
  }

  if (!hasVehicleManagementPermission(session.accessLevel)) {
    return { ok: false as const, message: "You do not have permission to authorize captured vehicles." };
  }

  const canAuthorize = hasRole(session.roles, "vehicle inception authorizer") || !hasExplicitInceptionRole(session.roles);
  if (!canAuthorize) {
    return { ok: false as const, message: "You do not have permission to authorize captured vehicles." };
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
      message: intent === "comment" ? "Comment cannot be empty." : "Please supply a comment before continuing.",
    };
  }

  try {
    let result;
    if (intent === "approve") {
      result = await approveVehicleAuthorization(id, comment);
    } else if (intent === "reject") {
      const rejectionReason = getText(formData, "rejectionReason");
      if (!rejectionReason) {
        return { status: "error", message: "Rejection reason is required." };
      }

      result = await rejectVehicleAuthorization(id, rejectionReason, comment);
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
