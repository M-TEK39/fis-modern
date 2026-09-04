"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  ClearanceApiError,
  createClearanceAgainstApi,
  createMerchantAgainstApi,
  deleteClearanceAgainstApi,
  updateClearanceAgainstApi,
  updateMerchantAgainstApi,
  type ClearanceRequest,
  type MerchantRequest,
} from "@/lib/api-clearance";
import { getSession } from "@/lib/session";

const CLEARANCE_ROLE = "Clearance";

class ClearanceValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getInteger(formData: FormData, key: string, label: string, required = true) {
  const value = getText(formData, key);
  if (!value && !required) {
    return null;
  }

  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed)) {
    throw new ClearanceValidationError(`${label} must be a whole number.`);
  }

  return parsed;
}

function getDecimal(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  if (!value) {
    return null;
  }

  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 0) {
    throw new ClearanceValidationError(`${label} must be a non-negative amount.`);
  }

  return parsed;
}

function getDate(formData: FormData) {
  const value = getText(formData, "clearanceDate");
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    throw new ClearanceValidationError("Clearance date is required.");
  }

  const parsed = new Date(`${value}T00:00:00Z`);
  if (Number.isNaN(parsed.getTime())) {
    throw new ClearanceValidationError("Clearance date is invalid.");
  }

  return `${value}T00:00:00.000Z`;
}

function getReturnPath(formData: FormData) {
  const value = getText(formData, "returnPath");
  return value.startsWith("/") && !value.startsWith("//") ? value : "/clearance/entry";
}

function redirectWithMessage(path: string, key: string, message: string) {
  const query = new URLSearchParams({ [key]: message });
  redirect(`${path}?${query.toString()}`);
}

async function authorizeClearance() {
  const session = await getSession();
  if (session.status === "unavailable") {
    return { ok: false as const, message: "The sign-in service is temporarily unavailable. Please try again." };
  }

  if (session.status !== "authenticated") {
    return { ok: false as const, message: "Your session has expired. Sign in again before continuing." };
  }

  if (!session.roles.some((role) => role.localeCompare(CLEARANCE_ROLE, undefined, { sensitivity: "accent" }) === 0)) {
    return { ok: false as const, message: "You do not have permission to maintain clearance records." };
  }

  return { ok: true as const };
}

function apiErrorMessage(error: unknown, subject: string) {
  if (error instanceof ClearanceApiError) {
    if (error.reason === "unauthorized") {
      return "Your session has expired. Sign in again before continuing.";
    }

    if (error.reason === "unavailable") {
      return `The ${subject} service is temporarily unavailable. Please try again.`;
    }

    if (error.reason === "not-found") {
      return `The ${subject} record was not found.`;
    }
  }

  return `${subject[0].toUpperCase()}${subject.slice(1)} operation failed. Please try again.`;
}

function buildClearanceRequest(formData: FormData): ClearanceRequest {
  const vmfCode = getInteger(formData, "vmfCode", "Vehicle");
  const clearanceNumber = getInteger(formData, "clearanceNumber", "Clearance number");
  const merchantCode = getInteger(formData, "merchantCode", "Merchant", false);
  const kilos = getInteger(formData, "kilos", "Clearance kilos", false);
  const amount = getDecimal(formData, "amount", "Clearance amount");
  const comment = getText(formData, "comment");

  if (vmfCode === null || vmfCode <= 0) {
    throw new ClearanceValidationError("Vehicle is required.");
  }

  if (clearanceNumber === null || clearanceNumber < 0) {
    throw new ClearanceValidationError("Clearance number must be a non-negative whole number.");
  }

  if (merchantCode !== null && merchantCode <= 0) {
    throw new ClearanceValidationError("Select a valid merchant.");
  }

  if (kilos !== null && kilos < 0) {
    throw new ClearanceValidationError("Clearance kilos must be non-negative.");
  }

  if (comment.length === 0) {
    throw new ClearanceValidationError("Clearance comment is required.");
  }

  if (comment.length > 80) {
    throw new ClearanceValidationError("Clearance comment must be 80 characters or fewer.");
  }

  return {
    vmf_code: vmfCode,
    clearance_number: clearanceNumber,
    Clearance_date: getDate(formData),
    Merchant_code: merchantCode,
    Clearance_amount: amount,
    clearance_comment: comment,
    clearance_kilo: kilos,
  };
}

export async function saveClearanceAction(formData: FormData) {
  const returnPath = getReturnPath(formData);
  const access = await authorizeClearance();
  if (!access.ok) {
    redirectWithMessage(returnPath, "error", access.message);
  }

  let request: ClearanceRequest;
  let isUpdate = false;
  try {
    request = buildClearanceRequest(formData);
    const rawCode = getText(formData, "clearanceCode");
    const clearanceCode = rawCode ? Number(rawCode) : null;
    if (clearanceCode !== null) {
      if (!Number.isInteger(clearanceCode) || clearanceCode <= 0) {
        throw new ClearanceValidationError("Clearance record is invalid.");
      }

      await updateClearanceAgainstApi(clearanceCode, request);
      isUpdate = true;
    } else {
      await createClearanceAgainstApi(request);
    }
  } catch (error) {
    if (error instanceof Error && !(error instanceof ClearanceApiError)) {
      redirectWithMessage(returnPath, "error", error.message);
    }

    redirectWithMessage(returnPath, "error", apiErrorMessage(error, "clearance"));
  }

  revalidatePath("/clearance");
  revalidatePath("/clearance/entry");
  redirect(`/clearance/entry?vmfCode=${request!.vmf_code}&${isUpdate ? "updated" : "saved"}=1`);
}

export async function deleteClearanceAction(formData: FormData) {
  const returnPath = getReturnPath(formData);
  const access = await authorizeClearance();
  if (!access.ok) {
    redirectWithMessage(returnPath, "error", access.message);
  }

  const clearanceCode = Number(getText(formData, "clearanceCode"));
  const vmfCode = Number(getText(formData, "vmfCode"));
  if (!Number.isInteger(clearanceCode) || clearanceCode <= 0 || !Number.isInteger(vmfCode) || vmfCode <= 0) {
    redirectWithMessage(returnPath, "error", "Clearance record is invalid.");
  }

  try {
    await deleteClearanceAgainstApi(clearanceCode);
  } catch (error) {
    redirectWithMessage(returnPath, "error", apiErrorMessage(error, "clearance"));
  }

  revalidatePath("/clearance/entry");
  redirect(`/clearance/entry?vmfCode=${vmfCode}&deleted=1`);
}

function buildMerchantRequest(formData: FormData): MerchantRequest {
  const name = getText(formData, "merchantName");
  if (!name) {
    throw new ClearanceValidationError("Merchant name is required.");
  }

  if (name.length > 30) {
    throw new ClearanceValidationError("Merchant name must be 30 characters or fewer.");
  }

  if (name.includes("'")) {
    throw new ClearanceValidationError("Merchant name cannot contain an apostrophe.");
  }

  return { Merchant_Name: name };
}

export async function saveMerchantAction(formData: FormData) {
  const returnPath = getReturnPath(formData).startsWith("/clearance/merchant")
    ? getReturnPath(formData)
    : "/clearance/merchant";
  const access = await authorizeClearance();
  if (!access.ok) {
    redirectWithMessage(returnPath, "error", access.message);
  }

  let isUpdate = false;
  try {
    const request = buildMerchantRequest(formData);
    const rawCode = getText(formData, "merchantCode");
    const merchantCode = rawCode ? Number(rawCode) : null;
    if (merchantCode !== null) {
      if (!Number.isInteger(merchantCode) || merchantCode <= 0) {
        throw new ClearanceValidationError("Merchant record is invalid.");
      }

      await updateMerchantAgainstApi(merchantCode, request);
      isUpdate = true;
    } else {
      await createMerchantAgainstApi(request);
    }
  } catch (error) {
    if (error instanceof Error && !(error instanceof ClearanceApiError)) {
      redirectWithMessage(returnPath, "error", error.message);
    }

    redirectWithMessage(returnPath, "error", apiErrorMessage(error, "merchant"));
  }

  revalidatePath("/clearance");
  revalidatePath("/clearance/merchant");
  redirect(`/clearance/merchant?${isUpdate ? "updated" : "saved"}=1`);
}
