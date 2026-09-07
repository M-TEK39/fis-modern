"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { hasAssetVerificationAccess } from "@/app/vehicle-verification/access";
import { createAssetVerification, updateAssetVerification, type AssetVerificationInput } from "@/lib/api-asset-verification";
import { getSession } from "@/lib/session";

class AssetVerificationValidationError extends Error {}

function text(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function requiredText(formData: FormData, key: string, label: string, maxLength: number) {
  const value = text(formData, key);
  if (!value) throw new AssetVerificationValidationError(`${label} is required.`);
  if (value.length > maxLength) throw new AssetVerificationValidationError(`${label} cannot exceed ${maxLength} characters.`);
  return value;
}

function optionalText(formData: FormData, key: string, maxLength: number) {
  const value = text(formData, key);
  if (value.length > maxLength) throw new AssetVerificationValidationError(`${key} cannot exceed ${maxLength} characters.`);
  return value || null;
}

function requiredInteger(formData: FormData, key: string, label: string) {
  const value = text(formData, key);
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0) throw new AssetVerificationValidationError(`${label} must be a positive whole number.`);
  return parsed;
}

function optionalInteger(formData: FormData, key: string) {
  const value = text(formData, key);
  if (!value) return null;
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed < 0) throw new AssetVerificationValidationError(`${key} must be a whole number.`);
  return parsed;
}

function requiredDate(formData: FormData, key: string, label: string) {
  const value = text(formData, key);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) throw new AssetVerificationValidationError(`${label} is required.`);
  return value;
}

function selectedValue(formData: FormData, key: string, label: string) {
  const value = requiredText(formData, key, label, 100);
  if (value.toLocaleLowerCase() === "select...") throw new AssetVerificationValidationError(`${label} must be selected.`);
  return value;
}

function resultPath(mode: string, gg: string, result: string, message?: string) {
  const params = new URLSearchParams({ gg, result });
  if (message) params.set("message", message);
  return `/vehicle-verification/${mode}/details?${params.toString()}`;
}

export async function saveAssetVerificationAction(formData: FormData) {
  const mode = text(formData, "mode");
  const gg = text(formData, "gg");
  const failurePath = mode === "edit" ? "edit" : "add";
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated") redirect(resultPath(failurePath, gg, "unavailable"));
  if (!hasAssetVerificationAccess(session.roles, session.accessLevel)) redirect(resultPath(failurePath, gg, "forbidden"));

  let saved: Awaited<ReturnType<typeof createAssetVerification>> = null;
  try {
    const code = optionalInteger(formData, "assetVerificationCode");
    if (mode === "edit" && !code) throw new AssetVerificationValidationError("The asset verification record is missing.");
    if (mode !== "add" && mode !== "edit") throw new AssetVerificationValidationError("The asset verification action is invalid.");

    const vmfCode = requiredInteger(formData, "vmfCode", "Vehicle VMF code");
    const siteCode = requiredInteger(formData, "siteCode", "Site");
    const province = selectedValue(formData, "province", "Province");
    const responsibleManager = requiredText(formData, "responsibleManager", "Responsible manager", 100);
    const telNo = requiredText(formData, "telNo", "Telephone number", 50);
    const faxNo = requiredText(formData, "faxNo", "Fax number", 50);
    const yesNoFields = [
      ["mobitrackFitted", "Mobitrack fitted"], ["petrolCard", "Petrol card"], ["lamination", "Lamination"],
      ["tyreBands", "Tyre bands"], ["barcode", "Barcode"], ["logbook", "Logbook"], ["gearlock", "Gearlock"],
      ["radio", "Radio"], ["carKeys", "Car keys"],
    ] as const;
    const values = Object.fromEntries(yesNoFields.map(([key, label]) => [key, selectedValue(formData, key, label)])) as Record<string, string>;
    const currentKm = requiredInteger(formData, "currentKm", "Current km");
    const lastVerified = requiredDate(formData, "lastVerified", "Last verified date");
    const comments = requiredText(formData, "comments", "Comments", 2000);

    const input: AssetVerificationInput = {
      assetVerificationCode: code ?? undefined,
      province,
      departmentName: optionalText(formData, "departmentName", 200),
      siteCode,
      siteName: optionalText(formData, "siteName", 200),
      responsibleManager,
      telNo,
      faxNo,
      vehicleRegNo: optionalText(formData, "vehicleRegNo", 50),
      vmfCode,
      verificationDate: lastVerified,
      verifiedBy: null,
      notes: comments,
      vehicleMake: optionalText(formData, "vehicleMake", 200),
      vehicleModel: optionalText(formData, "vehicleModel", 200),
      vehicleColour: optionalText(formData, "vehicleColour", 100),
      mobitrackFitted: values.mobitrackFitted,
      petrolCard: values.petrolCard,
      lamination: values.lamination,
      tyreBands: values.tyreBands,
      barcode: values.barcode,
      logbook: values.logbook,
      gearlock: values.gearlock,
      radio: values.radio,
      carKeys: values.carKeys,
      licenceExpiryDate: optionalText(formData, "licenceExpiryDate", 30),
      barcodeNumber: optionalText(formData, "barcodeNumber", 100),
      vehicleEngineNumber: optionalText(formData, "vehicleEngineNumber", 100),
      vehicleChassisNumber: optionalText(formData, "vehicleChassisNumber", 100),
      currentKm,
      dateLastVerified: lastVerified,
      comments,
    };

    saved = mode === "edit" ? await updateAssetVerification(code!, input) : await createAssetVerification(input);
    if (!saved) throw new Error("The API did not return the saved asset verification record.");
  } catch (error) {
    if (error instanceof AssetVerificationValidationError) redirect(resultPath(failurePath, gg, "invalid", error.message));
    console.error("FIS asset verification save failed", error instanceof Error ? error.message : "unknown error");
    redirect(resultPath(failurePath, gg, "unavailable"));
  }

  revalidatePath("/vehicle-verification");
  redirect(`/vehicle-verification?result=success&mode=${mode}`);
}
