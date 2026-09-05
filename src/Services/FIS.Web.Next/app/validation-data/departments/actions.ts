"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import {
  createDepartment,
  DepartmentApiError,
  deleteDepartment,
  updateDepartment,
  type DepartmentInput,
} from "@/lib/api-departments";
import { getSession } from "@/lib/session";

export type DepartmentActionState = {
  status: "idle" | "error";
  message?: string;
};

const initialState: DepartmentActionState = { status: "idle" };

class DepartmentValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getRequiredText(formData: FormData, key: string, label: string, maxLength: number) {
  const value = getText(formData, key);
  if (!value) throw new DepartmentValidationError(`${label} is required.`);
  if (value.length > maxLength) throw new DepartmentValidationError(`${label} must be ${maxLength} characters or fewer.`);
  return value;
}

function getOptionalText(formData: FormData, key: string, label: string, maxLength: number) {
  const value = getText(formData, key);
  if (value.length > maxLength) throw new DepartmentValidationError(`${label} must be ${maxLength} characters or fewer.`);
  return value || null;
}

function getInteger(formData: FormData, key: string, label: string, required = false) {
  const value = getText(formData, key);
  if (!value && !required) return null;
  if (!value) throw new DepartmentValidationError(`${label} is required.`);

  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed < 0) {
    throw new DepartmentValidationError(`${label} must be a non-negative whole number.`);
  }
  return parsed;
}

function getDecimal(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  if (!value) return 0;

  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 0) {
    throw new DepartmentValidationError(`${label} must be a non-negative number.`);
  }
  return parsed;
}

function getBoolean(formData: FormData, key: string) {
  return getText(formData, key).toLowerCase() === "true";
}

function getNullableBoolean(formData: FormData, key: string) {
  const value = getText(formData, key).toLowerCase();
  if (!value) return null;
  return value === "true";
}

function getOptionalDate(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  if (!value) return null;
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) throw new DepartmentValidationError(`${label} is invalid.`);

  const [year, month, day] = value.split("-").map(Number);
  const parsed = new Date(Date.UTC(year, month - 1, day));
  if (parsed.getUTCFullYear() !== year || parsed.getUTCMonth() !== month - 1 || parsed.getUTCDate() !== day) {
    throw new DepartmentValidationError(`${label} is invalid.`);
  }
  return `${value}T00:00:00.000Z`;
}

function buildInput(formData: FormData, mode: "create" | "update"): DepartmentInput {
  const description = getRequiredText(formData, "description", "Department description", mode === "create" ? 60 : 75);
  const departmentNumber = getOptionalText(formData, "departmentNumber", "Department number", 7);
  const postalCode = getOptionalText(formData, "postalCode", "Postal code", 10);

  if (mode === "create" && postalCode && !/^\d+$/.test(postalCode)) {
    throw new DepartmentValidationError("Postal code must contain numbers only.");
  }

  if (mode === "create" && (!departmentNumber || !/^\d{7}$/.test(departmentNumber))) {
    throw new DepartmentValidationError("Department number must contain exactly 7 numbers.");
  }

  return {
    departmentCode: getInteger(formData, "departmentCode", "Department code") ?? undefined,
    companyCode: getInteger(formData, "companyCode", "Company code") ?? 0,
    description,
    responsiblePerson: getOptionalText(formData, "responsiblePerson", "Responsible person", 59),
    address1: getOptionalText(formData, "address1", "Address 1", 50),
    address2: getOptionalText(formData, "address2", "Address 2", 50),
    address3: getOptionalText(formData, "address3", "Address 3", 50),
    postalCode,
    telephone: getOptionalText(formData, "telephone", "Telephone", 15),
    fax: getOptionalText(formData, "fax", "Fax", 15),
    netAddress: getOptionalText(formData, "netAddress", "Network address", 60),
    departmentNumber,
    cellNumber: getOptionalText(formData, "cellNumber", "Cell number", 15),
    notes: getOptionalText(formData, "notes", "Notes", 100),
    departmentAbbr: getOptionalText(formData, "departmentAbbr", "Department abbreviation", 30),
    basInstallationCode: getOptionalText(formData, "basInstallationCode", "BAS installation code", 30),
    deptActive: getBoolean(formData, "deptActive"),
    cloEmail: getOptionalText(formData, "cloEmail", "CLO email", 255),
    telephone2: getOptionalText(formData, "telephone2", "Telephone 2", 15),
    fax2: getOptionalText(formData, "fax2", "Fax 2", 15),
    financialSystemCode: getInteger(formData, "financialSystemCode", "Financial system code"),
    financialSystemActive: getNullableBoolean(formData, "financialSystemActive"),
    financialSystemActivateDate: getOptionalDate(formData, "financialSystemActivateDate", "Financial system activation date"),
    defaultSite: getInteger(formData, "defaultSite", "Default site"),
    exportIsActive: getNullableBoolean(formData, "exportIsActive"),
    dateLastExported: null,
    serviceKilometres: getInteger(formData, "serviceKilometres", "Service kilometres") ?? 0,
    serviceYears: getInteger(formData, "serviceYears", "Service years") ?? 0,
    overheadPercentage: getDecimal(formData, "overheadPercentage", "Overhead percentage"),
    userAccessCode: getInteger(formData, "userAccessCode", "User access code"),
    comments: getOptionalText(formData, "comments", "Comments", 1000),
  };
}

async function authorizeDepartmentMaintenance() {
  const session = await getSession();
  if (session.status === "unavailable") {
    return { ok: false as const, message: "The sign-in service is temporarily unavailable. Please try again." };
  }
  if (session.status !== "authenticated") {
    return { ok: false as const, message: "Your session has expired. Sign in again before continuing." };
  }
  if (!hasVehicleManagementPermission(session.accessLevel)) {
    return { ok: false as const, message: "You do not have permission to maintain departments." };
  }
  return { ok: true as const };
}

function apiErrorMessage(error: unknown, operation: string) {
  if (error instanceof DepartmentApiError) {
    if (error.reason === "unauthorized") return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable") return `The department ${operation} service is temporarily unavailable. Please try again.`;
    return error.message;
  }
  return `The department could not be ${operation}. Please try again.`;
}

export async function createDepartmentAction(
  _previousState: DepartmentActionState = initialState,
  formData: FormData,
): Promise<DepartmentActionState> {
  const access = await authorizeDepartmentMaintenance();
  if (!access.ok) return { status: "error", message: access.message };

  try {
    await createDepartment(buildInput(formData, "create"));
  } catch (error) {
    return { status: "error", message: error instanceof DepartmentValidationError ? error.message : apiErrorMessage(error, "created") };
  }

  revalidatePath("/validation-data");
  revalidatePath("/validation-data/departments");
  redirect("/validation-data/departments?saved=created");
}

export async function updateDepartmentAction(
  _previousState: DepartmentActionState = initialState,
  formData: FormData,
): Promise<DepartmentActionState> {
  const access = await authorizeDepartmentMaintenance();
  const departmentCode = getInteger(formData, "departmentCode", "Department code");
  const returnPath = departmentCode ? `/Validation/MNT_Department_Edit.aspx?cmbdep=${departmentCode}` : "/validation-data/departments";
  if (!access.ok) redirect(`${returnPath}&error=${encodeURIComponent(access.message)}`);
  if (!departmentCode) return { status: "error", message: "Department code is required." };

  try {
    await updateDepartment(departmentCode, buildInput(formData, "update"));
  } catch (error) {
    return { status: "error", message: error instanceof DepartmentValidationError ? error.message : apiErrorMessage(error, "updated") };
  }

  revalidatePath("/validation-data/departments");
  revalidatePath(`/Validation/MNT_Department_Edit.aspx`);
  redirect("/validation-data/departments?saved=updated");
}

export async function deleteDepartmentAction(formData: FormData) {
  const access = await authorizeDepartmentMaintenance();
  const departmentCode = getInteger(formData, "departmentCode", "Department code");
  if (!access.ok) redirect(`/Validation/MNT_Department_Del_Check.aspx?code=${departmentCode ?? ""}&error=${encodeURIComponent(access.message)}`);
  if (!departmentCode) redirect("/validation-data/departments?error=Department%20code%20is%20required.");

  try {
    await deleteDepartment(departmentCode);
  } catch (error) {
    redirect(`/Validation/MNT_Department_Del_Check.aspx?code=${departmentCode}&error=${encodeURIComponent(apiErrorMessage(error, "deleted"))}`);
  }

  revalidatePath("/validation-data/departments");
  redirect("/validation-data/departments?saved=deleted");
}
