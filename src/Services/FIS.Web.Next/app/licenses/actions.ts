"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { captureLicenseVehicle, LicenseApiError, submitLicensePassword } from "@/lib/api-licenses";
import { hasRole } from "@/app/drivers/access";
import { getSession } from "@/lib/session";

class LicenseValidationError extends Error {}

function text(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function date(formData: FormData, key: string, label: string, required = false) {
  const value = text(formData, key);
  if (!value && !required) return null;
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value) || Number.isNaN(new Date(`${value}T00:00:00.000Z`).getTime())) throw new LicenseValidationError(`${label} is invalid.`);
  return value;
}

function positiveInteger(formData: FormData, key: string, label: string) {
  const value = text(formData, key);
  const parsed = Number(value);
  if (!value || !Number.isSafeInteger(parsed) || parsed <= 0) throw new LicenseValidationError(`${label} must be a positive whole number.`);
  return parsed;
}

function optionalInteger(formData: FormData, key: string, label: string) {
  const value = text(formData, key);
  if (!value) return null;
  const parsed = Number(value);
  if (!Number.isSafeInteger(parsed) || parsed < 0) throw new LicenseValidationError(`${label} must be a whole number.`);
  return parsed;
}

function boundedText(formData: FormData, key: string, label: string, maxLength: number) {
  const value = text(formData, key);
  if (value.length > maxLength) throw new LicenseValidationError(`${label} must be ${maxLength} characters or fewer.`);
  return value || null;
}

function redirectWith(path: string, key: "saved" | "error", message: string): never {
  redirect(`${path}${path.includes("?") ? "&" : "?"}${new URLSearchParams({ [key]: message }).toString()}`);
}

function errorMessage(error: unknown) {
  if (error instanceof LicenseValidationError) return error.message;
  if (error instanceof LicenseApiError) {
    if (error.reason === "unauthorized") return "Your session has expired or the licence password was rejected.";
    if (error.reason === "not-found") return "The vehicle was not found.";
    if (error.reason === "unavailable") return "The Licence service is temporarily unavailable. Please try again.";
  }
  return "The licence operation failed. Please try again.";
}

function workflowPath(formData: FormData) {
  switch (text(formData, "workflow")) {
    case "multi-collection": return "/licenses/multi-collection";
    case "garage": return "/licenses/garage";
    default: return "/licenses/one-vehicle";
  }
}

export async function saveLicenseVehicleAction(formData: FormData) {
  const mode = text(formData, "numberType").toUpperCase() === "GP" ? "GP" : "GG";
  const number = text(formData, "number");
  const path = workflowPath(formData);
  const session = await getSession();
  if (session.status !== "authenticated") redirectWith(path, "error", "Your session has expired. Sign in again before continuing.");
  if (!hasRole(session.roles, "Licence")) redirectWith(path, "error", "You do not have permission to maintain Licences.");

  try {
    const expDate = date(formData, "expDate", "Exp Date", true);
    if (!expDate) throw new LicenseValidationError("Exp Date is required.");
    const expiry = new Date(`${expDate}T00:00:00.000Z`);
    if (expiry < new Date("2004-01-01T00:00:00.000Z")) throw new LicenseValidationError("Exp Date must be after 2004-01-01.");
    const nextDay = new Date(expiry);
    nextDay.setUTCDate(nextDay.getUTCDate() + 1);
    if (nextDay.getUTCMonth() === expiry.getUTCMonth()) throw new LicenseValidationError("Exp Date must be the last day of the month.");

    const dateCollected = date(formData, "dateCollected", "Date Collected", true);
    if (!dateCollected) throw new LicenseValidationError("Date Collected is required.");
    const collected = new Date(`${dateCollected}T00:00:00.000Z`);
    if (collected < new Date("2003-01-01T00:00:00.000Z")) throw new LicenseValidationError("Date Collected must be after 2003-01-01.");

    const cofRequired = boundedText(formData, "cofRequired", "Cof Required", 1);
    const cofExpDate = date(formData, "cofExpDate", "Cof Exp Date", cofRequired?.toUpperCase() !== "N");
    if (cofRequired?.toUpperCase() !== "N" && !cofExpDate) throw new LicenseValidationError("Cof Exp Date is required when Cof Required is not N.");
    const dayDifference = Math.abs((collected.getTime() - expiry.getTime()) / 86_400_000);
    if (dayDifference > 500) throw new LicenseValidationError("Date Collected and Exp Date cannot differ by more than 500 days.");

    await submitLicensePassword(text(formData, "password"));
    await captureLicenseVehicle({
      vmfCode: positiveInteger(formData, "vmfCode", "Vehicle"),
      numberType: mode,
      number,
      expDate,
      registerNumber: boundedText(formData, "registerNumber", "Register number", 50),
      regDoc: boundedText(formData, "regDoc", "Reg Doc", 100),
      tare: optionalInteger(formData, "tare", "Tare")?.toString() ?? null,
      receiver: boundedText(formData, "receiver", "Receiver", 100),
      receiverId: boundedText(formData, "receiverId", "Receiver ID", 50),
      receiverTel: boundedText(formData, "receiverTel", "Receiver telephone", 30),
      receiverSiteCode: optionalInteger(formData, "receiverSiteCode", "Receiver site"),
      dateCollected,
      cofRequired,
      cofExpDate,
      comments: boundedText(formData, "comments", "Comments", 2000),
      updateNotes: boundedText(formData, "updateNotes", "History note", 2000),
    });
    revalidatePath("/licenses");
    revalidatePath(path);
    redirectWith(`${path}?${new URLSearchParams({ mode, number, lookup: "1" }).toString()}`, "saved", "Licence details saved.");
  } catch (error) {
    redirectWith(path, "error", errorMessage(error));
  }
}
