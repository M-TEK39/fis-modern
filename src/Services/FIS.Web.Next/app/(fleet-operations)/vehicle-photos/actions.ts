"use server";

import { revalidatePath } from "next/cache";

import {
  createVehiclePhoto,
  deleteVehiclePhoto,
  updateVehiclePhoto,
  uploadVehiclePhoto,
  VehiclePhotoApiError,
} from "@/lib/api/vehicles/api-vehicle-photos";
import { hasVehicleManagementPermission } from "@/app/(administration)/drivers/access";
import { getSession } from "@/lib/auth/session";

const MAX_PHOTO_BYTES = 20 * 1024 * 1024;
const MAX_FILE_URL_LENGTH = 500;
const MAX_DESCRIPTION_LENGTH = 200;
const ALLOWED_IMAGE_MIME_TYPES = new Set([
  "image/jpeg",
  "image/png",
  "image/gif",
  "image/webp",
  "image/heic",
  "image/heif",
]);

export type VehiclePhotoActionState = {
  status: "idle" | "success" | "error";
  message?: string;
};

async function authorize() {
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
  if (!hasVehicleManagementPermission(session.accessLevel)) {
    return {
      ok: false as const,
      message: "You do not have permission to maintain vehicle photos.",
    };
  }
  return { ok: true as const };
}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getPositiveInteger(formData: FormData, key: string) {
  const value = Number(getText(formData, key));
  return Number.isSafeInteger(value) && value > 0 ? value : null;
}

function getOrientation(formData: FormData) {
  const orientation = Number(getText(formData, "orientation"));
  return Number.isInteger(orientation) && orientation >= 1 && orientation <= 6 ? orientation : null;
}

function getErrorMessage(error: unknown, fallback: string) {
  if (!(error instanceof VehiclePhotoApiError)) return fallback;
  if (error.reason === "unauthorized")
    return "Your session has expired. Sign in again before continuing.";
  if (error.reason === "not-found") return "The selected vehicle or photo could not be found.";
  if (error.reason === "unavailable")
    return "The vehicle photo service is temporarily unavailable. Please try again.";
  return error.message || fallback;
}

function revalidatePhotoRoutes(vmfCode: number) {
  revalidatePath("/vehicle-photos");
  revalidatePath(`/vehicle-photos/manage/${vmfCode}`);
}

export async function uploadVehiclePhotoAction(
  _previousState: VehiclePhotoActionState,
  formData: FormData,
): Promise<VehiclePhotoActionState> {
  const access = await authorize();
  if (!access.ok) return { status: "error", message: access.message };

  const vmfCode = getPositiveInteger(formData, "vmfCode");
  const orientation = getOrientation(formData);
  const description = getText(formData, "description");
  const file = formData.get("file");
  if (vmfCode === null) return { status: "error", message: "The selected vehicle is invalid." };
  if (orientation === null) return { status: "error", message: "Choose a photo orientation." };
  if (description.length > MAX_DESCRIPTION_LENGTH)
    return { status: "error", message: "Description cannot exceed 200 characters." };
  if (!(file instanceof File) || file.size === 0)
    return { status: "error", message: "Choose an image before uploading." };
  if (file.size > MAX_PHOTO_BYTES)
    return { status: "error", message: "Images must be 20 MB or smaller." };
  if (!ALLOWED_IMAGE_MIME_TYPES.has(file.type.toLowerCase()))
    return {
      status: "error",
      message: "Only JPEG, PNG, GIF, WebP, HEIC, and HEIF images are supported.",
    };

  const upload = new FormData();
  upload.set("vmfCode", String(vmfCode));
  upload.set("orientation", String(orientation));
  if (description) upload.set("description", description);
  upload.set("file", file, file.name || "vehicle-photo.jpg");

  try {
    await uploadVehiclePhoto(vmfCode, upload);
    revalidatePhotoRoutes(vmfCode);
    return { status: "success", message: "Vehicle photo uploaded successfully." };
  } catch (error) {
    console.error(
      "FIS vehicle photo upload failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return {
      status: "error",
      message: getErrorMessage(error, "The vehicle photo could not be uploaded."),
    };
  }
}

export async function saveVehiclePhotoReferenceAction(
  _previousState: VehiclePhotoActionState,
  formData: FormData,
): Promise<VehiclePhotoActionState> {
  const access = await authorize();
  if (!access.ok) return { status: "error", message: access.message };

  const vmfCode = getPositiveInteger(formData, "vmfCode");
  const photoIdText = getText(formData, "photoId");
  const photoId = photoIdText ? Number(photoIdText) : 0;
  const orientation = getOrientation(formData);
  const fileUrl = getText(formData, "fileUrl");
  const description = getText(formData, "description");
  if (vmfCode === null) return { status: "error", message: "The selected vehicle is invalid." };
  if (photoIdText && (!Number.isSafeInteger(photoId) || photoId <= 0))
    return { status: "error", message: "The selected photo is invalid." };
  if (orientation === null) return { status: "error", message: "Choose a photo orientation." };
  if (!fileUrl) return { status: "error", message: "Photo URL or stored image path is required." };
  if (fileUrl.length > MAX_FILE_URL_LENGTH)
    return { status: "error", message: "Photo URL or path cannot exceed 500 characters." };
  if (description.length > MAX_DESCRIPTION_LENGTH)
    return { status: "error", message: "Description cannot exceed 200 characters." };

  const payload = {
    VehiclePhotoInfoCode: photoId,
    VehicleMasterCode: vmfCode,
    FileUrl: fileUrl,
    Orientation: orientation,
    Description: description || null,
  };

  try {
    if (photoId > 0) await updateVehiclePhoto(photoId, payload);
    else await createVehiclePhoto(payload);
    revalidatePhotoRoutes(vmfCode);
    return {
      status: "success",
      message:
        photoId > 0
          ? "Photo reference updated successfully."
          : "Photo reference saved successfully.",
    };
  } catch (error) {
    console.error(
      "FIS vehicle photo reference save failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return {
      status: "error",
      message: getErrorMessage(error, "The photo reference could not be saved."),
    };
  }
}

export async function deleteVehiclePhotoAction(
  _previousState: VehiclePhotoActionState,
  formData: FormData,
): Promise<VehiclePhotoActionState> {
  const access = await authorize();
  if (!access.ok) return { status: "error", message: access.message };

  const vmfCode = getPositiveInteger(formData, "vmfCode");
  const photoId = getPositiveInteger(formData, "photoId");
  if (vmfCode === null || photoId === null)
    return { status: "error", message: "The selected photo is invalid." };

  try {
    await deleteVehiclePhoto(photoId);
    revalidatePhotoRoutes(vmfCode);
    return { status: "success", message: "Vehicle photo deleted successfully." };
  } catch (error) {
    console.error(
      "FIS vehicle photo delete failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return {
      status: "error",
      message: getErrorMessage(error, "The vehicle photo could not be deleted."),
    };
  }
}
