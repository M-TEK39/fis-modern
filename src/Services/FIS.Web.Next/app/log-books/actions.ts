"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { createLogbook, deleteLogbook, LogbookApiError, updateLogbook } from "@/lib/api-logbooks";
import { getSession } from "@/lib/session";

class LogbookValidationError extends Error {}

function text(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function positiveInteger(formData: FormData, key: string, label: string) {
  const value = text(formData, key);
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0)
    throw new LogbookValidationError(`${label} must be a positive whole number.`);
  return parsed;
}

function optionalInteger(formData: FormData, key: string, label: string) {
  const value = text(formData, key);
  if (!value) return null;
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0)
    throw new LogbookValidationError(`${label} must be a positive whole number.`);
  return parsed;
}

function optionalText(formData: FormData, key: string, label: string, maxLength: number) {
  const value = text(formData, key);
  if (value.length > maxLength)
    throw new LogbookValidationError(`${label} must be ${maxLength} characters or fewer.`);
  return value || null;
}

function optionalDate(formData: FormData, key: string, label: string) {
  const value = text(formData, key);
  if (!value) return null;
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) throw new LogbookValidationError(`${label} is invalid.`);
  const date = new Date(`${value}T00:00:00.000Z`);
  if (Number.isNaN(date.getTime())) throw new LogbookValidationError(`${label} is invalid.`);
  return date.toISOString();
}

function returnPath(formData: FormData, fallback: string) {
  const value = text(formData, "returnPath");
  return value.startsWith("/") && !value.startsWith("//") ? value : fallback;
}

function redirectWithMessage(
  path: string,
  key: "saved" | "updated" | "deleted" | "error",
  message: string,
): never {
  const separator = path.includes("?") ? "&" : "?";
  redirect(`${path}${separator}${new URLSearchParams({ [key]: message }).toString()}`);
}

async function authorizeLogbooks() {
  const session = await getSession();
  if (session.status === "unavailable")
    return {
      ok: false as const,
      message: "The sign-in service is temporarily unavailable. Please try again.",
    };
  if (session.status !== "authenticated")
    return {
      ok: false as const,
      message: "Your session has expired. Sign in again before continuing.",
    };
  const allowed = session.roles.some(
    (role) => role.toLocaleLowerCase().replace(/[^a-z0-9]/g, "") === "logbooks",
  );
  return allowed
    ? { ok: true as const }
    : { ok: false as const, message: "You do not have Logbooks access." };
}

function apiErrorMessage(error: unknown) {
  if (error instanceof LogbookApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable")
      return "The Logbooks service is temporarily unavailable. Please try again.";
    if (error.reason === "not-found") return "The logbook was not found.";
  }
  return "The Logbook operation failed. Please try again.";
}

function revalidateLogbookPages() {
  for (const path of [
    "/log-books",
    "/log-books/maintenance",
    "/log-books/collection",
    "/log-books/delete",
    "/log-books/help",
  ])
    revalidatePath(path);
}

function getWriteInput(formData: FormData, includeDateCreated = false) {
  const vmfCode = optionalInteger(formData, "vmfCode", "Vehicle");
  if (vmfCode === null) throw new LogbookValidationError("Vehicle is required.");
  return {
    vmf_code: vmfCode,
    begin_num: optionalText(formData, "beginNumber", "Begin number", 8),
    end_num: optionalText(formData, "endNumber", "End number", 8),
    handout_date: optionalDate(formData, "handoutDate", "Handout date"),
    site_code: optionalInteger(formData, "siteCode", "Site"),
    lb_receiver_name: optionalText(formData, "receiverName", "Receiver name", 25),
    lb_tel_num: optionalText(formData, "telephoneNumber", "Receiver telephone", 20),
    lb_comment: optionalText(formData, "comment", "Comment", 60),
    ...(includeDateCreated
      ? { date_created: optionalDate(formData, "dateCreated", "Created date") }
      : {}),
  };
}

export async function createLogbookAction(formData: FormData) {
  const path = returnPath(formData, "/log-books/maintenance");
  const access = await authorizeLogbooks();
  if (!access.ok) redirectWithMessage(path, "error", access.message);
  try {
    await createLogbook(getWriteInput(formData));
    revalidateLogbookPages();
    redirectWithMessage(path, "saved", "Logbook saved.");
  } catch (error) {
    redirectWithMessage(
      path,
      "error",
      error instanceof LogbookValidationError ? error.message : apiErrorMessage(error),
    );
  }
}

export async function updateLogbookAction(formData: FormData) {
  const id = positiveInteger(formData, "logbookId", "Logbook");
  const path = returnPath(formData, `/log-books/maintenance?vmfCode=${text(formData, "vmfCode")}`);
  const access = await authorizeLogbooks();
  if (!access.ok) redirectWithMessage(path, "error", access.message);
  try {
    await updateLogbook(id, getWriteInput(formData, true));
    revalidateLogbookPages();
    redirectWithMessage(path, "updated", "Logbook updated.");
  } catch (error) {
    redirectWithMessage(
      path,
      "error",
      error instanceof LogbookValidationError ? error.message : apiErrorMessage(error),
    );
  }
}

export async function deleteLogbookAction(formData: FormData) {
  const id = positiveInteger(formData, "logbookId", "Logbook");
  const path = returnPath(formData, "/log-books/delete");
  const access = await authorizeLogbooks();
  if (!access.ok) redirectWithMessage(path, "error", access.message);
  try {
    await deleteLogbook(id);
    revalidateLogbookPages();
    redirectWithMessage(path, "deleted", "Logbook deleted.");
  } catch (error) {
    redirectWithMessage(
      path,
      "error",
      error instanceof LogbookValidationError ? error.message : apiErrorMessage(error),
    );
  }
}

export async function collectLogbooksAction(formData: FormData) {
  const path = returnPath(formData, "/log-books/collection");
  const access = await authorizeLogbooks();
  if (!access.ok) redirectWithMessage(path, "error", access.message);
  try {
    const vmfCodes = formData
      .getAll("vmfCode")
      .map((value) => Number(value))
      .filter((value) => Number.isInteger(value) && value > 0);
    if (vmfCodes.length === 0) throw new LogbookValidationError("Select at least one vehicle.");
    const base = getWriteInput(formData);
    for (const vmfCode of [...new Set(vmfCodes)])
      await createLogbook({ ...base, vmf_code: vmfCode });
    revalidateLogbookPages();
    redirectWithMessage(
      path,
      "saved",
      `${vmfCodes.length} logbook handout${vmfCodes.length === 1 ? "" : "s"} saved.`,
    );
  } catch (error) {
    redirectWithMessage(
      path,
      "error",
      error instanceof LogbookValidationError ? error.message : apiErrorMessage(error),
    );
  }
}
