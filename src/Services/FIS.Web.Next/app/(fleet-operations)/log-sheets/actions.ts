"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  createLogsheet,
  deleteLogsheet,
  LogsheetApiError,
  updateLogsheet,
} from "@/lib/api/fleet-operations/api-logsheets";
import { getSession } from "@/lib/auth/session";

class LogsheetValidationError extends Error {}

function text(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function requiredInteger(formData: FormData, key: string, label: string) {
  const value = text(formData, key);
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0)
    throw new LogsheetValidationError(`${label} must be a positive whole number.`);
  return parsed;
}

function optionalInteger(formData: FormData, key: string, label: string) {
  const value = text(formData, key);
  if (!value) return null;
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed < 0)
    throw new LogsheetValidationError(`${label} must be a non-negative whole number.`);
  return parsed;
}

function odometer(formData: FormData, key: string, label: string) {
  const value = text(formData, key);
  const parsed = Number(value);
  if (!value || !Number.isFinite(parsed) || parsed < 0)
    throw new LogsheetValidationError(`${label} must be a non-negative number.`);
  return parsed;
}

function date(formData: FormData) {
  const value = text(formData, "month");
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value))
    throw new LogsheetValidationError("Logsheet month is invalid.");
  const parsed = new Date(`${value}T00:00:00.000Z`);
  if (Number.isNaN(parsed.getTime()))
    throw new LogsheetValidationError("Logsheet month is invalid.");
  return parsed.toISOString();
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

async function authorizeLogsheetEntry() {
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
  const hasReportsRole = session.roles.some(
    (role) => role.toLocaleLowerCase().replace(/[^a-z0-9]/g, "") === "reports",
  );
  if (!hasReportsRole)
    return { ok: false as const, message: "You do not have permission to change logsheets." };
  return { ok: true as const, session };
}

async function authorizeLogsheetManagement() {
  const access = await authorizeLogsheetEntry();
  if (!access.ok) return access;

  const code = Number(access.session.userAccessCode);
  return [279, 47, 38].includes(code)
    ? { ok: true as const }
    : { ok: false as const, message: "You do not have permission to change logsheets." };
}

function apiErrorMessage(error: unknown) {
  if (error instanceof LogsheetApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable")
      return "The Logsheet service is temporarily unavailable. Please try again.";
    if (error.reason === "not-found") return "The logsheet was not found.";
    if (error.reason === "conflict") return error.message;
  }
  return "The Logsheet operation failed. Please try again.";
}

function writeInput(formData: FormData) {
  const vmfCode = requiredInteger(formData, "vmfCode", "Vehicle");
  const requisition = text(formData, "requisition");
  if (!requisition || requisition.length > 10)
    throw new LogsheetValidationError(
      "Requisition number is required and must be 10 characters or fewer.",
    );
  const startOdo = odometer(formData, "startOdo", "Start odometer");
  const endOdo = odometer(formData, "endOdo", "End odometer");
  if (endOdo < startOdo)
    throw new LogsheetValidationError(
      "End odometer must be greater than or equal to start odometer.",
    );
  const contractCode = requiredInteger(formData, "contractCode", "Contract");
  return {
    vmf_code: vmfCode,
    start_odo: startOdo,
    end_odo: endOdo,
    month: date(formData),
    // The selected legacy contract determines the stored site and department.
    site_code: 0,
    rek_num: requisition,
    days_used: optionalInteger(formData, "daysUsed", "Days used"),
    bund_num: optionalInteger(formData, "bundleNumber", "Batch number"),
    contract_code: contractCode,
  };
}

function revalidateLogsheetPages() {
  for (const path of [
    "/log-sheets",
    "/log-sheets/enter",
    "/log-sheets/edit",
    "/log-sheets/delete",
    "/log-sheets/reports/captured",
    "/log-sheets/reports/total-km",
    "/log-sheets/help",
  ])
    revalidatePath(path);
}

export async function createLogsheetAction(formData: FormData) {
  const path = returnPath(formData, "/log-sheets/enter");
  const access = await authorizeLogsheetEntry();
  if (!access.ok) redirectWithMessage(path, "error", access.message);
  try {
    await createLogsheet(writeInput(formData));
    revalidateLogsheetPages();
    redirectWithMessage(path, "saved", "Logsheet saved.");
  } catch (error) {
    redirectWithMessage(
      path,
      "error",
      error instanceof LogsheetValidationError ? error.message : apiErrorMessage(error),
    );
  }
}

export async function updateLogsheetAction(formData: FormData) {
  const logCode = requiredInteger(formData, "logsheetId", "Logsheet");
  const path = returnPath(formData, "/log-sheets/edit");
  const access = await authorizeLogsheetManagement();
  if (!access.ok) redirectWithMessage(path, "error", access.message);
  try {
    await updateLogsheet(logCode, writeInput(formData));
    revalidateLogsheetPages();
    redirectWithMessage(path, "updated", "Logsheet updated.");
  } catch (error) {
    redirectWithMessage(
      path,
      "error",
      error instanceof LogsheetValidationError ? error.message : apiErrorMessage(error),
    );
  }
}

export async function deleteLogsheetAction(formData: FormData) {
  const logCode = requiredInteger(formData, "logsheetId", "Logsheet");
  const path = returnPath(formData, "/log-sheets/delete");
  const access = await authorizeLogsheetManagement();
  if (!access.ok) redirectWithMessage(path, "error", access.message);
  try {
    await deleteLogsheet(logCode);
    revalidateLogsheetPages();
    redirectWithMessage(path, "deleted", "Logsheet deleted.");
  } catch (error) {
    redirectWithMessage(
      path,
      "error",
      error instanceof LogsheetValidationError ? error.message : apiErrorMessage(error),
    );
  }
}
