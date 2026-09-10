"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  createFineAgainstApi,
  createTrafficDeptAgainstApi,
  deleteFineAgainstApi,
  deleteTrafficDeptAgainstApi,
  FineApiError,
  updateFineAgainstApi,
  updateTrafficDeptAgainstApi,
  type FineRequest,
  type TrafficDeptRequest,
} from "@/lib/api/fleet-operations/api-fines";
import { getSession } from "@/lib/auth/session";

const REPORTS_ROLE = "Reports";

class FineValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getOptionalText(formData: FormData, key: string, label: string, maxLength: number) {
  const value = getText(formData, key);
  if (value.length > maxLength) {
    throw new FineValidationError(`${label} must be ${maxLength} characters or fewer.`);
  }

  return value || null;
}

function getRequiredDate(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    throw new FineValidationError(`${label} is required.`);
  }

  const [year, month, day] = value.split("-").map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  if (
    date.getUTCFullYear() !== year ||
    date.getUTCMonth() !== month - 1 ||
    date.getUTCDate() !== day
  ) {
    throw new FineValidationError(`${label} is invalid.`);
  }

  return `${value}T00:00:00.000Z`;
}

function getOptionalDate(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  if (!value) {
    return null;
  }

  const [year, month, day] = value.split("-").map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  if (
    !/^\d{4}-\d{2}-\d{2}$/.test(value) ||
    date.getUTCFullYear() !== year ||
    date.getUTCMonth() !== month - 1 ||
    date.getUTCDate() !== day
  ) {
    throw new FineValidationError(`${label} is invalid.`);
  }

  return `${value}T00:00:00.000Z`;
}

function getPositiveInteger(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0) {
    throw new FineValidationError(`${label} is required.`);
  }

  return parsed;
}

function getOptionalSmallInteger(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  if (!value) {
    return null;
  }

  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed < 0 || parsed > 32767) {
    throw new FineValidationError(`${label} is invalid.`);
  }

  return parsed;
}

function getOptionalAmount(formData: FormData) {
  const value = getText(formData, "fineAmount");
  if (!value) {
    return null;
  }

  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 0) {
    throw new FineValidationError("Fine amount must be a non-negative amount.");
  }

  return parsed;
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
    return { ok: false as const, message: "You do not have permission to maintain Fines." };
  }

  return { ok: true as const };
}

function getFineReturnQuery(formData: FormData) {
  const searchType = getText(formData, "returnSearchType");
  const searchQuery = getText(formData, "returnSearchQuery");
  const params = new URLSearchParams();
  if (searchType === "GG" || searchType === "GP") {
    params.set("searchType", searchType);
  }
  if (searchQuery) {
    params.set("searchQuery", searchQuery.slice(0, 8));
  }
  return params.toString();
}

function redirectFineError(message: string, formData: FormData) {
  const params = new URLSearchParams({ error: message });
  const query = getFineReturnQuery(formData);
  if (query) {
    new URLSearchParams(query).forEach((value, key) => params.set(key, value));
  }
  redirect(`/fines/maintenance?${params.toString()}`);
}

function fineApiErrorMessage(error: unknown, operation: string) {
  if (error instanceof FineApiError) {
    if (error.reason === "unauthorized") {
      return "Your session has expired. Sign in again before continuing.";
    }
    if (error.reason === "unavailable") {
      return `The Fines ${operation} service is temporarily unavailable. Please try again.`;
    }
    if (error.reason === "not-found") {
      return "The fine record was not found. Return to the Fines menu and try again.";
    }
  }

  return `The fine could not be ${operation}. Please try again.`;
}

function buildFineRequest(formData: FormData): FineRequest {
  return {
    vmf_code: getPositiveInteger(formData, "vmfCode", "Vehicle"),
    Offence_date: getRequiredDate(formData, "offenceDate", "Date of offence"),
    Offence_reference: getOptionalText(formData, "offenceReference", "Reference number", 30),
    Offence_issuer: getOptionalText(formData, "offenceIssuer", "Issued by", 20),
    Fine_amount: getOptionalAmount(formData),
    Appear_date: getOptionalDate(formData, "appearDate", "Court appearance date"),
    Receive_gg_date: getOptionalDate(formData, "receiveGgDate", "Date received at GMT"),
    Notify_dept_date: getOptionalDate(
      formData,
      "notifyDeptDate",
      "Date of notification to department",
    ),
    Site_code: getOptionalSmallInteger(formData, "siteCode", "Site"),
    Offence_name: getOptionalText(formData, "offenceName", "Name of offender", 20),
    Fine_pay_date: getOptionalDate(formData, "finePayDate", "Date fine paid"),
    Withdraw_date: getOptionalDate(formData, "withdrawDate", "Date withdrawn"),
    Pay_due_date: getOptionalDate(formData, "payDueDate", "Payment due date"),
    Issuer_notify_date: getOptionalDate(
      formData,
      "issuerNotifyDate",
      "Date of notification to issuer",
    ),
    Dept_person_name: getOptionalText(formData, "deptPersonName", "Responsible person name", 25),
    Dept_person_id: getOptionalText(formData, "deptPersonId", "Responsible person ID", 13),
    Document_type: getOptionalText(formData, "documentType", "Document type", 20),
    Traffic_dept_code: getOptionalSmallInteger(formData, "trafficDeptCode", "Traffic department"),
  };
}

export async function saveFineAction(formData: FormData) {
  const access = await authorizeReports();
  if (!access.ok) {
    redirectFineError(access.message, formData);
  }

  let request: FineRequest;
  try {
    request = buildFineRequest(formData);
  } catch (error) {
    redirectFineError(
      error instanceof FineValidationError ? error.message : "The fine form is invalid.",
      formData,
    );
  }

  const rawFineCode = getText(formData, "fineCode");
  const fineCode = rawFineCode ? Number(rawFineCode) : null;
  if (fineCode !== null && (!Number.isInteger(fineCode) || fineCode <= 0)) {
    redirectFineError("The fine record is invalid.", formData);
  }

  try {
    if (fineCode === null) {
      await createFineAgainstApi(request!);
    } else {
      await updateFineAgainstApi(fineCode, request!);
    }
  } catch (error) {
    redirectFineError(
      fineApiErrorMessage(error, fineCode === null ? "captured" : "updated"),
      formData,
    );
  }

  revalidatePath("/fines");
  revalidatePath("/fines/maintenance");
  const query = getFineReturnQuery(formData);
  redirect(
    `/fines/maintenance?${query ? `${query}&` : ""}${fineCode === null ? "saved=1" : "updated=1"}`,
  );
}

export async function deleteFineAction(formData: FormData) {
  const access = await authorizeReports();
  if (!access.ok) {
    redirect(`/fines/delete?error=${encodeURIComponent(access.message)}`);
  }

  const fineCode = Number(getText(formData, "fineCode"));
  if (!Number.isInteger(fineCode) || fineCode <= 0) {
    redirect("/fines/delete?error=The%20fine%20record%20is%20invalid.");
  }

  try {
    await deleteFineAgainstApi(fineCode);
  } catch (error) {
    redirect(`/fines/delete?error=${encodeURIComponent(fineApiErrorMessage(error, "deleted"))}`);
  }

  revalidatePath("/fines");
  revalidatePath("/fines/delete");
  redirect("/fines/delete?deleted=1");
}

function getTrafficReturnPath(formData: FormData) {
  const value = getText(formData, "returnPath");
  return value === "/fines/traffic-dept" ? value : "/fines/traffic-dept";
}

function redirectTrafficError(formData: FormData, message: string) {
  redirect(`${getTrafficReturnPath(formData)}?error=${encodeURIComponent(message)}`);
}

function buildTrafficDeptRequest(formData: FormData): TrafficDeptRequest {
  const name = getText(formData, "name");
  if (!name) {
    throw new FineValidationError("Traffic department name is required.");
  }

  if (name.length > 50) {
    throw new FineValidationError("Traffic department name must be 50 characters or fewer.");
  }

  return {
    Traf_name: name,
    Traf_res_person: getOptionalText(formData, "responsiblePerson", "Responsible person", 30),
    Traf_post_address1: getOptionalText(formData, "postalAddress1", "Postal address 1", 30),
    Traf_post_address2: getOptionalText(formData, "postalAddress2", "Postal address 2", 30),
    Traf_post_code: getOptionalText(formData, "postalCode", "Postal code", 4),
    Traf_telephone: getOptionalText(formData, "telephone", "Telephone", 30),
    Traf_fax: getOptionalText(formData, "fax", "Fax", 20),
    Traf_cell: getOptionalText(formData, "cell", "Cell", 15),
    Traf_email: getOptionalText(formData, "email", "Email", 20),
  };
}

function trafficApiErrorMessage(error: unknown, operation: string) {
  if (error instanceof FineApiError) {
    if (error.reason === "unauthorized") {
      return "Your session has expired. Sign in again before continuing.";
    }
    if (error.reason === "unavailable") {
      return `The Traffic Dept ${operation} service is temporarily unavailable. Please try again.`;
    }
    if (error.reason === "not-found") {
      return "The traffic department record was not found.";
    }
  }

  return `The traffic department could not be ${operation}. Please try again.`;
}

export async function saveTrafficDeptAction(formData: FormData) {
  const access = await authorizeReports();
  if (!access.ok) {
    redirectTrafficError(formData, access.message);
  }

  let request: TrafficDeptRequest;
  try {
    request = buildTrafficDeptRequest(formData);
  } catch (error) {
    redirectTrafficError(
      formData,
      error instanceof FineValidationError ? error.message : "The form is invalid.",
    );
  }

  const rawCode = getText(formData, "trafficDeptCode");
  const code = rawCode ? Number(rawCode) : null;
  if (code !== null && (!Number.isInteger(code) || code <= 0 || code > 32767)) {
    redirectTrafficError(formData, "The traffic department record is invalid.");
  }

  try {
    if (code === null) {
      await createTrafficDeptAgainstApi(request!);
    } else {
      await updateTrafficDeptAgainstApi(code, request!);
    }
  } catch (error) {
    redirectTrafficError(
      formData,
      trafficApiErrorMessage(error, code === null ? "created" : "updated"),
    );
  }

  revalidatePath("/fines");
  revalidatePath("/fines/traffic-dept");
  redirect(`/fines/traffic-dept?${code === null ? "saved=1" : "updated=1"}`);
}

export async function deleteTrafficDeptAction(formData: FormData) {
  const access = await authorizeReports();
  if (!access.ok) {
    redirectTrafficError(formData, access.message);
  }

  const code = Number(getText(formData, "trafficDeptCode"));
  if (!Number.isInteger(code) || code <= 0 || code > 32767) {
    redirectTrafficError(formData, "The traffic department record is invalid.");
  }

  try {
    await deleteTrafficDeptAgainstApi(code);
  } catch (error) {
    redirectTrafficError(formData, trafficApiErrorMessage(error, "deleted"));
  }

  revalidatePath("/fines/traffic-dept");
  redirect("/fines/traffic-dept?deleted=1");
}
