"use server";

import { redirect } from "next/navigation";

import { hasFinanceRole } from "@/app/finance/_components";
import { FinanceApiError, runFinanceAction } from "@/lib/api-finance";
import { getSession } from "@/lib/session";

const ACTIONS = ["start", "check-scoa", "rollback", "finish"] as const;
type BatchAction = (typeof ACTIONS)[number];

function text(formData: FormData, name: string) {
  const value = formData.get(name);
  return typeof value === "string" ? value.trim() : "";
}

function resultPath(action: string, result: string, message: string) {
  return `/finance/batch-management/${encodeURIComponent(action)}?result=${encodeURIComponent(result)}&message=${encodeURIComponent(message)}`;
}

function messageFor(value: unknown, action: BatchAction) {
  if (value && typeof value === "object" && !Array.isArray(value)) {
    const record = value as Record<string, unknown>;
    const direct = record.message ?? record.Message ?? record.error ?? record.Error;
    if (typeof direct === "string" && direct.trim()) return direct.trim();
    if (action === "check-scoa") {
      const compliant = record.isCompliant ?? record.IsCompliant;
      const errors = record.errors ?? record.Errors;
      const warnings = record.warnings ?? record.Warnings;
      if (compliant === true)
        return warnings instanceof Array && warnings.length > 0
          ? `SCOA check passed with ${warnings.length} warning(s).`
          : "SCOA check passed.";
      if (errors instanceof Array && errors.length > 0)
        return errors.filter((item): item is string => typeof item === "string").join(" ");
      return "SCOA check found issues.";
    }
  }
  return action === "start"
    ? "Batch started."
    : action === "rollback"
      ? "Batch rolled back."
      : action === "finish"
        ? "Batch finished."
        : "Batch action completed.";
}

function apiMessage(error: unknown) {
  if (error instanceof FinanceApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable")
      return "The Finance service is temporarily unavailable. Please try again.";
    return error.message;
  }
  return "The batch action could not be completed.";
}

export async function runBatchAction(formData: FormData) {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    redirect(
      resultPath(
        "batch-management",
        "error",
        "The sign-in service is temporarily unavailable. Please try again.",
      ),
    );
  if (!hasFinanceRole(session.roles))
    redirect(
      resultPath(
        "batch-management",
        "forbidden",
        "Your account does not have permission to manage batches.",
      ),
    );

  const rawAction = text(formData, "action").toLowerCase();
  if (!ACTIONS.includes(rawAction as BatchAction))
    redirect(resultPath("batch-management", "error", "The requested batch action is invalid."));
  const action = rawAction as BatchAction;
  const endpoint = `api/finance/batch/${action}`;
  const batchDate = text(formData, "batchDate");
  if (action === "start" && !/^\d{4}-\d{2}-\d{2}$/.test(batchDate))
    redirect(resultPath(action, "error", "Select a valid batch date."));

  let result: unknown;
  try {
    result = await runFinanceAction(
      endpoint,
      action === "start" ? { batchDate: `${batchDate}T00:00:00`, financialSystemCode: 1 } : {},
    );
  } catch (error) {
    redirect(resultPath(action, "error", apiMessage(error)));
  }
  redirect(resultPath(action, "success", messageFor(result, action)));
}
