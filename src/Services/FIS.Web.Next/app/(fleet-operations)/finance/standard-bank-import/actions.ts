"use server";

import { redirect } from "next/navigation";

import { hasHeadOfficeFinanceAccess } from "@/app/(fleet-operations)/finance/_components";
import { FinanceApiError, importStandardBankFile } from "@/lib/api/finance/api-finance";
import { getSession } from "@/lib/auth/session";

function resultPath(result: string, message: string) {
  return `/finance/standard-bank-import?result=${encodeURIComponent(result)}&message=${encodeURIComponent(message)}`;
}

function apiMessage(error: unknown) {
  if (error instanceof FinanceApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable")
      return "The Finance service is temporarily unavailable. Please try again.";
    return error.message;
  }
  return "The Standard Bank file could not be imported.";
}

function responseMessage(value: unknown) {
  if (value && typeof value === "object" && !Array.isArray(value)) {
    const record = value as Record<string, unknown>;
    const imported = record.recordsImported ?? record.RecordsImported;
    const failed = record.recordsFailed ?? record.RecordsFailed;
    if (typeof imported === "number" && typeof failed === "number") {
      return failed > 0
        ? `Imported ${imported} transaction(s); ${failed} row(s) failed.`
        : imported > 0
          ? `Imported ${imported} transaction(s) successfully.`
          : "No transaction rows were imported.";
    }
    const message = record.message ?? record.Message ?? record.error ?? record.Error;
    if (typeof message === "string" && message.trim()) return message.trim();
  }
  return "Standard Bank file import completed.";
}

export async function importStandardBankAction(formData: FormData) {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    redirect(
      resultPath("error", "The sign-in service is temporarily unavailable. Please try again."),
    );
  if (!hasHeadOfficeFinanceAccess(session.siteCode, session.email, session.roles))
    redirect(
      resultPath(
        "forbidden",
        "Your account does not have permission to import Standard Bank transactions.",
      ),
    );

  const fileValue = formData.get("file");
  if (
    !fileValue ||
    typeof fileValue !== "object" ||
    !("arrayBuffer" in fileValue) ||
    typeof fileValue.arrayBuffer !== "function"
  )
    redirect(resultPath("error", "Select a CSV file before uploading."));
  const file = fileValue as Blob & { name?: string; size?: number };
  if (!file.name?.toLowerCase().endsWith(".csv"))
    redirect(resultPath("error", "Only CSV files can be uploaded."));
  if (!file.size || file.size <= 0 || file.size > 20 * 1024 * 1024)
    redirect(resultPath("error", "The CSV file must be between 1 byte and 20 MB."));

  let result: unknown;
  try {
    result = await importStandardBankFile(file, file.name);
  } catch (error) {
    redirect(resultPath("error", apiMessage(error)));
  }
  redirect(resultPath("success", responseMessage(result)));
}
