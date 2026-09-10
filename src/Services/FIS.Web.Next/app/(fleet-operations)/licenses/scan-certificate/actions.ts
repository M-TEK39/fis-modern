"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { hasRole } from "@/app/(administration)/drivers/access";
import {
  deleteLicenseCertificate,
  LicenseCertificateApiError,
  uploadLicenseCertificate,
} from "@/lib/api/vehicles/api-license-certificates";
import { getSession } from "@/lib/auth/session";

class CertificateValidationError extends Error {}

function text(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function positiveInteger(formData: FormData, key: string, label: string) {
  const value = text(formData, key);
  const parsed = Number(value);
  if (!value || !Number.isSafeInteger(parsed) || parsed <= 0)
    throw new CertificateValidationError(`${label} must be a positive whole number.`);
  return parsed;
}

function optionalDate(formData: FormData, key: string, label: string) {
  const value = text(formData, key);
  if (!value) return "";
  if (
    !/^\d{4}-\d{2}-\d{2}$/.test(value) ||
    Number.isNaN(new Date(`${value}T00:00:00.000Z`).getTime())
  )
    throw new CertificateValidationError(`${label} is invalid.`);
  return value;
}

function returnPath(formData: FormData) {
  const value = text(formData, "returnPath");
  return value.startsWith("/licenses/scan-certificate") ? value : "/licenses/scan-certificate";
}

function redirectWith(path: string, key: "saved" | "error", message: string): never {
  redirect(
    `${path}${path.includes("?") ? "&" : "?"}${new URLSearchParams({ [key]: message }).toString()}`,
  );
}

function errorMessage(error: unknown) {
  if (error instanceof CertificateValidationError) return error.message;
  if (error instanceof LicenseCertificateApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "not-found") return "The selected vehicle or certificate was not found.";
    if (error.reason === "unavailable")
      return "The certificate service is temporarily unavailable. Please try again.";
    return error.message;
  }
  return "The certificate operation failed. Please try again.";
}

async function authorize(path: string) {
  const session = await getSession();
  if (session.status !== "authenticated")
    redirectWith(path, "error", "Your session has expired. Sign in again before continuing.");
  if (!hasRole(session.roles, "Licence"))
    redirectWith(path, "error", "You do not have permission to maintain licence certificates.");
}

export async function uploadLicenseCertificateAction(formData: FormData) {
  const path = returnPath(formData);
  await authorize(path);
  try {
    const vmfCode = positiveInteger(formData, "vmfCode", "Vehicle");
    const periodBegin = optionalDate(formData, "periodBegin", "Period begin");
    const periodEnd = optionalDate(formData, "periodEnd", "Period end");
    if (periodBegin && periodEnd && periodEnd < periodBegin)
      throw new CertificateValidationError(
        "The certificate end date must be on or after the begin date.",
      );
    const file = formData.get("file");
    if (!(file instanceof File) || file.size === 0)
      throw new CertificateValidationError("Choose a certificate image before uploading.");
    if (file.size > 20 * 1024 * 1024)
      throw new CertificateValidationError("Certificate images must be 20 MB or smaller.");
    if (!["image/jpeg", "image/png", "image/gif"].includes(file.type.toLowerCase()))
      throw new CertificateValidationError("Only JPG, PNG, or GIF image scans are accepted.");

    const upload = new FormData();
    upload.set("vmfCode", String(vmfCode));
    if (periodBegin) upload.set("periodBegin", periodBegin);
    if (periodEnd) upload.set("periodEnd", periodEnd);
    upload.set("file", file, file.name || "licence-certificate.jpg");
    await uploadLicenseCertificate(vmfCode, upload);
    revalidatePath("/licenses/scan-certificate");
    redirectWith(
      `${path.split("?")[0]}?${new URLSearchParams({ view: "vehicle", vmfCode: String(vmfCode), lookup: "1" }).toString()}`,
      "saved",
      "Licence certificate uploaded successfully.",
    );
  } catch (error) {
    redirectWith(path, "error", errorMessage(error));
  }
}

export async function deleteLicenseCertificateAction(formData: FormData) {
  const path = returnPath(formData);
  await authorize(path);
  try {
    const vmfCode = positiveInteger(formData, "vmfCode", "Vehicle");
    const source = text(formData, "source");
    const documentKey = text(formData, "documentKey");
    if (
      !["modern", "legacy"].includes(source) ||
      !documentKey ||
      documentKey.length > 255 ||
      documentKey.includes("/")
    )
      throw new CertificateValidationError("The selected certificate is invalid.");
    await deleteLicenseCertificate(source, documentKey, vmfCode);
    revalidatePath("/licenses/scan-certificate");
    redirectWith(path, "saved", "Licence certificate deleted successfully.");
  } catch (error) {
    redirectWith(path, "error", errorMessage(error));
  }
}
