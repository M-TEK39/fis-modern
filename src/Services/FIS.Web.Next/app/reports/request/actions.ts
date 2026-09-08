"use server";

import { redirect } from "next/navigation";

import { hasReportsRole } from "@/app/reports/_components";
import { LegacyReportApiError, submitAdditionalReportRequest } from "@/lib/api-legacy-reports";
import type { ReportRequestResult } from "@/lib/api-legacy-reports";
import { getSession } from "@/lib/session";

const RETURN_PATH = "/reports/request";

function text(formData: FormData, name: string) {
  const value = formData.get(name);
  return typeof value === "string" ? value.trim() : "";
}

function resultPath(result: string, message?: string) {
  const params = new URLSearchParams({ result });
  if (message) params.set("message", message);
  return `${RETURN_PATH}?${params.toString()}`;
}

function apiMessage(error: unknown) {
  if (error instanceof LegacyReportApiError) {
    if (error.reason === "unauthorized") return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable") return "The reports service is temporarily unavailable. Please try again.";
    return error.message || "The report request could not be submitted.";
  }
  return "The report request could not be submitted.";
}

export async function submitAdditionalReportRequestAction(formData: FormData) {
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated") redirect(resultPath("unavailable", "The sign-in service is temporarily unavailable. Please try again."));
  if (!hasReportsRole(session.roles)) redirect(resultPath("forbidden", "Your account needs the legacy Reports permission."));

  const category = text(formData, "category") || "New Report";
  const priority = text(formData, "priority") || "Medium";
  const requestedBy = text(formData, "requestedBy");
  const email = text(formData, "email");
  const subject = text(formData, "subject");
  const module = text(formData, "module");
  const details = text(formData, "details");

  if (!subject || !details) redirect(resultPath("invalid", "Subject and detailed request are required."));
  if (email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) redirect(resultPath("invalid", "Enter a valid contact email or leave it blank."));

  let result: ReportRequestResult;
  try {
    result = await submitAdditionalReportRequest({
      reportType: subject,
      requestedBy,
      parameters: {
        category,
        priority,
        email,
        subject,
        module,
        details,
        requestedAt: new Date().toISOString(),
      },
    });
  } catch (error) {
    redirect(resultPath("error", apiMessage(error)));
  }

  redirect(resultPath(result.success ? "success" : "error", result.message));
}
