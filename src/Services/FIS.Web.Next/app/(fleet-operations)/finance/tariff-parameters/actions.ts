"use server";

import { redirect } from "next/navigation";

import { hasTariffApproverRole } from "@/app/(fleet-operations)/finance/_components";
import { FinanceApiError, runFinanceAction } from "@/lib/api/finance/api-finance";
import { getSession } from "@/lib/auth/session";

function text(formData: FormData, name: string) {
  const value = formData.get(name);
  return typeof value === "string" ? value.trim() : "";
}

function resultPath(year: string, result: string, message: string) {
  return `/finance/tariff-parameters?year=${encodeURIComponent(year)}&result=${encodeURIComponent(result)}&message=${encodeURIComponent(message)}`;
}

function apiMessage(error: unknown) {
  if (error instanceof FinanceApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable")
      return "The Finance service is temporarily unavailable. Please try again.";
    return error.message;
  }
  return "The tariff parameter operation could not be completed.";
}

function responseMessage(value: unknown, year: string, operation: string) {
  if (value && typeof value === "object" && !Array.isArray(value)) {
    const record = value as Record<string, unknown>;
    const message = record.message ?? record.Message ?? record.error ?? record.Error;
    if (typeof message === "string" && message.trim()) return message.trim();
  }
  return `Tariff parameters for ${year} ${operation}.`;
}

export async function updateTariffParametersAction(formData: FormData) {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    redirect(
      resultPath("", "error", "The sign-in service is temporarily unavailable. Please try again."),
    );
  if (!hasTariffApproverRole(session.roles))
    redirect(
      resultPath(
        "",
        "forbidden",
        "Your account does not have permission to approve or reject tariff parameters.",
      ),
    );

  const year = text(formData, "year");
  const operation = text(formData, "operation").toLowerCase();
  if (!/^\d{4}$/.test(year) || !["approve", "reject"].includes(operation))
    redirect(resultPath(year, "error", "The tariff parameter operation is invalid."));

  let result: unknown;
  try {
    result = await runFinanceAction(
      `api/finance/tariff-parameters/${encodeURIComponent(year)}/${operation}`,
      {},
    );
  } catch (error) {
    redirect(resultPath(year, "error", apiMessage(error)));
  }
  redirect(
    resultPath(
      year,
      "success",
      responseMessage(result, year, operation === "approve" ? "approved" : "rejected"),
    ),
  );
}
