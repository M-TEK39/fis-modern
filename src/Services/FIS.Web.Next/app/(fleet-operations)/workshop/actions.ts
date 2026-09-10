"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  createWorkshop,
  deleteWorkshop,
  getWorkshop,
  updateWorkshop,
  WorkshopApiError,
  type WorkshopInput,
} from "@/lib/api/fleet-operations/api-workshop";
import {
  createWorkshopMerchant,
  deleteWorkshopMerchant,
  updateWorkshopMerchant,
  type WorkshopMerchantInput,
} from "@/lib/api/fleet-operations/api-workshop-merchant";
import { getSession } from "@/lib/auth/session";

const WORKSHOP_ROLE = "Workshop";

class WorkshopValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getInteger(formData: FormData, key: string, label: string, required = true) {
  const value = getText(formData, key);
  if (!value && !required) return null;
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0) {
    throw new WorkshopValidationError(`${label} must be a positive whole number.`);
  }
  return parsed;
}

function getDate(formData: FormData, key: string, label: string, required = false) {
  const value = getText(formData, key);
  if (!value && !required) return null;
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    throw new WorkshopValidationError(`${label} is invalid.`);
  }
  const parsed = new Date(`${value}T00:00:00.000Z`);
  if (Number.isNaN(parsed.getTime())) throw new WorkshopValidationError(`${label} is invalid.`);
  return parsed.toISOString();
}

function getTime(formData: FormData, key: string) {
  const value = getText(formData, key);
  if (!value) return null;
  if (!/^\d{2}:\d{2}$/.test(value)) throw new WorkshopValidationError("Workshop time is invalid.");
  return `${value}:00`;
}

function getReturnPath(formData: FormData, fallback: string) {
  const value = getText(formData, "returnPath");
  return value.startsWith("/") && !value.startsWith("//") ? value : fallback;
}

function redirectWithMessage(path: string, key: string, message: string): never {
  const query = new URLSearchParams({ [key]: message });
  redirect(`${path}?${query.toString()}`);
}

async function authorizeWorkshop() {
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
  if (
    !session.roles.some(
      (role) => role.localeCompare(WORKSHOP_ROLE, undefined, { sensitivity: "accent" }) === 0,
    )
  ) {
    return {
      ok: false as const,
      message: "You do not have permission to maintain Workshop records.",
    };
  }
  return { ok: true as const };
}

function apiErrorMessage(error: unknown, subject: string) {
  if (error instanceof WorkshopApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable")
      return `The ${subject} service is temporarily unavailable. Please try again.`;
    if (error.reason === "not-found") return `The ${subject} record was not found.`;
  }
  return `${subject[0].toUpperCase()}${subject.slice(1)} operation failed. Please try again.`;
}

function buildWorkshopInput(formData: FormData): WorkshopInput {
  const vmfCode = getInteger(formData, "vmfCode", "Vehicle");
  const receiveDate = getDate(formData, "receiveDate", "Receive date", true);
  if (vmfCode === null || receiveDate === null)
    throw new WorkshopValidationError("Vehicle and receive date are required.");

  return {
    vmf_code: vmfCode,
    receive_time: getTime(formData, "receiveTime"),
    receive_date: receiveDate,
    complete_time: getTime(formData, "completeTime"),
    complete_date: getDate(formData, "completeDate", "Complete date"),
  };
}

export async function saveWorkshopAction(formData: FormData) {
  const returnPath = getReturnPath(formData, "/workshop/entry");
  const access = await authorizeWorkshop();
  if (!access.ok) redirectWithMessage(returnPath, "error", access.message);

  try {
    const input = buildWorkshopInput(formData);
    const rawCode = getText(formData, "wwCode");
    if (rawCode) {
      const code = Number(rawCode);
      if (!Number.isInteger(code) || code <= 0)
        throw new WorkshopValidationError("Workshop entry is invalid.");
      await updateWorkshop(code, input);
      revalidatePath("/workshop");
      revalidatePath("/workshop/entry");
      redirectWithMessage(`/workshop/entry/modify?id=${code}`, "updated", "1");
    }

    await createWorkshop(input);
    revalidatePath("/workshop");
    revalidatePath("/workshop/entry");
    redirectWithMessage("/workshop/entry", "saved", "1");
  } catch (error) {
    if (error instanceof WorkshopValidationError)
      redirectWithMessage(returnPath, "error", error.message);
    redirectWithMessage(returnPath, "error", apiErrorMessage(error, "workshop"));
  }
}

export async function deleteWorkshopAction(formData: FormData) {
  const returnPath = getReturnPath(formData, "/workshop/entry/delete");
  const access = await authorizeWorkshop();
  if (!access.ok) redirectWithMessage(returnPath, "error", access.message);

  try {
    const code = getInteger(formData, "wwCode", "Workshop entry");
    if (code === null) throw new WorkshopValidationError("Select a workshop entry.");
    await deleteWorkshop(code);
    revalidatePath("/workshop");
    revalidatePath("/workshop/entry");
    redirectWithMessage(returnPath, "deleted", "1");
  } catch (error) {
    if (error instanceof WorkshopValidationError)
      redirectWithMessage(returnPath, "error", error.message);
    redirectWithMessage(returnPath, "error", apiErrorMessage(error, "workshop"));
  }
}

export async function reopenWorkshopAction(formData: FormData) {
  const returnPath = getReturnPath(formData, "/workshop/open-job-card");
  const access = await authorizeWorkshop();
  if (!access.ok) redirectWithMessage(returnPath, "error", access.message);

  try {
    const code = getInteger(formData, "wwCode", "Workshop entry");
    if (code === null) throw new WorkshopValidationError("Select a closed workshop entry.");
    if (!getText(formData, "password"))
      throw new WorkshopValidationError("Authorizer password is required.");
    const existing = await getWorkshop(code);
    await updateWorkshop(code, {
      vmf_code: existing.vmfCode,
      receive_time: existing.receiveTime,
      receive_date: existing.receiveDate,
      complete_time: null,
      complete_date: null,
    });
    revalidatePath("/workshop");
    revalidatePath("/workshop/open-job-card");
    redirectWithMessage(returnPath, "reopened", "1");
  } catch (error) {
    if (error instanceof WorkshopValidationError)
      redirectWithMessage(returnPath, "error", error.message);
    redirectWithMessage(returnPath, "error", apiErrorMessage(error, "workshop"));
  }
}

function buildMerchantInput(formData: FormData): WorkshopMerchantInput {
  const name = getText(formData, "merchantName");
  if (!name) throw new WorkshopValidationError("Merchant name is required.");
  if (name.length > 40)
    throw new WorkshopValidationError("Merchant name must be 40 characters or fewer.");
  return {
    name,
    tel: getText(formData, "merchantTel") || null,
    fax: getText(formData, "merchantFax") || null,
    email: getText(formData, "merchantEmail") || null,
  };
}

export async function saveWorkshopMerchantAction(formData: FormData) {
  const returnPath = getReturnPath(formData, "/workshop/merchant");
  const access = await authorizeWorkshop();
  if (!access.ok) redirectWithMessage(returnPath, "error", access.message);

  try {
    const input = buildMerchantInput(formData);
    const rawCode = getText(formData, "merchantCode");
    if (rawCode) {
      const code = Number(rawCode);
      if (!Number.isInteger(code) || code <= 0)
        throw new WorkshopValidationError("Merchant is invalid.");
      await updateWorkshopMerchant(code, input);
      revalidatePath("/workshop/merchant");
      redirectWithMessage(returnPath, "updated", "1");
    }
    await createWorkshopMerchant(input);
    revalidatePath("/workshop/merchant");
    redirectWithMessage(returnPath, "saved", "1");
  } catch (error) {
    if (error instanceof WorkshopValidationError)
      redirectWithMessage(returnPath, "error", error.message);
    redirectWithMessage(returnPath, "error", apiErrorMessage(error, "merchant"));
  }
}

export async function deleteWorkshopMerchantAction(formData: FormData) {
  const returnPath = getReturnPath(formData, "/workshop/merchant");
  const access = await authorizeWorkshop();
  if (!access.ok) redirectWithMessage(returnPath, "error", access.message);

  try {
    const code = getInteger(formData, "merchantCode", "Merchant");
    if (code === null) throw new WorkshopValidationError("Select a merchant.");
    await deleteWorkshopMerchant(code);
    revalidatePath("/workshop/merchant");
    redirectWithMessage(returnPath, "deleted", "1");
  } catch (error) {
    if (error instanceof WorkshopValidationError)
      redirectWithMessage(returnPath, "error", error.message);
    redirectWithMessage(returnPath, "error", apiErrorMessage(error, "merchant"));
  }
}
