"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  AccidentApiError,
  getAccidentForEdit,
  updateAccidentAgainstApi,
  type AccidentEditRecord,
  type AccidentUpdateRequest,
} from "@/lib/api-accidents";
import { getSession } from "@/lib/session";

const ACCIDENTS_ROLE = "Accidents";

export type GarageEditActionState = {
  status: "idle" | "error";
  message?: string;
};

const initialStatus: GarageEditActionState = { status: "idle" };

class AccidentFormValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getAccidentCode(formData: FormData) {
  const value = getText(formData, "accidentCode");
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0) {
    throw new AccidentFormValidationError("A valid accident record is required.");
  }

  return parsed;
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

function getRequiredDate(formData: FormData, key: string, label: string) {
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

  return `${value}T00:00:00.000Z`;
}

function getOptionalTime(formData: FormData, key: string, accidentDate: string) {
  const value = getText(formData, key);
  if (!value) {
    return null;
  }

  if (!/^(?:[01]\d|2[0-3]):[0-5]\d$/.test(value)) {
    throw new AccidentFormValidationError("Accident time must use the HH:MM format.");
  }

  return `${accidentDate.slice(0, 10)}T${value}:00.000Z`;
}

function getAmount(formData: FormData, key: string, label: string, existing: number | null) {
  const value = getText(formData, key);
  if (!value && existing === null) {
    return null;
  }

  const parsed = Number(value || "0");
  if (!Number.isFinite(parsed) || parsed < 0) {
    throw new AccidentFormValidationError(`${label} must be a non-negative amount.`);
  }

  return parsed;
}

function hasRole(roles: readonly string[], role: string) {
  return roles.some((candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0);
}

function buildUpdateRequest(formData: FormData, existing: AccidentEditRecord): AccidentUpdateRequest {
  const accidentDate = getRequiredDate(formData, "occurenceDate", "Accident date");
  const reportedDate = getRequiredDate(formData, "reportedDate", "Date reported");
  const driverEmployNumber = getOptionalText(formData, "driverEmployNumber", "Driver ID number", 13);
  if (driverEmployNumber && !/^[0-9/]+$/.test(driverEmployNumber)) {
    throw new AccidentFormValidationError("Driver ID number may contain only numbers and '/'.");
  }

  return {
    accident_code: existing.accidentCode,
    vmf_code: existing.vmfCode,
    posting_month_code: existing.postingMonthCode,
    description: getRequiredText(formData, "description", "Accident description", 60),
    driver_name: getOptionalText(formData, "driverName", "GG driver name", 25),
    driver_employ_number: driverEmployNumber,
    hq_reference: getOptionalText(formData, "hqReference", "HQ reference", 60),
    gg_reference: existing.ggReference,
    sa_reference: existing.saReference,
    occurence_date: accidentDate,
    occurence_time: getOptionalTime(formData, "occurenceTime", accidentDate),
    reported_date: reportedDate,
    claim_amount: getAmount(formData, "claimAmount", "Claim amount", existing.claimAmount),
    excess_amount: getAmount(formData, "excessAmount", "Excess amount", existing.excessAmount),
    date_created: existing.dateCreated,
    date_updated: existing.dateUpdated,
    created_by_user_code: existing.createdByUserCode,
    modified_by_user_code: existing.modifiedByUserCode,
    is_deleted: existing.isDeleted,
  };
}

function apiErrorMessage(error: AccidentApiError) {
  if (error.reason === "unauthorized") {
    return "Your session has expired or you are no longer allowed to edit accidents. Sign in again.";
  }

  if (error.reason === "not-found") {
    return "That accident record no longer exists.";
  }

  if (error.reason === "unavailable") {
    return "The accident service is temporarily unavailable. Please try again.";
  }

  return "The accident service rejected the submitted data. Check the fields and try again.";
}

export async function updateGarageAccidentAction(
  _previousState: GarageEditActionState = initialStatus,
  formData: FormData,
): Promise<GarageEditActionState> {
  const session = await getSession();
  if (session.status === "unavailable") {
    return { status: "error", message: "The sign-in service is temporarily unavailable. Please try again." };
  }

  if (session.status !== "authenticated") {
    return { status: "error", message: "Your session has expired. Sign in again before editing an accident." };
  }

  if (!hasRole(session.roles, ACCIDENTS_ROLE)) {
    return { status: "error", message: "You do not have permission to edit garage accidents." };
  }

  let accidentCode: number;
  try {
    accidentCode = getAccidentCode(formData);
  } catch (error) {
    return {
      status: "error",
      message: error instanceof AccidentFormValidationError ? error.message : "A valid accident record is required.",
    };
  }

  try {
    const existing = await getAccidentForEdit(accidentCode);
    await updateAccidentAgainstApi(buildUpdateRequest(formData, existing));
  } catch (error) {
    if (error instanceof AccidentFormValidationError) {
      return { status: "error", message: error.message };
    }

    if (error instanceof AccidentApiError) {
      return { status: "error", message: apiErrorMessage(error) };
    }

    console.error("FIS garage accident update failed", error instanceof Error ? error.message : "unknown error");
    return { status: "error", message: "Accident update failed. Please try again." };
  }

  revalidatePath("/accidents/garage");
  redirect("/accidents/garage?updated=1");
}
