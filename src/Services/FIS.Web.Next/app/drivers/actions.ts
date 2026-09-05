"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  createDriverManagementAuthoriser,
  createDriverManagementSiteDriver,
  deleteDriverManagementAuthoriser,
  deleteDriverManagementSiteDriver,
  updateDriverManagementAuthoriser,
  updateDriverManagementSiteDriver,
  type DriverManagementAuthoriserInput,
  type DriverManagementDriverInput,
} from "@/lib/api-driver-management";
import { actionResultPath, hasVehicleManagementPermission } from "@/app/drivers/access";
import { getSession } from "@/lib/session";

class DriverManagementValidationError extends Error {}

function getText(formData: FormData, name: string) {
  const value = formData.get(name);
  return typeof value === "string" ? value.trim() : "";
}

function getRequiredText(formData: FormData, name: string, label: string, maxLength: number) {
  const value = getText(formData, name);
  if (!value) {
    throw new DriverManagementValidationError(`${label} is required.`);
  }
  if (value.length > maxLength) {
    throw new DriverManagementValidationError(`${label} must be ${maxLength} characters or fewer.`);
  }
  return value;
}

function getOptionalText(formData: FormData, name: string, label: string, maxLength: number) {
  const value = getText(formData, name);
  if (value.length > maxLength) {
    throw new DriverManagementValidationError(`${label} must be ${maxLength} characters or fewer.`);
  }
  return value || null;
}

function getInteger(formData: FormData, name: string, label: string, required = true) {
  const value = getText(formData, name);
  if (!value && !required) {
    return null;
  }
  const parsed = Number(value);
  if (!value || !Number.isSafeInteger(parsed)) {
    throw new DriverManagementValidationError(`${label} must be a valid whole number.`);
  }
  return parsed;
}

function getDate(formData: FormData, name: string, label: string, required = true) {
  const value = getText(formData, name);
  if (!value && !required) {
    return null;
  }
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value) || Number.isNaN(Date.parse(`${value}T00:00:00Z`))) {
    throw new DriverManagementValidationError(`${label} must be a valid date.`);
  }
  return value;
}

function normalizeCompact(value: string | null, uppercase = false) {
  if (!value) {
    return null;
  }
  const compact = value.replace(/[\s-]/g, "");
  return compact ? (uppercase ? compact.toUpperCase() : compact) : null;
}

function validateSouthAfricanId(value: string | null) {
  if (!value) {
    return null;
  }
  if (!/^\d{13}$/.test(value)) {
    return "South African ID number must be 13 digits.";
  }

  const year = Number(value.slice(0, 2));
  const month = Number(value.slice(2, 4));
  const day = Number(value.slice(4, 6));
  const currentYear = new Date().getFullYear() % 100;
  const fullYear = year <= currentYear ? 2000 + year : 1900 + year;
  const date = new Date(Date.UTC(fullYear, month - 1, day));
  if (date.getUTCFullYear() !== fullYear || date.getUTCMonth() !== month - 1 || date.getUTCDate() !== day) {
    return "South African ID number must contain a valid birth date.";
  }

  let sum = 0;
  let doubleDigit = false;
  for (let index = value.length - 1; index >= 0; index -= 1) {
    let digit = Number(value[index]);
    if (doubleDigit) {
      digit *= 2;
      if (digit > 9) {
        digit -= 9;
      }
    }
    sum += digit;
    doubleDigit = !doubleDigit;
  }
  return sum % 10 === 0 ? null : "South African ID number is not valid.";
}

function getContext(formData: FormData) {
  const departmentCode = getInteger(formData, "departmentCode", "Department");
  const siteCode = getInteger(formData, "siteCode", "Site");
  if (departmentCode === null || siteCode === null || departmentCode <= 0 || siteCode <= 0) {
    throw new DriverManagementValidationError("A valid department and site are required.");
  }
  return { departmentCode, siteCode };
}

async function requireVehicleManagementAccess(returnPath: string) {
  const session = await getSession();
  if (session.status === "anonymous") {
    redirect("/login");
  }
  if (session.status === "expired") {
    redirect(`${returnPath}${returnPath.includes("?") ? "&" : "?"}result=unauthorized`);
  }
  if (session.status !== "authenticated") {
    redirect(`${returnPath}${returnPath.includes("?") ? "&" : "?"}result=unavailable`);
  }
  if (!hasVehicleManagementPermission(session.accessLevel)) {
    redirect(`${returnPath}${returnPath.includes("?") ? "&" : "?"}result=forbidden`);
  }
}

function mutationResult(result: { ok: true } | { ok: false; error: { reason: string } }) {
  if (result.ok) {
    return "success";
  }
  return result.error.reason;
}

function buildAuthoriserInput(formData: FormData): DriverManagementAuthoriserInput {
  const context = getContext(formData);
  const rankCode = getInteger(formData, "rankCode", "Rank");
  if (rankCode === null || rankCode <= 0) {
    throw new DriverManagementValidationError("Rank is required.");
  }

  return {
    rankCode,
    firstname: getRequiredText(formData, "firstname", "First Name", 50),
    surname: getRequiredText(formData, "surname", "Surname", 50),
    persalNumber: getOptionalText(formData, "persalNumber", "Persal Number", 10),
    telephoneNumber: getOptionalText(formData, "telephoneNumber", "Telephone Number", 20),
    isActive: getText(formData, "isActive") !== "false",
    siteCode: context.siteCode,
    departmentCode: context.departmentCode,
  };
}

export async function saveAuthoriserAction(formData: FormData) {
  const context = getContext(formData);
  const returnPath = actionResultPath("/drivers/authorisers/edit", context.departmentCode, context.siteCode, "");
  let input: DriverManagementAuthoriserInput;
  try {
    input = buildAuthoriserInput(formData);
  } catch (error) {
    if (error instanceof DriverManagementValidationError) {
      redirect(`${actionResultPath("/drivers/authorisers/edit", context.departmentCode, context.siteCode, "invalid")}&${new URLSearchParams({ message: error.message }).toString()}`);
    }
    throw error;
  }

  await requireVehicleManagementAccess(returnPath.replace(/&result=$/, ""));
  const authoriserCode = getInteger(formData, "authoriserCode", "Authoriser", false);
  const result = authoriserCode
    ? await updateDriverManagementAuthoriser(authoriserCode, input)
    : await createDriverManagementAuthoriser(input);
  revalidatePath("/drivers/authorisers");
  redirect(actionResultPath("/drivers/authorisers", context.departmentCode, context.siteCode, mutationResult(result)));
}

export async function deleteAuthoriserAction(formData: FormData) {
  const context = getContext(formData);
  const returnPath = actionResultPath("/drivers/authorisers", context.departmentCode, context.siteCode, "");
  await requireVehicleManagementAccess(returnPath.replace(/&result=$/, ""));
  const authoriserCode = getInteger(formData, "authoriserCode", "Authoriser");
  if (authoriserCode === null || authoriserCode <= 0) {
    redirect(actionResultPath("/drivers/authorisers", context.departmentCode, context.siteCode, "invalid"));
  }
  const result = await deleteDriverManagementAuthoriser(authoriserCode);
  revalidatePath("/drivers/authorisers");
  redirect(actionResultPath("/drivers/authorisers", context.departmentCode, context.siteCode, mutationResult(result)));
}

function buildSiteDriverInput(formData: FormData): DriverManagementDriverInput {
  const context = getContext(formData);
  const driverSAId = normalizeCompact(getOptionalText(formData, "driverSAId", "South African ID", 13));
  const driverPassportNumber = normalizeCompact(getOptionalText(formData, "driverPassportNumber", "Passport Number", 20), true);
  if (!driverSAId && !driverPassportNumber) {
    throw new DriverManagementValidationError("Either a passport number or South African ID number must be specified.");
  }
  const saIdError = validateSouthAfricanId(driverSAId);
  if (saIdError) {
    throw new DriverManagementValidationError(saIdError);
  }
  if (driverPassportNumber && !/^[A-Z0-9]+$/.test(driverPassportNumber)) {
    throw new DriverManagementValidationError("Passport number can contain letters and numbers only.");
  }

  const driverLicenceNumber = normalizeCompact(getRequiredText(formData, "driverLicenceNumber", "Licence Number", 20), true);
  if (!driverLicenceNumber || !/^\d{8}[A-Z]{4}$/.test(driverLicenceNumber)) {
    throw new DriverManagementValidationError("Licence number must be 8 digits followed by 4 letters.");
  }
  const driverLicenceTypeId = getInteger(formData, "driverLicenceTypeId", "Driver Licence Type");
  if (driverLicenceTypeId === null || driverLicenceTypeId <= 0) {
    throw new DriverManagementValidationError("Driver Licence Type is required.");
  }
  const driverHasPDP = getText(formData, "driverHasPDP") === "true";
  const driverPDPExpiryDate = getDate(formData, "driverPDPExpiryDate", "PDP Expiry Date", false);
  if (driverHasPDP && !driverPDPExpiryDate) {
    throw new DriverManagementValidationError("PDP Expiry Date is required when the driver has a PDP.");
  }

  return {
    siteCode: context.siteCode,
    driverLicenceTypeId,
    driverFirstname: getRequiredText(formData, "driverFirstname", "First Name", 50),
    driverSurname: getRequiredText(formData, "driverSurname", "Surname", 50),
    driverSAId,
    driverPassportNumber,
    driverPersonalNumber: normalizeCompact(getOptionalText(formData, "driverPersonalNumber", "Persal Number", 10)),
    driverContractNumber: normalizeCompact(getOptionalText(formData, "driverContractNumber", "Contract Number", 10)),
    driverLicenceNumber,
    driverLicenceIssueDate: getDate(formData, "driverLicenceIssueDate", "Licence Issue Date") ?? "",
    driverLicenceLastVerifiedDate: getDate(formData, "driverLicenceLastVerifiedDate", "Licence Last Verified Date") ?? "",
    driverHasPDP,
    driverPDPExpiryDate,
    driverLicenceExpiryDate: getDate(formData, "driverLicenceExpiryDate", "Licence Expiry Date", false),
    driverActive: true,
  };
}

export async function saveSiteDriverAction(formData: FormData) {
  const context = getContext(formData);
  const returnPath = actionResultPath("/drivers/site-drivers/edit", context.departmentCode, context.siteCode, "");
  let input: DriverManagementDriverInput;
  try {
    input = buildSiteDriverInput(formData);
  } catch (error) {
    if (error instanceof DriverManagementValidationError) {
      redirect(`${actionResultPath("/drivers/site-drivers/edit", context.departmentCode, context.siteCode, "invalid")}&${new URLSearchParams({ message: error.message }).toString()}`);
    }
    throw error;
  }

  await requireVehicleManagementAccess(returnPath.replace(/&result=$/, ""));
  const siteDriverCode = getInteger(formData, "siteDriverCode", "Site driver", false);
  const result = siteDriverCode
    ? await updateDriverManagementSiteDriver(siteDriverCode, input)
    : await createDriverManagementSiteDriver(input);
  revalidatePath("/drivers/site-drivers");
  redirect(actionResultPath("/drivers/site-drivers", context.departmentCode, context.siteCode, mutationResult(result)));
}

export async function deleteSiteDriverAction(formData: FormData) {
  const context = getContext(formData);
  const returnPath = actionResultPath("/drivers/site-drivers", context.departmentCode, context.siteCode, "");
  await requireVehicleManagementAccess(returnPath.replace(/&result=$/, ""));
  const siteDriverCode = getInteger(formData, "siteDriverCode", "Site driver");
  if (siteDriverCode === null || siteDriverCode <= 0) {
    redirect(actionResultPath("/drivers/site-drivers", context.departmentCode, context.siteCode, "invalid"));
  }
  const result = await deleteDriverManagementSiteDriver(siteDriverCode);
  revalidatePath("/drivers/site-drivers");
  redirect(actionResultPath("/drivers/site-drivers", context.departmentCode, context.siteCode, mutationResult(result)));
}
