"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  AuctionApiError,
  deleteAuctionAgainstApi,
  updateAuctionMaintenanceAgainstApi,
  type AuctionMaintenanceRequest,
} from "@/lib/api-auction";
import { getSession } from "@/lib/session";

const REPORTS_ROLE = "Reports";

class AuctionValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getReturnPath(formData: FormData, fallback: string) {
  const value = getText(formData, "returnPath");
  return value.startsWith("/") && !value.startsWith("//") ? value : fallback;
}

function getInteger(formData: FormData, key: string, label: string, required = false) {
  const value = getText(formData, key);
  if (!value && !required) {
    return null;
  }

  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed < 0) {
    throw new AuctionValidationError(`${label} must be a non-negative whole number.`);
  }

  return parsed;
}

function getRequiredInteger(formData: FormData, key: string, label: string) {
  const value = getInteger(formData, key, label, true);
  if (value === null) {
    throw new AuctionValidationError(`${label} is required.`);
  }

  return value;
}

function getOptionalDate(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  if (!value) {
    return null;
  }

  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    throw new AuctionValidationError(`${label} is invalid.`);
  }

  const [year, month, day] = value.split("-").map(Number);
  const parsed = new Date(Date.UTC(year, month - 1, day));
  if (
    parsed.getUTCFullYear() !== year ||
    parsed.getUTCMonth() !== month - 1 ||
    parsed.getUTCDate() !== day
  ) {
    throw new AuctionValidationError(`${label} is invalid.`);
  }

  return `${value}T00:00:00.000Z`;
}

function getOptionalText(formData: FormData, key: string, label: string, maxLength: number) {
  const value = getText(formData, key);
  if (value.length > maxLength) {
    throw new AuctionValidationError(`${label} must be ${maxLength} characters or fewer.`);
  }

  return value || null;
}

function hasReportsRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(REPORTS_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

async function authorizeReports() {
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

  if (!hasReportsRole(session.roles)) {
    return {
      ok: false as const,
      message: "You do not have permission to maintain Auction records.",
    };
  }

  return { ok: true as const };
}

function apiErrorMessage(error: unknown, operation: string) {
  if (error instanceof AuctionApiError) {
    if (error.reason === "unauthorized") {
      return "Your session has expired. Sign in again before continuing.";
    }

    if (error.reason === "unavailable") {
      return `The Auction ${operation} service is temporarily unavailable. Please try again.`;
    }

    if (error.reason === "not-found") {
      return "The auction record was not found. Return to the Auction menu and try again.";
    }
  }

  return `The auction record could not be ${operation}. Please try again.`;
}

function buildAuctionRequest(formData: FormData): AuctionMaintenanceRequest {
  const auctionCode = getRequiredInteger(formData, "auctionCode", "Auction record");
  const vmfCode = getRequiredInteger(formData, "vmfCode", "Vehicle");
  const auctionNumber = getText(formData, "auctionNumber");
  const camp = getText(formData, "camp");
  const garageOwner = getText(formData, "garageOwner");
  const reasonSold = getText(formData, "reasonSold");

  if (!auctionNumber || auctionNumber.length > 7) {
    throw new AuctionValidationError(
      "Auction number is required and must be 7 characters or fewer.",
    );
  }

  if (camp !== "Camp1" && camp !== "Camp2") {
    throw new AuctionValidationError("Select a valid auction camp.");
  }

  if (garageOwner !== "Jhb" && garageOwner !== "Pta") {
    throw new AuctionValidationError("Select a valid garage owner.");
  }

  const allowedReasons = new Set([
    "Old & Obsolete",
    "Obsolete",
    "Old & Uneconomical",
    "Uneconomical",
    "Stolen & Recovered",
    "Accident",
  ]);
  if (!allowedReasons.has(reasonSold)) {
    throw new AuctionValidationError("Select a valid sold reason.");
  }

  const lot = getRequiredInteger(formData, "lot", "Lot number");
  const auctionKm = getRequiredInteger(formData, "auctionKm", "Auction km");
  const estimateAmount = getRequiredInteger(formData, "estimateAmount", "Estimate price");
  const reserveAmount = getRequiredInteger(formData, "reserveAmount", "Reserve price");
  const soldAmount = getRequiredInteger(formData, "soldAmount", "Sold price");

  return {
    auction_code: auctionCode,
    vmf_code: vmfCode,
    auction_number: auctionNumber,
    camp,
    lot,
    auction_garage: 1,
    auth_number: getOptionalText(formData, "authNumber", "Auth number", 10),
    auth_date: getOptionalDate(formData, "authDate", "Auth date"),
    auction_km: auctionKm,
    garage_owner: garageOwner,
    reason_sold: reasonSold,
    estimate_amount: estimateAmount,
    reserve_amount: reserveAmount,
    sold_id: getOptionalText(formData, "soldId", "Sold ID", 13),
    remark: getOptionalText(formData, "remark", "Remarks", 30),
    barcode: getOptionalText(formData, "barcode", "Barcode", 15),
    sold_to: getOptionalText(formData, "soldTo", "Sold to name", 30),
    sold_date: getOptionalDate(formData, "soldDate", "Sold date"),
    sold_amount: soldAmount,
  };
}

function redirectError(returnPath: string, message: string) {
  redirect(
    `${returnPath}${returnPath.includes("?") ? "&" : "?"}error=${encodeURIComponent(message)}`,
  );
}

export async function saveAuctionMaintenanceAction(formData: FormData) {
  const returnPath = getReturnPath(formData, "/auction/maintenance");
  const access = await authorizeReports();
  if (!access.ok) {
    redirectError(returnPath, access.message);
  }

  let request: AuctionMaintenanceRequest;
  try {
    request = buildAuctionRequest(formData);
    await updateAuctionMaintenanceAgainstApi(request.auction_code, request);
  } catch (error) {
    if (error instanceof AuctionValidationError) {
      redirectError(returnPath, error.message);
    }

    redirectError(returnPath, apiErrorMessage(error, "updated"));
  }

  revalidatePath("/auction");
  revalidatePath("/auction/maintenance");
  revalidatePath("/auction/delete-vehicle");
  redirect(`/auction/maintenance/detail?auctionId=${request!.auction_code}&updated=1`);
}

export async function deleteAuctionAction(formData: FormData) {
  const returnPath = getReturnPath(formData, "/auction/delete-vehicle");
  const access = await authorizeReports();
  if (!access.ok) {
    redirectError(returnPath, access.message);
  }

  const auctionCode = Number(getText(formData, "auctionCode"));
  if (!Number.isInteger(auctionCode) || auctionCode <= 0) {
    redirectError(returnPath, "The auction record is invalid.");
  }

  try {
    await deleteAuctionAgainstApi(auctionCode);
  } catch (error) {
    redirectError(returnPath, apiErrorMessage(error, "deleted"));
  }

  revalidatePath("/auction");
  revalidatePath("/auction/delete-vehicle");
  redirect("/auction/delete-vehicle?deleted=1");
}
