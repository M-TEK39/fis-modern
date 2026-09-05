"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import {
  ClassApiError,
  createClass,
  deleteClass,
  getClassDeleteCheck,
  updateClass,
  type ClassWriteInput,
} from "@/lib/api-classes";
import { getSession } from "@/lib/session";

export type ClassActionState = {
  status: "idle" | "error";
  message?: string;
};

const initialState: ClassActionState = { status: "idle" };

class ClassValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getRequiredInteger(formData: FormData, key: string, label: string, maxLength: number, max: number) {
  const value = getText(formData, key);
  if (!value) throw new ClassValidationError(`${label} is required.`);
  if (!/^\d+$/.test(value) || value.length > maxLength) {
    throw new ClassValidationError(`${label} must be a whole number with no more than ${maxLength} digits.`);
  }
  const parsed = Number(value);
  if (!Number.isSafeInteger(parsed) || parsed < 0 || parsed > max) {
    throw new ClassValidationError(`${label} is outside the supported range.`);
  }
  return parsed;
}

function getRequiredDecimal(formData: FormData, key: string, label: string, pattern: RegExp, max: number) {
  const value = getText(formData, key);
  const parsed = Number(value);
  if (!value || !pattern.test(value) || !Number.isFinite(parsed) || parsed < 0 || parsed > max) {
    throw new ClassValidationError(`${label} is required and outside the supported range.`);
  }
  return parsed;
}

function getClassInput(formData: FormData): ClassWriteInput {
  const description = getText(formData, "description");
  if (!description || description.length > 60) {
    throw new ClassValidationError("Description is required and must be 60 characters or fewer.");
  }

  const classNumber = getText(formData, "classNumber");
  if (!/^\d{3}$/.test(classNumber)) {
    throw new ClassValidationError("Class number is required and must contain exactly 3 digits.");
  }

  const bankNumber = getText(formData, "bankNumber");
  if (!bankNumber || bankNumber.length > 30) {
    throw new ClassValidationError("Bank number is required and must be 30 characters or fewer.");
  }

  return {
    description,
    classNumber,
    bankNumber,
    monthsLife: getRequiredInteger(formData, "monthsLife", "Months life", 2, 99),
    depreciationPercent: getRequiredDecimal(formData, "depreciationPercent", "Depreciation percent", /^\d{1,2}(\.\d{1,2})?$/, 99.99),
    odometerLife: getRequiredInteger(formData, "odometerLife", "Odometer life", 6, 999999),
    appreciatePercent: getRequiredInteger(formData, "appreciatePercent", "Appreciate percent", 2, 99),
    replacementCost: getRequiredInteger(formData, "replacementCost", "Replacement cost", 8, 99999999),
  };
}

function getClassCode(formData: FormData) {
  const value = getText(formData, "classCode");
  const parsed = Number(value);
  if (!value || !Number.isInteger(parsed) || parsed <= 0 || parsed > 32767) {
    throw new ClassValidationError("Class code is invalid.");
  }
  return parsed;
}

async function authorizeClassMaintenance() {
  const session = await getSession();
  if (session.status === "unavailable") {
    return { ok: false as const, message: "The sign-in service is temporarily unavailable. Please try again." };
  }
  if (session.status !== "authenticated") {
    return { ok: false as const, message: "Your session has expired. Sign in again before continuing." };
  }
  if (!hasVehicleManagementPermission(session.accessLevel)) {
    return { ok: false as const, message: "You do not have permission to maintain vehicle classes." };
  }
  return { ok: true as const };
}

function apiErrorMessage(error: unknown, operation: string) {
  if (error instanceof ClassApiError) {
    if (error.reason === "unauthorized") return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable") return `The vehicle class ${operation} service is temporarily unavailable. Please try again.`;
    return error.message;
  }
  return `The vehicle class could not be ${operation}. Please try again.`;
}

function revalidateClassRoutes() {
  revalidatePath("/validation-data");
  revalidatePath("/validation-data/classes");
  revalidatePath("/Validation/MNT_Class.aspx");
}

export async function createClassAction(
  _previousState: ClassActionState = initialState,
  formData: FormData,
): Promise<ClassActionState> {
  const access = await authorizeClassMaintenance();
  if (!access.ok) return { status: "error", message: access.message };

  try {
    const created = await createClass(getClassInput(formData));
    if (!created) throw new ClassApiError("invalid-response", "The FIS API did not return the created class.");
  } catch (error) {
    return { status: "error", message: error instanceof ClassValidationError ? error.message : apiErrorMessage(error, "created") };
  }

  revalidateClassRoutes();
  redirect("/validation-data/classes?saved=created");
}

export async function updateClassAction(
  _previousState: ClassActionState = initialState,
  formData: FormData,
): Promise<ClassActionState> {
  const access = await authorizeClassMaintenance();
  if (!access.ok) return { status: "error", message: access.message };

  let classCode = 0;
  try {
    classCode = getClassCode(formData);
    const updated = await updateClass(classCode, getClassInput(formData));
    if (!updated) throw new ClassApiError("invalid-response", "The FIS API did not return the updated class.");
  } catch (error) {
    return { status: "error", message: error instanceof ClassValidationError ? error.message : apiErrorMessage(error, "updated") };
  }

  revalidateClassRoutes();
  redirect(`/validation-data/classes?saved=updated&classCode=${encodeURIComponent(String(classCode))}`);
}

export async function deleteClassAction(formData: FormData) {
  const access = await authorizeClassMaintenance();
  let classCode = 0;
  try {
    classCode = getClassCode(formData);
  } catch (error) {
    redirect(`/validation-data/classes?error=${encodeURIComponent(error instanceof Error ? error.message : "Class code is invalid.")}`);
  }
  if (!access.ok) {
    redirect(`/Validation/MNT_Class_Del_Check.aspx?code=${encodeURIComponent(String(classCode))}&error=${encodeURIComponent(access.message)}`);
  }

  let dependencies;
  try {
    dependencies = await getClassDeleteCheck(classCode);
  } catch (error) {
    redirect(`/Validation/MNT_Class_Del_Check.aspx?code=${classCode}&error=${encodeURIComponent(apiErrorMessage(error, "deleted"))}`);
  }
  if (!dependencies.canDelete || dependencies.modelCount > 0 || dependencies.vehicleCount > 0) {
    redirect(`/Validation/MNT_Class_Del_Check.aspx?code=${classCode}&error=${encodeURIComponent("This class cannot be deleted while models or vehicles are linked to it.")}`);
  }

  try {
    await deleteClass(classCode);
  } catch (error) {
    redirect(`/Validation/MNT_Class_Del_Check.aspx?code=${classCode}&error=${encodeURIComponent(apiErrorMessage(error, "deleted"))}`);
  }

  revalidateClassRoutes();
  redirect("/validation-data/classes?saved=deleted");
}
