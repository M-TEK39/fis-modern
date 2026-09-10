"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  createLoss,
  deleteLoss,
  getLossVehicleMatches,
  LossApiError,
  updateLoss,
  type LossInput,
} from "@/lib/api/fleet-operations/api-losses";
import { getSession } from "@/lib/auth/session";

class LossValidationError extends Error {}

const LOSS_STATUSES = new Set(["Open", "Under Investigation", "Reported to SAPD", "Closed"]);
const INCIDENT_CHOICES = new Set(["", "Yes", "No"]);

function text(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function returnPath(formData: FormData, fallback: string) {
  const value = text(formData, "returnPath");
  return value.startsWith("/") && !value.startsWith("//") ? value : fallback;
}

function integer(formData: FormData, key: string, label: string, required = false) {
  const value = text(formData, key);
  if (!value && !required) return null;
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0) {
    throw new LossValidationError(`${label} must be a positive whole number.`);
  }
  return parsed;
}

function decimal(formData: FormData, key: string, label: string) {
  const value = text(formData, key);
  if (!value) return null;
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 0) {
    throw new LossValidationError(`${label} must be a non-negative number.`);
  }
  return parsed;
}

function date(formData: FormData, key: string, label: string, required = false) {
  const value = text(formData, key);
  if (!value && !required) return null;
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    throw new LossValidationError(`${label} is invalid.`);
  }
  const [year, month, day] = value.split("-").map(Number);
  const parsed = new Date(Date.UTC(year, month - 1, day));
  if (
    parsed.getUTCFullYear() !== year ||
    parsed.getUTCMonth() !== month - 1 ||
    parsed.getUTCDate() !== day
  ) {
    throw new LossValidationError(`${label} is invalid.`);
  }
  return `${value}T00:00:00.000Z`;
}

function boundedText(formData: FormData, key: string, label: string, maxLength: number) {
  const value = text(formData, key);
  if (value.length > maxLength) {
    throw new LossValidationError(`${label} must be ${maxLength} characters or fewer.`);
  }
  return value || null;
}

function choice(formData: FormData, key: string, label: string) {
  const value = text(formData, key);
  if (!INCIDENT_CHOICES.has(value)) {
    throw new LossValidationError(`${label} must be Yes or No.`);
  }
  return value === "Yes";
}

async function authorized() {
  const session = await getSession();
  if (session.status !== "authenticated") {
    return {
      ok: false as const,
      message:
        session.status === "unavailable"
          ? "The sign-in service is temporarily unavailable."
          : "Your session has expired. Sign in again.",
    };
  }
  if (
    !session.roles.some(
      (role) => role.localeCompare("Losses", undefined, { sensitivity: "accent" }) === 0,
    )
  ) {
    return {
      ok: false as const,
      message: "You do not have permission to maintain Losses records.",
    };
  }
  return { ok: true as const };
}

async function resolveVehicleCode(formData: FormData) {
  const directCode = integer(formData, "vmfCode", "Vehicle", false);
  if (directCode !== null) return directCode;

  const identifier = text(formData, "vehicleIdentifier");
  if (!identifier) {
    throw new LossValidationError("Enter a GG number, GP number, or VMF vehicle code.");
  }

  const mode = text(formData, "vehicleSearchMode").toUpperCase();
  if (mode !== "GG" && mode !== "GP") {
    throw new LossValidationError("Select GG or GP vehicle search.");
  }

  const matches = await getLossVehicleMatches(identifier);
  const exact = matches.filter((vehicle) => {
    const value = mode === "GG" ? vehicle.fleetNumber : vehicle.registrationNumber;
    return value?.trim().localeCompare(identifier, undefined, { sensitivity: "accent" }) === 0;
  });
  const selected = exact.length === 1 ? exact[0] : matches.length === 1 ? matches[0] : null;
  if (!selected) {
    throw new LossValidationError(
      "The vehicle identifier did not resolve to one vehicle. Enter the exact GG or GP number, or provide its VMF code.",
    );
  }
  return selected.vmfCode;
}

async function buildInput(formData: FormData): Promise<LossInput> {
  const lossReference = boundedText(formData, "lossReference", "Loss reference", 30);
  if (!lossReference) throw new LossValidationError("Loss reference is required.");

  const lossStatus = text(formData, "lossStatus") || "Open";
  if (!LOSS_STATUSES.has(lossStatus)) throw new LossValidationError("Select a valid loss status.");

  const callReference = integer(formData, "callReference", "Call reference", false);
  return {
    vmfCode: await resolveVehicleCode(formData),
    lossDate: date(formData, "lossDate", "Loss date", true),
    lossReference,
    lossTypeCode: integer(formData, "lossTypeCode", "Loss type", false),
    siteCode: integer(formData, "siteCode", "Site", false),
    departmentContact: boundedText(formData, "departmentContact", "Departmental contact", 20),
    lossAmount: decimal(formData, "lossAmount", "Loss amount"),
    departmentClaim: decimal(formData, "departmentClaim", "Department claim"),
    sapd: boundedText(formData, "sapd", "SAPD office", 20),
    inspector: boundedText(formData, "inspector", "Inspector", 20),
    caseNumber: boundedText(formData, "caseNumber", "Case number", 20),
    cancelled: choice(formData, "cancelled", "Cancelled"),
    coverForfeit: choice(formData, "coverForfeit", "Cover forfeit"),
    prosecute: choice(formData, "prosecute", "Prosecute"),
    compensationOrder: choice(formData, "compensationOrder", "Compensation order"),
    remarks: boundedText(formData, "remarks", "Remarks", 30),
    hqReference: boundedText(formData, "hqReference", "HQ reference", 20),
    placeOfLoss: boundedText(formData, "placeOfLoss", "Place of loss", 30),
    garagingAuthority: choice(formData, "garagingAuthority", "Garaging authority"),
    driverName: boundedText(formData, "driverName", "Driver name", 20),
    reportFromDepartment: choice(formData, "reportFromDepartment", "Department report"),
    dateReportedGgmt: date(formData, "dateReportedGgmt", "Date reported at GGMT"),
    dateReportedSapd: date(formData, "dateReportedSapd", "Date reported at SAPD"),
    callReference,
    towNeed: text(formData, "towNeed") || null,
    lossStatus,
  };
}

function apiErrorMessage(error: unknown, operation: string) {
  if (error instanceof LossApiError) {
    if (error.reason === "unauthorized") return "Your session has expired. Sign in again.";
    if (error.reason === "unavailable")
      return `The Losses ${operation} service is temporarily unavailable.`;
    if (error.reason === "not-found") return "The loss record was not found.";
  }
  return `The loss record could not be ${operation}.`;
}

function redirectError(path: string, message: string): never {
  redirect(`${path}${path.includes("?") ? "&" : "?"}error=${encodeURIComponent(message)}`);
}

export async function saveLossAction(formData: FormData) {
  const path = returnPath(formData, "/losses/maintenance");
  const access = await authorized();
  if (!access.ok) redirectError(path, access.message);

  let input: LossInput;
  try {
    input = await buildInput(formData);
  } catch (error) {
    redirectError(
      path,
      error instanceof LossValidationError ? error.message : apiErrorMessage(error, "save"),
    );
  }

  const lossCode = integer(formData, "lossCode", "Loss record", false);
  let savedCode = lossCode;
  try {
    if (lossCode === null) {
      const created = await createLoss(input!);
      savedCode = created?.lossCode ?? null;
    } else {
      await updateLoss(lossCode, input!);
    }
  } catch (error) {
    redirectError(path, apiErrorMessage(error, lossCode === null ? "created" : "updated"));
  }

  revalidatePath("/losses");
  revalidatePath("/losses/maintenance");
  revalidatePath("/losses/edit");
  redirect(
    lossCode === null
      ? `/losses/maintenance?saved=created${savedCode ? `&lossCode=${savedCode}` : ""}`
      : `/losses/edit?lossCode=${lossCode}&saved=updated`,
  );
}

export async function deleteLossAction(formData: FormData) {
  const path = returnPath(formData, "/losses/maintenance");
  const access = await authorized();
  if (!access.ok) redirectError(path, access.message);

  let lossCode: number;
  try {
    const parsed = integer(formData, "lossCode", "Loss record", true);
    if (parsed === null) throw new LossValidationError("The loss record is invalid.");
    lossCode = parsed;
  } catch (error) {
    redirectError(
      path,
      error instanceof LossValidationError ? error.message : "The loss record is invalid.",
    );
  }

  try {
    await deleteLoss(lossCode!);
  } catch (error) {
    redirectError(path, apiErrorMessage(error, "deleted"));
  }

  revalidatePath("/losses");
  revalidatePath("/losses/maintenance");
  redirect(`${path}?saved=deleted`);
}
