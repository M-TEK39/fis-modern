"use server";

import { redirect } from "next/navigation";

import { hasFinanceRole } from "@/app/finance/_components";
import { FinanceApiError, runFinanceAction } from "@/lib/api-finance";
import { getSession } from "@/lib/session";

function text(formData: FormData, name: string) {
  const value = formData.get(name);
  return typeof value === "string" ? value.trim() : "";
}

function resultPath(result: string, message: string) {
  return `/finance/missing-kilometres/close-gaps?result=${encodeURIComponent(result)}&message=${encodeURIComponent(message)}`;
}

function apiMessage(error: unknown) {
  if (error instanceof FinanceApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable")
      return "The Finance service is temporarily unavailable. Please try again.";
    return error.message;
  }
  return "The missing-kilometres operation could not be completed.";
}

function responseMessage(value: unknown) {
  if (value && typeof value === "object" && !Array.isArray(value)) {
    const record = value as Record<string, unknown>;
    const message = record.message ?? record.Message ?? record.error ?? record.Error;
    if (typeof message === "string" && message.trim()) return message.trim();
  }
  return "Missing kilometre gaps were processed.";
}

export async function closeMissingKilometresAction(formData: FormData) {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    redirect(
      resultPath("error", "The sign-in service is temporarily unavailable. Please try again."),
    );
  if (!hasFinanceRole(session.roles))
    redirect(
      resultPath("forbidden", "Your account does not have permission to close kilometre gaps."),
    );

  const financialYear = text(formData, "financialYear");
  if (!/^\d{4}$/.test(financialYear))
    redirect(resultPath("error", "Select a valid financial year before closing kilometre gaps."));

  let result: unknown;
  try {
    result = await runFinanceAction("api/finance/missing-kilometres/close-gaps", { financialYear });
  } catch (error) {
    redirect(resultPath("error", apiMessage(error)));
  }
  redirect(resultPath("success", responseMessage(result)));
}
