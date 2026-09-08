"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  createPrivateHireContractor,
  createPrivateHireVehicle,
  deletePrivateHireContractor,
  deletePrivateHireVehicle,
  PrivateHireApiError,
  updatePrivateHireContractor,
  updatePrivateHireVehicle,
} from "@/lib/api-private-hire";
import { getSession } from "@/lib/session";

const PRIVATE_HIRE_ROLE = "Private Hire Vehicles";

class PrivateHireValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getNumber(
  formData: FormData,
  key: string,
  label: string,
  options: { required?: boolean; min?: number } = {},
) {
  const value = getText(formData, key);
  if (!value && !options.required) return null;
  const parsed = Number(value);
  const min = options.min ?? 0;
  if (!value || !Number.isInteger(parsed) || parsed < min) {
    throw new PrivateHireValidationError(`${label} must be a whole number of at least ${min}.`);
  }
  return parsed;
}

function getDecimal(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  if (!value) return null;
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 0)
    throw new PrivateHireValidationError(`${label} must be a zero or greater amount.`);
  return parsed;
}

function getDate(formData: FormData, key: string, label: string, required = false) {
  const value = getText(formData, key);
  if (!value && !required) return null;
  if (!value || Number.isNaN(Date.parse(value)))
    throw new PrivateHireValidationError(`${label} must be a valid date.`);
  return `${value}T00:00:00`;
}

function getReturnPath(formData: FormData, fallback: string) {
  const value = getText(formData, "returnPath");
  return value.startsWith("/private-hire") && !value.startsWith("//") ? value : fallback;
}

function redirectWithMessage(path: string, key: string, message: string): never {
  redirect(
    `${path}${path.includes("?") ? "&" : "?"}${new URLSearchParams({ [key]: message }).toString()}`,
  );
}

async function authorizePrivateHire() {
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
  if (
    !session.roles.some(
      (role) => role.localeCompare(PRIVATE_HIRE_ROLE, undefined, { sensitivity: "accent" }) === 0,
    )
  ) {
    return {
      ok: false as const,
      message: "You do not have permission to maintain Private Hire Vehicles.",
    };
  }
  return { ok: true as const };
}

function apiErrorMessage(error: unknown, subject: string) {
  if (error instanceof PrivateHireApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable")
      return `The Private Hire ${subject} service is temporarily unavailable. Please try again.`;
    if (error.reason === "not-found") return `The ${subject} record was not found.`;
  }
  return `${subject[0].toUpperCase()}${subject.slice(1)} operation failed. Please try again.`;
}

function vehicleInput(formData: FormData) {
  const registrationNumber = getText(formData, "registrationNumber");
  const modelCode = getNumber(formData, "modelCode", "Model code", { required: true, min: 1 });
  const siteCode = getNumber(formData, "siteCode", "Site code", { required: true, min: 1 });
  const contractorId = getNumber(formData, "contractorId", "Contractor", {
    required: true,
    min: 1,
  });
  if (!registrationNumber) throw new PrivateHireValidationError("Registration number is required.");
  return {
    registrationNumber,
    modelCode: modelCode ?? 0,
    modelDescription: getText(formData, "modelDescription") || null,
    siteCode: siteCode ?? 0,
    contractedTo: getNumber(formData, "contractedTo", "Contracted to", { min: 0 }),
    engineNumber: getText(formData, "engineNumber") || null,
    chassisNumber: getText(formData, "chassisNumber") || null,
    yearManufactured: getText(formData, "yearManufactured") || null,
    bankCode: getText(formData, "bankCode") || null,
    colour: getText(formData, "colour") || null,
    tankCapacity: getNumber(formData, "tankCapacity", "Tank capacity", { min: 0 }),
    contractorId: contractorId ?? 0,
    fuelCard: getText(formData, "fuelCard") || null,
    fuelCardReceiver: getText(formData, "fuelCardReceiver") || null,
    takeOnDate: getDate(formData, "takeOnDate", "Take-on date", true),
    takeOnOdo:
      getNumber(formData, "takeOnOdo", "Take-on odometer", { required: true, min: 0 }) ?? 0,
    returnDate: getDate(formData, "returnDate", "Return date"),
    returnOdo: getNumber(formData, "returnOdo", "Return odometer", { required: true, min: 0 }) ?? 0,
    kmTariff: getDecimal(formData, "kmTariff", "Kilometre tariff"),
    dailyTariff: getDecimal(formData, "dailyTariff", "Daily tariff"),
    hourlyTariff: getDecimal(formData, "hourlyTariff", "Hourly tariff"),
  };
}

function contractorInput(formData: FormData) {
  const companyName = getText(formData, "companyName");
  if (!companyName) throw new PrivateHireValidationError("Company name is required.");
  const active = getText(formData, "status").toLowerCase() === "inactive" ? 0 : 1;
  return {
    companyName,
    physicalAddress: getText(formData, "physicalAddress") || null,
    postalAddress: getText(formData, "postalAddress") || null,
    phone: getText(formData, "phone") || null,
    faxNumber: getText(formData, "faxNumber") || null,
    email: getText(formData, "email") || null,
    contactPerson: getText(formData, "contactPerson") || null,
    active,
    type: getText(formData, "type") || null,
    quotations: formData.get("quotations") === "on",
    projectName: getText(formData, "projectName") || null,
    projectBeginDate: getDate(formData, "projectBeginDate", "Project begin date"),
    projectEndDate: getDate(formData, "projectEndDate", "Project end date"),
  };
}

export async function savePrivateHireVehicleAction(formData: FormData) {
  const returnPath = getReturnPath(formData, "/private-hire/maintenance?mode=add");
  const access = await authorizePrivateHire();
  if (!access.ok) redirectWithMessage(returnPath, "error", access.message);
  try {
    const input = vehicleInput(formData);
    const phvCode = getNumber(formData, "phvCode", "Vehicle", { min: 1 });
    if (phvCode === null) await createPrivateHireVehicle(input);
    else await updatePrivateHireVehicle(phvCode, input);
    revalidatePath("/private-hire");
    revalidatePath("/private-hire/maintenance");
    redirectWithMessage(
      returnPath,
      "saved",
      phvCode === null
        ? "Private Hire vehicle added successfully."
        : "Private Hire vehicle updated successfully.",
    );
  } catch (error) {
    redirectWithMessage(
      returnPath,
      "error",
      error instanceof PrivateHireValidationError
        ? error.message
        : apiErrorMessage(error, "vehicle"),
    );
  }
}

export async function deletePrivateHireVehicleAction(formData: FormData) {
  const returnPath = getReturnPath(formData, "/private-hire/maintenance?mode=delete");
  const access = await authorizePrivateHire();
  if (!access.ok) redirectWithMessage(returnPath, "error", access.message);
  try {
    const phvCode = getNumber(formData, "phvCode", "Vehicle", { required: true, min: 1 });
    await deletePrivateHireVehicle(phvCode ?? 0);
    revalidatePath("/private-hire");
    revalidatePath("/private-hire/maintenance");
    redirectWithMessage(returnPath, "deleted", "Private Hire vehicle deleted successfully.");
  } catch (error) {
    redirectWithMessage(
      returnPath,
      "error",
      error instanceof PrivateHireValidationError
        ? error.message
        : apiErrorMessage(error, "vehicle"),
    );
  }
}

export async function savePrivateHireContractorAction(formData: FormData) {
  const returnPath = getReturnPath(formData, "/private-hire/maintenance?mode=contractor-add");
  const access = await authorizePrivateHire();
  if (!access.ok) redirectWithMessage(returnPath, "error", access.message);
  try {
    const input = contractorInput(formData);
    const contractorId = getNumber(formData, "contractorId", "Contractor", { min: 1 });
    if (contractorId === null) await createPrivateHireContractor(input);
    else await updatePrivateHireContractor(contractorId, input);
    revalidatePath("/private-hire");
    revalidatePath("/private-hire/maintenance");
    revalidatePath("/private-hire/reports/contractors");
    redirectWithMessage(
      returnPath,
      "saved",
      contractorId === null
        ? "Private Hire contractor added successfully."
        : "Private Hire contractor updated successfully.",
    );
  } catch (error) {
    redirectWithMessage(
      returnPath,
      "error",
      error instanceof PrivateHireValidationError
        ? error.message
        : apiErrorMessage(error, "contractor"),
    );
  }
}

export async function deletePrivateHireContractorAction(formData: FormData) {
  const returnPath = getReturnPath(formData, "/private-hire/maintenance?mode=contractor-delete");
  const access = await authorizePrivateHire();
  if (!access.ok) redirectWithMessage(returnPath, "error", access.message);
  try {
    const contractorId = getNumber(formData, "contractorId", "Contractor", {
      required: true,
      min: 1,
    });
    await deletePrivateHireContractor(contractorId ?? 0);
    revalidatePath("/private-hire");
    revalidatePath("/private-hire/maintenance");
    revalidatePath("/private-hire/reports/contractors");
    redirectWithMessage(returnPath, "deleted", "Private Hire contractor deleted successfully.");
  } catch (error) {
    redirectWithMessage(
      returnPath,
      "error",
      error instanceof PrivateHireValidationError
        ? error.message
        : apiErrorMessage(error, "contractor"),
    );
  }
}
