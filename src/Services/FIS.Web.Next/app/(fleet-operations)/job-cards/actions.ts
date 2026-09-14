"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  authorizeJobCard,
  cancelJobCard,
  closeJobCard,
  createJobCard,
  declineJobCard,
  deleteJobCard,
  JobCardApiError,
  updateJobCard,
  updateJobCardCosts,
} from "@/lib/api/fleet-operations/api-job-cards";
import { getSession } from "@/lib/auth/session";

class JobCardValidationError extends Error {}

function text(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function positiveInteger(formData: FormData, key: string, label: string) {
  const value = text(formData, key);
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0) {
    throw new JobCardValidationError(`${label} must be a positive whole number.`);
  }
  return parsed;
}

function optionalInteger(formData: FormData, key: string, label: string) {
  const value = text(formData, key);
  if (!value) return null;
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    throw new JobCardValidationError(`${label} must be a positive whole number.`);
  }
  return parsed;
}

function optionalMoney(formData: FormData, key: string, label: string) {
  const value = text(formData, key);
  if (!value) return null;
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 0)
    throw new JobCardValidationError(`${label} must be zero or greater.`);
  return parsed;
}

function optionalDate(formData: FormData, key: string, label: string) {
  const value = text(formData, key);
  if (!value) return null;
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) throw new JobCardValidationError(`${label} is invalid.`);
  const date = new Date(`${value}T00:00:00.000Z`);
  if (Number.isNaN(date.getTime())) throw new JobCardValidationError(`${label} is invalid.`);
  return date.toISOString();
}

function optionalText(formData: FormData, key: string, label: string, maxLength: number) {
  const value = text(formData, key);
  if (value.length > maxLength)
    throw new JobCardValidationError(`${label} must be ${maxLength} characters or fewer.`);
  return value || null;
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

function hasJobCardRole(roles: readonly string[], kind: "capturer" | "authorizer") {
  const expectedRole = `jobcard${kind}`;
  return roles.some((role) => {
    const normalized = role.toLocaleLowerCase().replace(/[^a-z0-9]/g, "");
    return normalized === expectedRole;
  });
}

async function authorizePage(kind: "capturer" | "authorizer") {
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

  const allowed = hasJobCardRole(session.roles, kind);
  return allowed
    ? { ok: true as const }
    : { ok: false as const, message: `You do not have Job Card ${kind} access.` };
}

function apiErrorMessage(error: unknown) {
  if (error instanceof JobCardApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable")
      return "The Job Cards service is temporarily unavailable. Please try again.";
    if (error.reason === "not-found") return "The job card was not found.";
  }
  return "The Job Card operation failed. Please try again.";
}

function revalidateJobCardPages() {
  for (const path of [
    "/job-cards",
    "/job-cards/capturer-default",
    "/job-cards/list",
    "/job-cards/authorizer-dashboard",
    "/job-cards/authorizer-vehicles",
    "/job-cards/cancel",
    "/job-cards/close",
    "/job-cards/print",
    "/job-cards/repair-cost-report",
  ]) {
    revalidatePath(path);
  }
}

export async function createJobCardAction(formData: FormData) {
  const vmfCode = positiveInteger(formData, "vmfCode", "Vehicle");
  const path = returnPath(formData, `/job-cards/create?vmfCode=${vmfCode}`);
  const access = await authorizePage("capturer");
  if (!access.ok) redirectWithMessage(path, "error", access.message);

  try {
    const extraCodes = formData.getAll("extraCode").reduce<number[]>((codes, value) => {
      const extraCode = Number(value);
      if (Number.isInteger(extraCode) && extraCode > 0) codes.push(extraCode);
      return codes;
    }, []);
    if (extraCodes.length === 0)
      throw new JobCardValidationError("Select at least one job card category.");
    // Legacy creates each selected category in its selected order. Do not
    // parallelize procedure calls: their business side effects are ordered.
    for (const extraCode of extraCodes) {
      await createJobCard({
        vmf_code: vmfCode,
        extra_code: extraCode,
      });
    }
    revalidateJobCardPages();
    redirectWithMessage(path, "saved", "1");
  } catch (error) {
    redirectWithMessage(
      path,
      "error",
      error instanceof JobCardValidationError ? error.message : apiErrorMessage(error),
    );
  }
}

export async function updateJobCardAction(formData: FormData) {
  const id = positiveInteger(formData, "jobCardId", "Job card");
  const path = returnPath(formData, `/job-cards/list?id=${id}`);
  const access = await authorizePage("capturer");
  if (!access.ok) redirectWithMessage(path, "error", access.message);

  try {
    await updateJobCard(id, {
      jcs_comment: optionalText(formData, "jcsComment", "Job card comment", 150),
      damages: optionalText(formData, "damages", "Damages", 1)?.toUpperCase() ?? null,
      comments: optionalText(formData, "comments", "Damage comment", 500),
      assigned_to: optionalInteger(formData, "assignedTo", "Assigned user"),
      assigned_date: optionalDate(formData, "assignedDate", "Assigned date"),
    });
    revalidateJobCardPages();
    redirectWithMessage(path, "updated", "1");
  } catch (error) {
    redirectWithMessage(
      path,
      "error",
      error instanceof JobCardValidationError ? error.message : apiErrorMessage(error),
    );
  }
}

export async function authorizeJobCardAction(formData: FormData) {
  const id = positiveInteger(formData, "jobCardId", "Job card");
  const path = returnPath(formData, "/job-cards/authorizer-dashboard");
  const access = await authorizePage("authorizer");
  if (!access.ok) redirectWithMessage(path, "error", access.message);

  try {
    await authorizeJobCard(id, optionalText(formData, "comment", "Comment", 2000));
    revalidateJobCardPages();
    redirectWithMessage(path, "updated", "Job card authorized.");
  } catch (error) {
    redirectWithMessage(
      path,
      "error",
      error instanceof JobCardValidationError ? error.message : apiErrorMessage(error),
    );
  }
}

export async function declineJobCardAction(formData: FormData) {
  const id = positiveInteger(formData, "jobCardId", "Job card");
  const path = returnPath(formData, "/job-cards/authorizer-dashboard");
  const access = await authorizePage("authorizer");
  if (!access.ok) redirectWithMessage(path, "error", access.message);

  try {
    const reason = text(formData, "declineReason");
    if (!reason) throw new JobCardValidationError("A decline reason is required.");
    if (reason.length > 2000)
      throw new JobCardValidationError("The decline reason must be 2000 characters or fewer.");
    await declineJobCard(id, reason);
    revalidateJobCardPages();
    redirectWithMessage(path, "updated", "Job card declined.");
  } catch (error) {
    redirectWithMessage(
      path,
      "error",
      error instanceof JobCardValidationError ? error.message : apiErrorMessage(error),
    );
  }
}

export async function cancelJobCardAction(formData: FormData) {
  const id = positiveInteger(formData, "jobCardId", "Job card");
  const path = returnPath(formData, "/job-cards/cancel");
  const access = await authorizePage("capturer");
  if (!access.ok) redirectWithMessage(path, "error", access.message);

  try {
    await cancelJobCard(id, optionalText(formData, "cancelReason", "Cancellation reason", 2000));
    revalidateJobCardPages();
    redirectWithMessage(path, "updated", "Job card canceled.");
  } catch (error) {
    redirectWithMessage(
      path,
      "error",
      error instanceof JobCardValidationError ? error.message : apiErrorMessage(error),
    );
  }
}

export async function closeJobCardAction(formData: FormData) {
  const id = positiveInteger(formData, "jobCardId", "Job card");
  const path = returnPath(formData, "/job-cards/close");
  const access = await authorizePage("capturer");
  if (!access.ok) redirectWithMessage(path, "error", access.message);

  try {
    await closeJobCard(id, {
      close_notes: optionalText(formData, "closeNotes", "Close notes", 150),
      damages: optionalText(formData, "damages", "Damages", 1)?.toUpperCase() ?? null,
      damage_comment: optionalText(formData, "damageComment", "Damage comment", 500),
      barcode: optionalText(formData, "barcode", "Barcode", 20),
      close_date: optionalDate(formData, "closeDate", "Close date"),
    });
    revalidateJobCardPages();
    redirectWithMessage(path, "updated", "Job card closed.");
  } catch (error) {
    redirectWithMessage(
      path,
      "error",
      error instanceof JobCardValidationError ? error.message : apiErrorMessage(error),
    );
  }
}

export async function updateJobCardCostsAction(formData: FormData) {
  const id = positiveInteger(formData, "jobCardId", "Job card");
  const path = returnPath(formData, `/job-cards/list?id=${id}`);
  const access = await authorizePage("capturer");
  if (!access.ok) redirectWithMessage(path, "error", access.message);

  try {
    await updateJobCardCosts(id, {
      labour_cost: optionalMoney(formData, "labourCost", "Labour cost"),
      parts_cost: optionalMoney(formData, "partsCost", "Parts cost"),
      other_cost: optionalMoney(formData, "otherCost", "Other cost"),
      invoice_number: optionalText(formData, "invoiceNumber", "Invoice number", 50),
      invoice_date: optionalDate(formData, "invoiceDate", "Invoice date"),
      service_provider: optionalText(formData, "serviceProvider", "Service provider", 200),
    });
    revalidateJobCardPages();
    redirectWithMessage(path, "updated", "Job card costs updated.");
  } catch (error) {
    redirectWithMessage(
      path,
      "error",
      error instanceof JobCardValidationError ? error.message : apiErrorMessage(error),
    );
  }
}

export async function deleteJobCardAction(formData: FormData) {
  const id = positiveInteger(formData, "jobCardId", "Job card");
  const path = returnPath(formData, "/job-cards/list");
  const access = await authorizePage("capturer");
  if (!access.ok) redirectWithMessage(path, "error", access.message);

  try {
    await deleteJobCard(id);
    revalidateJobCardPages();
    redirectWithMessage(path, "deleted", "Job card deleted.");
  } catch (error) {
    redirectWithMessage(
      path,
      "error",
      error instanceof JobCardValidationError ? error.message : apiErrorMessage(error),
    );
  }
}
