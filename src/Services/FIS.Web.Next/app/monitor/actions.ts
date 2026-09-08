"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  createMonitor,
  getMonitorDrivers,
  MonitorApiError,
  updateMonitor,
} from "@/lib/api-monitor";
import { getSession } from "@/lib/session";

class MonitorValidationError extends Error {}

function text(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function integer(formData: FormData, key: string, label: string): number;
function integer(formData: FormData, key: string, label: string, required: false): number | null;
function integer(formData: FormData, key: string, label: string, required = true): number | null {
  const value = text(formData, key);
  if (!value && !required) return null;
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0)
    throw new MonitorValidationError(`${label} must be a positive whole number.`);
  return parsed;
}

function date(formData: FormData) {
  const value = text(formData, "captureDate");
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value))
    throw new MonitorValidationError("Capture date is invalid.");
  const parsed = new Date(`${value}T00:00:00.000Z`);
  if (Number.isNaN(parsed.getTime())) throw new MonitorValidationError("Capture date is invalid.");
  return parsed.toISOString();
}

function returnPath(formData: FormData, fallback: string) {
  const value = text(formData, "returnPath");
  return value.startsWith("/") && !value.startsWith("//") ? value : fallback;
}

function redirectWithMessage(
  path: string,
  key: "saved" | "updated" | "error",
  message: string,
): never {
  redirect(
    `${path}${path.includes("?") ? "&" : "?"}${new URLSearchParams({ [key]: message }).toString()}`,
  );
}

async function authorizeMonitor() {
  const session = await getSession();
  if (session.status === "unavailable")
    return {
      ok: false as const,
      message: "The sign-in service is temporarily unavailable. Please try again.",
    };
  if (session.status !== "authenticated")
    return {
      ok: false as const,
      message: "Your session has expired. Sign in again before continuing.",
    };
  const allowed = session.roles.some(
    (role) => role.toLocaleLowerCase().replace(/[^a-z0-9]/g, "") === "callcentre",
  );
  return allowed
    ? { ok: true as const }
    : { ok: false as const, message: "You do not have permission to maintain Monitor inquiries." };
}

function apiErrorMessage(error: unknown) {
  if (error instanceof MonitorApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable")
      return "The Monitor service is temporarily unavailable. Please try again.";
    if (error.reason === "not-found") return "The monitor inquiry was not found.";
  }
  return "The Monitor operation failed. Please try again.";
}

export async function saveMonitorAction(formData: FormData) {
  const monitorCode = integer(formData, "monitorCode", "Reference", false);
  const path = returnPath(
    formData,
    monitorCode ? `/monitor/edit?referenceNumber=${monitorCode}` : "/monitor/capture",
  );
  const access = await authorizeMonitor();
  if (!access.ok) redirectWithMessage(path, "error", access.message);

  try {
    const vmfCode = integer(formData, "vmfCode", "Vehicle");
    const inquiryType = text(formData, "inquiryType");
    if (!inquiryType || inquiryType.length > 100)
      throw new MonitorValidationError(
        "Inquiry type is required and must be 100 characters or fewer.",
      );
    const driverCode = integer(formData, "driverCode", "Driver", false);
    let driverName = text(formData, "driverName") || null;
    let driverPersalNo = text(formData, "driverPersalNo") || null;
    let driverSite = integer(formData, "driverSite", "Driver site", false);
    if (driverCode) {
      const driver = (await getMonitorDrivers()).find((item) => item.code === driverCode);
      if (!driver) throw new MonitorValidationError("The selected driver could not be found.");
      driverName = `${driver.firstname ?? ""} ${driver.surname ?? ""}`.trim() || null;
      driverPersalNo = driver.persalNumber;
      driverSite = driver.siteCode;
    }
    if (driverName && driverName.length > 100)
      throw new MonitorValidationError("Driver name must be 100 characters or fewer.");
    if (driverPersalNo && driverPersalNo.length > 50)
      throw new MonitorValidationError("Driver Persal number must be 50 characters or fewer.");
    if (driverSite !== null && driverSite <= 0)
      throw new MonitorValidationError("Driver site must be a positive site code.");
    const input = {
      vmf_code: vmfCode,
      Capture_dat: date(formData),
      Inquiry_type: inquiryType,
      Inquiry_Desc: text(formData, "inquiryDescription") || null,
      Driver_name: driverName,
      Driver_persalno: driverPersalNo,
      Driver_Site: driverSite,
    };
    if (monitorCode) {
      await updateMonitor(monitorCode, input);
      revalidatePath("/monitor");
      revalidatePath(`/monitor/edit?referenceNumber=${monitorCode}`);
      redirectWithMessage(path, "updated", "Monitor inquiry updated.");
    }
    await createMonitor(input);
    revalidatePath("/monitor");
    redirectWithMessage(path, "saved", "Monitor inquiry captured.");
  } catch (error) {
    redirectWithMessage(
      path,
      "error",
      error instanceof MonitorValidationError ? error.message : apiErrorMessage(error),
    );
  }
}
