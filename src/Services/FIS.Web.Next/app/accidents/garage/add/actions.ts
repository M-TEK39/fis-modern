"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  AccidentApiError,
  createAccidentAgainstApi,
  type CreateAccidentRequest,
} from "@/lib/api-accidents";
import { getSession } from "@/lib/session";

const ACCIDENTS_ROLE = "Accidents";

export type GarageAddActionState = {
  status: "idle" | "error";
  message?: string;
};

const initialStatus: GarageAddActionState = { status: "idle" };

class AccidentFormValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getRequiredText(formData: FormData, key: string, label: string, maxLength: number) {
  const value = getText(formData, key);
  if (!value) {
    throw new AccidentFormValidationError(`${label} is required.`);
  }

  if (value.length > maxLength) {
    throw new AccidentFormValidationError(`${label} must be ${maxLength} characters or fewer.`);
  }

  return value;
}

function getOptionalText(formData: FormData, key: string, label: string, maxLength: number) {
  const value = getText(formData, key);
  if (value.length > maxLength) {
    throw new AccidentFormValidationError(`${label} must be ${maxLength} characters or fewer.`);
  }

  return value || null;
}

function getPositiveInteger(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0) {
    throw new AccidentFormValidationError(`${label} is required.`);
  }

  return parsed;
}

function getDate(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (!match) {
    throw new AccidentFormValidationError(`${label} is required.`);
  }

  const year = Number(match[1]);
  const month = Number(match[2]);
  const day = Number(match[3]);
  const parsed = new Date(Date.UTC(year, month - 1, day));
  if (
    parsed.getUTCFullYear() !== year ||
    parsed.getUTCMonth() !== month - 1 ||
    parsed.getUTCDate() !== day
  ) {
    throw new AccidentFormValidationError(`${label} is invalid.`);
  }

  return value;
}

function getOptionalTime(formData: FormData, key: string) {
  const value = getText(formData, key);
  if (!value) {
    return null;
  }

  if (!/^(?:[01]\d|2[0-3]):[0-5]\d$/.test(value)) {
    throw new AccidentFormValidationError("Accident time must use the HH:MM format.");
  }

  return value;
}

function getAmount(formData: FormData, key: string, label: string) {
  const value = getText(formData, key) || "0";
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 0) {
    throw new AccidentFormValidationError(`${label} must be a non-negative amount.`);
  }

  return parsed;
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some((candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0);
}

function validateAndBuildRequest(formData: FormData): CreateAccidentRequest {
  const vmfCode = getPositiveInteger(formData, "vmfCode", "Vehicle");
  const accidentDate = getDate(formData, "occurenceDate", "Accident date");
  const reportedDate = getDate(formData, "reportedDate", "Date reported");
  const accidentTime = getOptionalTime(formData, "occurenceTime");
  const driverEmployNumber = getOptionalText(formData, "driverEmployNumber", "Driver ID number", 13);

  if (driverEmployNumber && !/^[0-9/]+$/.test(driverEmployNumber)) {
    throw new AccidentFormValidationError("Driver ID number may contain only numbers and '/'.");
  }

  const description = getRequiredText(formData, "description", "Accident description", 60);

  return {
    vmf_code: vmfCode,
    description,
    driver_name: getOptionalText(formData, "driverName", "GG driver name", 25),
    driver_employ_number: driverEmployNumber,
    hq_reference: getOptionalText(formData, "hqReference", "HQ reference", 60),
    gg_reference: getOptionalText(formData, "ggReference", "GG reference", 60),
    sa_reference: null,
    occurence_date: `${accidentDate}T00:00:00.000Z`,
    occurence_time: accidentTime ? `${accidentDate}T${accidentTime}:00.000Z` : null,
    reported_date: `${reportedDate}T00:00:00.000Z`,
    claim_amount: getAmount(formData, "claimAmount", "Claim amount"),
    excess_amount: getAmount(formData, "excessAmount", "Excess amount"),
  };
}

export async function createGarageAccidentAction(
  _previousState: GarageAddActionState = initialStatus,
  formData: FormData,
): Promise<GarageAddActionState> {
  let request: CreateAccidentRequest;
  try {
    request = validateAndBuildRequest(formData);
  } catch (error) {
    if (error instanceof AccidentFormValidationError) {
      return { status: "error", message: error.message };
    }

    throw error;
  }

  const session = await getSession();
  if (session.status === "unavailable") {
    return { status: "error", message: "The sign-in service is temporarily unavailable. Please try again." };
  }

  if (session.status !== "authenticated") {
    return { status: "error", message: "Your session has expired. Sign in again before adding an accident." };
  }

  if (!hasRole(session.roles, ACCIDENTS_ROLE)) {
    return { status: "error", message: "You do not have permission to add garage accidents." };
  }

  try {
    await createAccidentAgainstApi(request);
  } catch (error) {
    if (error instanceof AccidentApiError) {
      return {
        status: "error",
        message:
          error.reason === "unauthorized"
            ? "Your session has expired or you are no longer allowed to add accidents. Sign in again."
            : error.reason === "unavailable"
              ? "The accident service is temporarily unavailable. Please try again."
              : "The accident service rejected the submitted data. Check the fields and try again.",
      };
    }

    console.error("FIS garage accident creation failed", error instanceof Error ? error.message : "unknown error");
    return { status: "error", message: "Accident creation failed. Please try again." };
  }

  revalidatePath("/accidents/garage");
  redirect("/accidents/garage?created=1");
}
