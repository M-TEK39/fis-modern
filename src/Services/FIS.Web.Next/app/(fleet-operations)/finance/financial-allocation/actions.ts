"use server";

import { redirect } from "next/navigation";

import { hasFinanceRole } from "@/app/(fleet-operations)/finance/_components";
import { activateBasSegments, FinanceApiError, importBas } from "@/lib/api/finance/api-finance";
import { getSession } from "@/lib/auth/session";

function text(formData: FormData, name: string) {
  const value = formData.get(name);
  return typeof value === "string" ? value.trim() : "";
}

function resultPath(action: string, result: string, message: string) {
  return `/finance/financial-allocation/${encodeURIComponent(action)}?result=${encodeURIComponent(result)}&message=${encodeURIComponent(message)}`;
}

function department(formData: FormData) {
  const value = text(formData, "departmentCode");
  if (!value) return undefined;
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : undefined;
}

function responseMessage(value: unknown, fallback: string) {
  if (value && typeof value === "object" && !Array.isArray(value)) {
    const record = value as Record<string, unknown>;
    const message = record.message ?? record.Message ?? record.error ?? record.Error;
    if (typeof message === "string" && message.trim()) return message.trim();
    const imported = record.recordsImported ?? record.RecordsImported;
    const errors = record.errors ?? record.Errors;
    if (typeof imported === "number")
      return `Imported/updated ${imported} BAS segment row(s).${Array.isArray(errors) && errors.length ? ` ${errors.length} warning(s).` : ""}`;
    const updated = record.updated ?? record.Updated;
    if (typeof updated === "number") return `Activated ${updated} segment(s).`;
  }
  return fallback;
}

function apiMessage(error: unknown) {
  if (error instanceof FinanceApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable")
      return "The Finance service is temporarily unavailable. Please try again.";
    return error.message;
  }
  return "The Finance operation could not be completed.";
}

export async function importBasAction(formData: FormData) {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    redirect(
      resultPath(
        "import-bas",
        "error",
        "The sign-in service is temporarily unavailable. Please try again.",
      ),
    );
  if (!hasFinanceRole(session.roles))
    redirect(
      resultPath(
        "import-bas",
        "forbidden",
        "Your account does not have permission to maintain BAS segments.",
      ),
    );

  const file = formData.get("file");
  if (
    !file ||
    typeof file !== "object" ||
    !("arrayBuffer" in file) ||
    typeof file.arrayBuffer !== "function"
  )
    redirect(resultPath("import-bas", "error", "Select a BAS import file before submitting."));
  if (file.size <= 0 || file.size > 20 * 1024 * 1024)
    redirect(
      resultPath("import-bas", "error", "The BAS import file must be between 1 byte and 20 MB."),
    );

  let result: unknown;
  try {
    const fileData = Buffer.from(await file.arrayBuffer()).toString("base64");
    result = await importBas(fileData, department(formData));
  } catch (error) {
    redirect(resultPath("import-bas", "error", apiMessage(error)));
  }
  redirect(resultPath("import-bas", "success", responseMessage(result, "BAS import completed.")));
}

export async function activateBasSegmentsAction(formData: FormData) {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated")
    redirect(
      resultPath(
        "activate-bas",
        "error",
        "The sign-in service is temporarily unavailable. Please try again.",
      ),
    );
  if (!hasFinanceRole(session.roles))
    redirect(
      resultPath(
        "activate-bas",
        "forbidden",
        "Your account does not have permission to maintain BAS segments.",
      ),
    );

  const segmentCodes = formData
    .getAll("segmentCode")
    .map((value) => (typeof value === "string" ? Number(value) : NaN))
    .filter((value) => Number.isSafeInteger(value) && value > 0);
  if (segmentCodes.length === 0)
    redirect(
      resultPath(
        "activate-bas",
        "error",
        "Select at least one BAS segment before updating the list.",
      ),
    );
  let result: unknown;
  try {
    result = await activateBasSegments(segmentCodes);
  } catch (error) {
    redirect(resultPath("activate-bas", "error", apiMessage(error)));
  }
  redirect(
    resultPath("activate-bas", "success", responseMessage(result, "BAS segment list updated.")),
  );
}
