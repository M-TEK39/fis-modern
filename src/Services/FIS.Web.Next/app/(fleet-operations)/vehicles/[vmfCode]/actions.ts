"use server";

import { revalidatePath } from "next/cache";

import {
  deleteVehicleDocument,
  VEHICLE_DOCUMENT_CATEGORIES,
  VehicleDocumentApiError,
  uploadVehicleDocument,
} from "@/lib/api/vehicles/api-vehicle-documents";
import {
  deleteVehicleAgainstApi,
  updateVehicleInvoiceAgainstApi,
  VehicleEditApiError,
} from "@/lib/api/vehicles/api-vehicle-edit";
import { getSession } from "@/lib/auth/session";

const VEHICLE_MANAGEMENT_PERMISSION = 1;
const MAX_DOCUMENT_BYTES = 20 * 1024 * 1024;
const ALLOWED_DOCUMENT_MIME_TYPES = new Set(["image/jpeg", "image/png", "application/pdf"]);

export type VehicleDetailActionState = {
  status: "idle" | "success" | "error";
  message?: string;
};

async function authorizeVehicleDetail() {
  const session = await getSession();

  if (session.status === "unavailable") {
    return {
      ok: false as const,
      message: "The sign-in service is temporarily unavailable. Please try again.",
    };
  }

  if (session.status !== "authenticated") {
    return {
      ok: false as const,
      message: "Your session has expired. Sign in again before continuing.",
    };
  }

  try {
    if (
      (BigInt(session.accessLevel ?? "0") & BigInt(VEHICLE_MANAGEMENT_PERMISSION)) !==
      BigInt(VEHICLE_MANAGEMENT_PERMISSION)
    ) {
      return {
        ok: false as const,
        message: "You do not have permission to maintain Vehicle Master records.",
      };
    }
  } catch {
    return { ok: false as const, message: "Your Vehicle Master permission could not be verified." };
  }

  return { ok: true as const };
}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getVmfCode(formData: FormData) {
  const vmfCode = Number(getText(formData, "vmfCode"));
  return Number.isInteger(vmfCode) && vmfCode > 0 ? vmfCode : null;
}

function apiErrorMessage(error: unknown, fallback: string) {
  if (error instanceof VehicleEditApiError || error instanceof VehicleDocumentApiError) {
    if (error.reason === "unauthorized") {
      return "Your session has expired. Sign in again before continuing.";
    }

    if (error.reason === "not-found") {
      return "The selected vehicle or document could not be found.";
    }

    if (error.reason === "unavailable") {
      return "The vehicle service is temporarily unavailable. Please try again.";
    }

    return error.message || fallback;
  }

  return fallback;
}

function revalidateVehicle(vmfCode: number) {
  revalidatePath(`/vehicles/${vmfCode}`);
  revalidatePath("/vehicles");
}

export async function updateVehicleInvoiceAction(
  _previousState: VehicleDetailActionState,
  formData: FormData,
): Promise<VehicleDetailActionState> {
  const access = await authorizeVehicleDetail();
  if (!access.ok) {
    return { status: "error", message: access.message };
  }

  const vmfCode = getVmfCode(formData);
  if (vmfCode === null) {
    return { status: "error", message: "The selected vehicle is invalid." };
  }

  const invoiceNumber = getText(formData, "invoiceNumber");
  if (invoiceNumber.length > 60) {
    return { status: "error", message: "Invoice number cannot exceed 60 characters." };
  }

  try {
    await updateVehicleInvoiceAgainstApi(vmfCode, invoiceNumber || null);
    revalidateVehicle(vmfCode);
    return { status: "success", message: "Invoice number updated successfully." };
  } catch (error) {
    console.error(
      "FIS vehicle invoice update failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return {
      status: "error",
      message: apiErrorMessage(error, "The invoice number could not be updated."),
    };
  }
}

export async function deleteVehicleAction(
  _previousState: VehicleDetailActionState,
  formData: FormData,
): Promise<VehicleDetailActionState> {
  const access = await authorizeVehicleDetail();
  if (!access.ok) {
    return { status: "error", message: access.message };
  }

  const vmfCode = getVmfCode(formData);
  if (vmfCode === null) {
    return { status: "error", message: "The selected vehicle is invalid." };
  }

  try {
    await deleteVehicleAgainstApi(vmfCode);
    revalidateVehicle(vmfCode);
    return { status: "success", message: "Vehicle deleted successfully." };
  } catch (error) {
    console.error(
      "FIS vehicle delete failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return {
      status: "error",
      message: apiErrorMessage(error, "The vehicle could not be deleted."),
    };
  }
}

export async function uploadVehicleDocumentAction(
  _previousState: VehicleDetailActionState,
  formData: FormData,
): Promise<VehicleDetailActionState> {
  const access = await authorizeVehicleDetail();
  if (!access.ok) {
    return { status: "error", message: access.message };
  }

  const vmfCode = getVmfCode(formData);
  if (vmfCode === null) {
    return { status: "error", message: "The selected vehicle is invalid." };
  }

  const category = getText(formData, "category");
  if (!(VEHICLE_DOCUMENT_CATEGORIES as readonly string[]).includes(category)) {
    return { status: "error", message: "Choose a valid document category." };
  }

  const file = formData.get("file");
  if (!(file instanceof File) || file.size === 0) {
    return { status: "error", message: "Choose a document before uploading." };
  }

  if (file.size > MAX_DOCUMENT_BYTES) {
    return { status: "error", message: "Documents must be 20 MB or smaller." };
  }

  if (!ALLOWED_DOCUMENT_MIME_TYPES.has(file.type.toLowerCase())) {
    return { status: "error", message: "Only JPEG, PNG, and PDF documents are supported." };
  }

  try {
    await uploadVehicleDocument(vmfCode, formData);
    revalidateVehicle(vmfCode);
    return { status: "success", message: "Document uploaded successfully." };
  } catch (error) {
    console.error(
      "FIS vehicle document upload failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return {
      status: "error",
      message: apiErrorMessage(error, "The vehicle document could not be uploaded."),
    };
  }
}

export async function deleteVehicleDocumentAction(
  _previousState: VehicleDetailActionState,
  formData: FormData,
): Promise<VehicleDetailActionState> {
  const access = await authorizeVehicleDetail();
  if (!access.ok) {
    return { status: "error", message: access.message };
  }

  const vmfCode = getVmfCode(formData);
  const documentId = Number(getText(formData, "documentId"));
  if (vmfCode === null || !Number.isInteger(documentId) || documentId <= 0) {
    return { status: "error", message: "The selected document is invalid." };
  }

  try {
    await deleteVehicleDocument(vmfCode, documentId);
    revalidateVehicle(vmfCode);
    return { status: "success", message: "Document deleted successfully." };
  } catch (error) {
    console.error(
      "FIS vehicle document delete failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return {
      status: "error",
      message: apiErrorMessage(error, "The vehicle document could not be deleted."),
    };
  }
}
