"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  closeContractAgainstApi,
  ContractApiError,
  editContractAgainstApi,
  extendContractAgainstApi,
  hireContractAgainstApi,
  postContractAction,
  updateContractHistoryAgainstApi,
  type CloseContractRequest,
  type EditContractRequest,
  type HireContractRequest,
} from "@/lib/api-contracts";
import { getSession } from "@/lib/session";

const CONTRACT_PERMISSION = BigInt(2);

class ContractValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getReturnPath(formData: FormData, fallback: string) {
  const value = getText(formData, "returnPath");
  return value.startsWith("/contracts") && !value.startsWith("//") ? value : fallback;
}

function getInteger(formData: FormData, key: string, label: string, required = false) {
  const value = getText(formData, key);
  if (!value && !required) return null;

  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed < 0) {
    throw new ContractValidationError(`${label} must be a non-negative whole number.`);
  }
  return parsed;
}

function getRequiredInteger(formData: FormData, key: string, label: string) {
  const value = getInteger(formData, key, label, true);
  if (value === null) throw new ContractValidationError(`${label} is required.`);
  return value;
}

function getOptionalDate(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  if (!value) return null;
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) throw new ContractValidationError(`${label} is invalid.`);

  const [year, month, day] = value.split("-").map(Number);
  const parsed = new Date(Date.UTC(year, month - 1, day));
  if (parsed.getUTCFullYear() !== year || parsed.getUTCMonth() !== month - 1 || parsed.getUTCDate() !== day) {
    throw new ContractValidationError(`${label} is invalid.`);
  }
  return `${value}T00:00:00.000Z`;
}

function getRequiredDate(formData: FormData, key: string, label: string) {
  const value = getOptionalDate(formData, key, label);
  if (!value) throw new ContractValidationError(`${label} is required.`);
  return value;
}

function getOptionalText(formData: FormData, key: string, label: string, maxLength: number) {
  const value = getText(formData, key);
  if (value.length > maxLength) throw new ContractValidationError(`${label} must be ${maxLength} characters or fewer.`);
  return value || null;
}

function getBoolean(formData: FormData, key: string) {
  return getText(formData, key).toLowerCase() === "true";
}

function hasContractAccess(accessLevel: string | undefined, roles: readonly string[]) {
  if (roles.some((role) => ["contracts", "contract", "admin", "administrator"].includes(role.trim().toLowerCase()))) {
    return true;
  }

  try {
    return accessLevel ? (BigInt(accessLevel) & CONTRACT_PERMISSION) === CONTRACT_PERMISSION : false;
  } catch {
    return false;
  }
}

function hasContractApproverRole(roles: readonly string[]) {
  return roles.some((role) => ["contracts approver", "contracts_approver", "back dating contract (approver)", "admin", "administrator"].includes(role.trim().toLowerCase()));
}

function hasContractHistoryBackdatingRole(roles: readonly string[]) {
  return roles.some((role) => ["contract history back dating", "contract_history_backdating", "admin", "administrator"].includes(role.trim().toLowerCase()));
}

async function authorizeContract() {
  const session = await getSession();
  if (session.status === "unavailable") {
    return { ok: false as const, message: "The sign-in service is temporarily unavailable. Please try again." };
  }
  if (session.status !== "authenticated") {
    return { ok: false as const, message: "Your session has expired. Sign in again before continuing." };
  }
  if (!hasContractAccess(session.accessLevel, session.roles)) {
    return { ok: false as const, message: "You do not have permission to maintain vehicle contracts." };
  }
  return { ok: true as const };
}

async function authorizeContractRole(roleCheck: (roles: readonly string[]) => boolean, message: string) {
  const access = await authorizeContract();
  if (!access.ok) return access;

  const session = await getSession();
  if (session.status !== "authenticated" || !roleCheck(session.roles)) {
    return { ok: false as const, message };
  }

  return { ok: true as const };
}

function apiErrorMessage(error: unknown, operation: string) {
  if (error instanceof ContractApiError) {
    if (error.reason === "unauthorized") return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable") return `The contract ${operation} service is temporarily unavailable. Please try again.`;
    if (error.reason === "not-found") return "The contract record was not found. Return to the Contracts menu and try again.";
    return error.message;
  }
  return `The contract could not be ${operation}. Please try again.`;
}

function redirectError(returnPath: string, message: string) {
  redirect(`${returnPath}${returnPath.includes("?") ? "&" : "?"}error=${encodeURIComponent(message)}`);
}

function buildHireRequest(formData: FormData): HireContractRequest {
  const startOdometer = getInteger(formData, "startOdometer", "Start odometer");
  return {
    VmfCode: getRequiredInteger(formData, "vmfCode", "Vehicle"),
    SiteCode: getRequiredInteger(formData, "siteCode", "Site"),
    StartOdometer: startOdometer,
    DriverId: getOptionalText(formData, "driverId", "Driver ID", 60),
    SiteDriverCode: getInteger(formData, "siteDriverCode", "Site driver"),
    UserCode: getInteger(formData, "userCode", "User code"),
    Authorisation: getOptionalText(formData, "authorisation", "Authorisation", 60),
    Notes: getOptionalText(formData, "notes", "Notes", 1000),
    TargetReturnDate: getOptionalDate(formData, "targetReturnDate", "Target return date"),
  };
}

function buildEditRequest(formData: FormData): EditContractRequest {
  return {
    SiteCode: getInteger(formData, "siteCode", "Site"),
    DriverId: getOptionalText(formData, "driverId", "Driver ID", 60),
    SiteDriverCode: getInteger(formData, "siteDriverCode", "Site driver"),
    UserCode: getInteger(formData, "userCode", "User code"),
    Authorisation: getOptionalText(formData, "authorisation", "Authorisation", 60),
    Notes: getOptionalText(formData, "notes", "Notes", 1000),
    TargetReturnDate: getOptionalDate(formData, "targetReturnDate", "Target return date"),
    StartOdometer: getInteger(formData, "startOdometer", "Start odometer"),
  };
}

function getContractId(formData: FormData) {
  return getRequiredInteger(formData, "contractId", "Contract");
}

export async function hireContractAction(formData: FormData) {
  const returnPath = getReturnPath(formData, "/contracts/maintenance");
  const access = await authorizeContract();
  if (!access.ok) redirectError(returnPath, access.message);

  try {
    await hireContractAgainstApi(buildHireRequest(formData));
  } catch (error) {
    redirectError(returnPath, error instanceof ContractValidationError ? error.message : apiErrorMessage(error, "captured"));
  }

  revalidatePath("/contracts");
  revalidatePath("/contracts/maintenance");
  redirect(`${returnPath}?saved=1`);
}

export async function editContractAction(formData: FormData) {
  const contractId = getContractId(formData);
  const returnPath = getReturnPath(formData, `/contracts/detail?contractId=${contractId}`);
  const access = await authorizeContract();
  if (!access.ok) redirectError(returnPath, access.message);

  try {
    await editContractAgainstApi(contractId, buildEditRequest(formData));
  } catch (error) {
    redirectError(returnPath, error instanceof ContractValidationError ? error.message : apiErrorMessage(error, "updated"));
  }

  revalidatePath("/contracts");
  revalidatePath("/contracts/maintenance");
  revalidatePath("/contracts/detail");
  redirect(`${returnPath}${returnPath.includes("?") ? "&" : "?"}updated=1`);
}

export async function updateContractHistoryAction(formData: FormData) {
  const contractId = getContractId(formData);
  const returnPath = getReturnPath(formData, `/contracts/backdating-history?contractId=${contractId}`);
  const access = await authorizeContractRole(
    hasContractHistoryBackdatingRole,
    "You do not have permission to backdate contract history.",
  );
  if (!access.ok) redirectError(returnPath, access.message);

  try {
    await updateContractHistoryAgainstApi(contractId, {
      StartDate: getRequiredDate(formData, "startDate", "Start date"),
      EndDate: getOptionalDate(formData, "endDate", "End date"),
      StartOdometer: getInteger(formData, "startOdometer", "Start odometer"),
      EndOdometer: getInteger(formData, "endOdometer", "End odometer"),
    });
  } catch (error) {
    redirectError(returnPath, error instanceof ContractValidationError ? error.message : apiErrorMessage(error, "updated"));
  }

  revalidatePath("/contracts");
  revalidatePath("/contracts/backdating-history");
  revalidatePath("/contracts/detail");
  redirect(`${returnPath}${returnPath.includes("?") ? "&" : "?"}updated=1`);
}

export async function runContractAction(formData: FormData) {
  const contractId = getContractId(formData);
  const returnPath = getReturnPath(formData, `/contracts/detail?contractId=${contractId}`);
  const access = await authorizeContract();
  if (!access.ok) redirectError(returnPath, access.message);

  const action = getText(formData, "action");
  if (["approve", "approve-activate", "decline-correction", "decline"].includes(action)) {
    const approverAccess = await authorizeContractRole(
      hasContractApproverRole,
      "You do not have permission to review vehicle contracts.",
    );
    if (!approverAccess.ok) redirectError(returnPath, approverAccess.message);
  }

  let operation = "updated";
  try {
    switch (action) {
      case "submit":
        await postContractAction(`api/contracts/${contractId}/submit`);
        operation = "submitted";
        break;
      case "recall":
        await postContractAction(`api/contracts/${contractId}/recall`);
        operation = "recalled";
        break;
      case "approve":
        await postContractAction(`api/contracts/${contractId}/approve`, { ApprovalNotes: getOptionalText(formData, "approvalNotes", "Approval notes", 1000) });
        operation = "approved";
        break;
      case "approve-activate":
        await postContractAction(`api/contracts/${contractId}/approve-activate`, { ApprovalNotes: getOptionalText(formData, "approvalNotes", "Approval notes", 1000) });
        operation = "activated";
        break;
      case "decline-correction":
        await postContractAction(`api/contracts/${contractId}/decline-correction`, { DeclineReason: getOptionalText(formData, "declineReason", "Decline reason", 1000) ?? "Correction required." });
        operation = "returned for correction";
        break;
      case "decline":
        await postContractAction(`api/contracts/${contractId}/decline`, { DeclineReason: getOptionalText(formData, "declineReason", "Decline reason", 1000) ?? "Declined." });
        operation = "declined";
        break;
      case "cancel":
        await postContractAction(`api/contracts/${contractId}/cancel`, { CancellationReason: getOptionalText(formData, "cancellationReason", "Cancellation reason", 1000) });
        operation = "cancelled";
        break;
      case "extend":
        await extendContractAgainstApi(contractId, getRequiredDate(formData, "newTargetReturnDate", "New target return date"));
        operation = "extended";
        break;
      case "close": {
        const request: CloseContractRequest = {
          EndDate: getRequiredDate(formData, "endDate", "End date"),
          EndOdometer: getRequiredInteger(formData, "endOdometer", "End odometer"),
          Notes: getOptionalText(formData, "closeNotes", "Close notes", 1000),
          HomeDepartmentCode: getRequiredInteger(formData, "homeDepartmentCode", "Home department"),
          HomeSiteCode: getRequiredInteger(formData, "homeSiteCode", "Home site"),
          HomeSiteDriverCode: getInteger(formData, "homeSiteDriverCode", "Home site driver"),
          CreateHomeCustodyContract: getBoolean(formData, "createHomeCustodyContract"),
        };
        await closeContractAgainstApi(contractId, request);
        operation = "closed";
        break;
      }
      default:
        throw new ContractValidationError("The requested contract action is not supported.");
    }
  } catch (error) {
    redirectError(returnPath, error instanceof ContractValidationError ? error.message : apiErrorMessage(error, operation));
  }

  revalidatePath("/contracts");
  revalidatePath("/contracts/maintenance");
  revalidatePath("/contracts/detail");
  redirect(`${returnPath}${returnPath.includes("?") ? "&" : "?"}success=${encodeURIComponent(operation)}`);
}
