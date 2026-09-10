"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  closeContractAgainstApi,
  ContractApiError,
  editContractAgainstApi,
  extendContractAgainstApi,
  createReliefContractAgainstApi,
  getContract,
  hireContractAgainstApi,
  postContractAction,
  reassignContractAgainstApi,
  updateContractHistoryAgainstApi,
  type CloseContractRequest,
  type EditContractRequest,
  type HireContractRequest,
} from "@/lib/api/finance/api-contracts";
import {
  canCaptureNewContract,
  canCloseActiveContract,
  canEditContract,
  canManageActiveContract,
  canReviewContract,
  canSubmitContract,
  hasContractAccess,
  hasContractHistoryBackdatingRole,
} from "@/app/(fleet-operations)/contracts/access";
import { getSession } from "@/lib/auth/session";

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
  if (
    parsed.getUTCFullYear() !== year ||
    parsed.getUTCMonth() !== month - 1 ||
    parsed.getUTCDate() !== day
  ) {
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
  if (value.length > maxLength)
    throw new ContractValidationError(`${label} must be ${maxLength} characters or fewer.`);
  return value || null;
}

function getBoolean(formData: FormData, key: string) {
  return getText(formData, key).toLowerCase() === "true";
}

type AuthenticatedSession = Extract<Awaited<ReturnType<typeof getSession>>, {
  status: "authenticated";
}>;

type ContractAuthorization =
  | { ok: false; message: string }
  | { ok: true; session: AuthenticatedSession };

async function authorizeContract(): Promise<ContractAuthorization> {
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
  if (!hasContractAccess(session.accessLevel, session.roles)) {
    return {
      ok: false as const,
      message: "You do not have permission to maintain vehicle contracts.",
    };
  }
  return { ok: true as const, session };
}

async function authorizeContractRole(
  roleCheck: (session: AuthenticatedSession) => boolean,
  message: string,
) {
  const access = await authorizeContract();
  if (!access.ok) return access;

  if (!roleCheck(access.session)) {
    return { ok: false as const, message };
  }

  return access;
}

function apiErrorMessage(error: unknown, operation: string) {
  if (error instanceof ContractApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable")
      return `The contract ${operation} service is temporarily unavailable. Please try again.`;
    if (error.reason === "not-found")
      return "The contract record was not found. Return to the Contracts menu and try again.";
    return error.message;
  }
  return `The contract could not be ${operation}. Please try again.`;
}

function redirectError(returnPath: string, message: string): never {
  redirect(
    `${returnPath}${returnPath.includes("?") ? "&" : "?"}error=${encodeURIComponent(message)}`,
  );
}

async function authorizeContractRecord(contractId: number, returnPath: string) {
  const access = await authorizeContract();
  if (!access.ok) redirectError(returnPath, access.message);

  try {
    return { ...access, contract: await getContract(contractId) };
  } catch (error) {
    redirectError(returnPath, apiErrorMessage(error, "loaded"));
  }
}

function canRunContractAction(
  action: string,
  contract: Awaited<ReturnType<typeof getContract>>,
  session: AuthenticatedSession,
) {
  const status = contract.contractStatusCode;
  const isActive = status === 3 || (status === null && contract.stillCurrent?.toUpperCase() === "Y");
  switch (action) {
    case "submit":
      return (status === 0 || status === 4) && canSubmitContract(contract, session);
    case "recall":
      return status === 1 && canSubmitContract(contract, session);
    case "approve":
    case "decline-correction":
    case "decline":
      return status === 1 && canReviewContract(contract, session);
    case "approve-activate":
      return (status === 1 || status === 2) && canReviewContract(contract, session);
    case "extend":
    case "reassign":
    case "relief":
      return isActive && canManageActiveContract(session.roles);
    case "close":
    case "cancel":
      return isActive && canCloseActiveContract(session.roles);
    default:
      return false;
  }
}

const ACTION_PERMISSION_MESSAGE = "You do not have permission for this contract action.";

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
  if (!canCaptureNewContract(access.session.roles)) {
    redirectError(returnPath, "You do not have permission to capture vehicle contracts.");
  }

  try {
    await hireContractAgainstApi(buildHireRequest(formData));
  } catch (error) {
    redirectError(
      returnPath,
      error instanceof ContractValidationError ? error.message : apiErrorMessage(error, "captured"),
    );
  }

  revalidatePath("/contracts");
  revalidatePath("/contracts/maintenance");
  redirect(`${returnPath}?saved=1`);
}

export async function editContractAction(formData: FormData) {
  const contractId = getContractId(formData);
  const returnPath = getReturnPath(formData, `/contracts/detail?contractId=${contractId}`);
  const access = await authorizeContractRecord(contractId, returnPath);
  if (!canEditContract(access.contract, access.session)) {
    redirectError(returnPath, ACTION_PERMISSION_MESSAGE);
  }
  if (![0, 4].includes(access.contract.contractStatusCode ?? -1)) {
    redirectError(returnPath, "This contract cannot be edited in its current state.");
  }

  try {
    await editContractAgainstApi(contractId, buildEditRequest(formData));
  } catch (error) {
    redirectError(
      returnPath,
      error instanceof ContractValidationError ? error.message : apiErrorMessage(error, "updated"),
    );
  }

  revalidatePath("/contracts");
  revalidatePath("/contracts/maintenance");
  revalidatePath("/contracts/detail");
  redirect(`${returnPath}${returnPath.includes("?") ? "&" : "?"}updated=1`);
}

export async function createReliefContractAction(formData: FormData) {
  const contractId = getContractId(formData);
  const returnPath = getReturnPath(formData, `/contracts/detail?contractId=${contractId}`);
  const access = await authorizeContractRole(
    (session) => canManageActiveContract(session.roles),
    "You do not have permission to create relief contracts.",
  );
  if (!access.ok) redirectError(returnPath, access.message);

  const reliefVmfCode = getRequiredInteger(formData, "reliefVmfCode", "Relief vehicle");
  if (reliefVmfCode <= 0) redirectError(returnPath, "Select a valid relief vehicle.");

  try {
    await createReliefContractAgainstApi(contractId, {
      ReliefVmfCode: reliefVmfCode,
      StartOdometer: getInteger(formData, "startOdometer", "Start odometer"),
      TargetReturnDate: getOptionalDate(formData, "targetReturnDate", "Target return date"),
      Reason: getOptionalText(formData, "reason", "Reason", 1000) ?? "Assign as relief vehicle",
    });
  } catch (error) {
    redirectError(
      returnPath,
      error instanceof ContractValidationError ? error.message : apiErrorMessage(error, "created"),
    );
  }

  revalidatePath("/contracts");
  revalidatePath("/contracts/maintenance");
  revalidatePath("/contracts/detail");
  revalidatePath("/contracts/relief-vehicle-search");
  redirect(`${returnPath}${returnPath.includes("?") ? "&" : "?"}success=relief-created`);
}

export async function updateContractHistoryAction(formData: FormData) {
  const contractId = getContractId(formData);
  const returnPath = getReturnPath(
    formData,
    `/contracts/backdating-history?contractId=${contractId}`,
  );
  const access = await authorizeContractRole(
    (session) => hasContractHistoryBackdatingRole(session.roles),
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
    redirectError(
      returnPath,
      error instanceof ContractValidationError ? error.message : apiErrorMessage(error, "updated"),
    );
  }

  revalidatePath("/contracts");
  revalidatePath("/contracts/backdating-history");
  revalidatePath("/contracts/detail");
  redirect(`${returnPath}${returnPath.includes("?") ? "&" : "?"}updated=1`);
}

export async function runContractAction(formData: FormData) {
  const contractId = getContractId(formData);
  const returnPath = getReturnPath(formData, `/contracts/detail?contractId=${contractId}`);
  const action = getText(formData, "action");
  const access = await authorizeContractRecord(contractId, returnPath);
  if (!canRunContractAction(action, access.contract, access.session)) {
    redirectError(returnPath, ACTION_PERMISSION_MESSAGE);
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
        await postContractAction(`api/contracts/${contractId}/approve`, {
          ApprovalNotes: getOptionalText(formData, "approvalNotes", "Approval notes", 1000),
        });
        operation = "approved";
        break;
      case "approve-activate":
        await postContractAction(`api/contracts/${contractId}/approve-activate`, {
          ApprovalNotes: getOptionalText(formData, "approvalNotes", "Approval notes", 1000),
        });
        operation = "activated";
        break;
      case "decline-correction":
        await postContractAction(`api/contracts/${contractId}/decline-correction`, {
          DeclineReason:
            getOptionalText(formData, "declineReason", "Decline reason", 1000) ??
            "Correction required.",
        });
        operation = "returned for correction";
        break;
      case "decline":
        await postContractAction(`api/contracts/${contractId}/decline`, {
          DeclineReason:
            getOptionalText(formData, "declineReason", "Decline reason", 1000) ?? "Declined.",
        });
        operation = "declined";
        break;
      case "cancel":
        await postContractAction(`api/contracts/${contractId}/cancel`, {
          CancellationReason: getOptionalText(
            formData,
            "cancellationReason",
            "Cancellation reason",
            1000,
          ),
        });
        operation = "cancelled";
        break;
      case "extend":
        await extendContractAgainstApi(
          contractId,
          getRequiredDate(formData, "newTargetReturnDate", "New target return date"),
        );
        operation = "extended";
        break;
      case "reassign": {
        const newSiteCode = getRequiredInteger(formData, "newSiteCode", "Destination site");
        if (newSiteCode <= 0)
          throw new ContractValidationError("Destination site must be a positive whole number.");
        await reassignContractAgainstApi(contractId, {
          NewSiteCode: newSiteCode,
          StartDate: getRequiredDate(formData, "reassignStartDate", "Effective start date"),
          StartOdometer: getRequiredInteger(formData, "reassignStartOdometer", "Start odometer"),
          Reason:
            getOptionalText(formData, "reassignReason", "Reassignment reason", 1000) ??
            "Reassigned from contract detail.",
        });
        operation = "reassigned";
        break;
      }
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
    redirectError(
      returnPath,
      error instanceof ContractValidationError ? error.message : apiErrorMessage(error, operation),
    );
  }

  revalidatePath("/contracts");
  revalidatePath("/contracts/maintenance");
  revalidatePath("/contracts/detail");
  redirect(
    `${returnPath}${returnPath.includes("?") ? "&" : "?"}success=${encodeURIComponent(operation)}`,
  );
}
