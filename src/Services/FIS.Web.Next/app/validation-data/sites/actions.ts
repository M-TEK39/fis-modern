"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { hasVehicleManagementPermission } from "@/app/drivers/access";
import {
  createSite,
  deleteSite,
  getSiteDeleteCheck,
  SiteApiError,
  updateSite,
  type SiteWriteInput,
} from "@/lib/api-sites";
import { getSession } from "@/lib/session";

export type SiteActionState = { status: "idle" | "error"; message?: string };

const initialState: SiteActionState = { status: "idle" };
class SiteValidationError extends Error {}

function getText(formData: FormData, key: string) {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

function getRequiredText(formData: FormData, key: string, label: string, maxLength: number) {
  const value = getText(formData, key);
  if (!value) throw new SiteValidationError(`${label} is required.`);
  if (value.length > maxLength)
    throw new SiteValidationError(`${label} must be ${maxLength} characters or fewer.`);
  return value;
}

function getOptionalText(formData: FormData, key: string, label: string, maxLength: number) {
  const value = getText(formData, key);
  if (value.length > maxLength)
    throw new SiteValidationError(`${label} must be ${maxLength} characters or fewer.`);
  return value || null;
}

function getCode(formData: FormData, key: string, label: string, max = 32767) {
  const value = getText(formData, key);
  if (!value) throw new SiteValidationError(`${label} is required.`);
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0 || parsed > max)
    throw new SiteValidationError(`${label} is invalid.`);
  return parsed;
}

function getOptionalInteger(formData: FormData, key: string, label: string, max = 2_147_483_647) {
  const value = getText(formData, key);
  if (!value) return null;
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed < 0 || parsed > max)
    throw new SiteValidationError(`${label} is invalid.`);
  return parsed;
}

function getNonNegativeInteger(formData: FormData, key: string, label: string, max: number) {
  const value = getText(formData, key);
  if (!value) return 0;
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed < 0 || parsed > max)
    throw new SiteValidationError(`${label} is invalid.`);
  return parsed;
}

function getDecimal(formData: FormData, key: string, label: string) {
  const value = getText(formData, key);
  if (!value) return 0;
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 0 || parsed > 999.999)
    throw new SiteValidationError(`${label} is invalid.`);
  return parsed;
}

function getBoolean(formData: FormData, key: string) {
  return getText(formData, key).toLowerCase() === "true";
}

function getNullableBoolean(formData: FormData, key: string) {
  const value = getText(formData, key).toLowerCase();
  if (!value) return null;
  if (value !== "true" && value !== "false")
    throw new SiteValidationError("A boolean setting is invalid.");
  return value === "true";
}

function getOptionalDate(formData: FormData, key: string, label: string, originalKey?: string) {
  const value = getText(formData, key);
  if (!value) return null;
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) throw new SiteValidationError(`${label} is invalid.`);
  const original = originalKey ? getText(formData, originalKey) : "";
  if (original && original.slice(0, 10) === value && !Number.isNaN(Date.parse(original))) {
    return original;
  }
  const [year, month, day] = value.split("-").map(Number);
  const parsed = new Date(Date.UTC(year, month - 1, day));
  if (
    parsed.getUTCFullYear() !== year ||
    parsed.getUTCMonth() !== month - 1 ||
    parsed.getUTCDate() !== day
  ) {
    throw new SiteValidationError(`${label} is invalid.`);
  }
  return `${value}T00:00:00.000Z`;
}

function buildInput(formData: FormData, mode: "create" | "update"): SiteWriteInput {
  const departmentNumber = getRequiredText(formData, "departmentNumber", "Department number", 7);
  if (!/^\d{7}$/.test(departmentNumber))
    throw new SiteValidationError("Department number must contain exactly 7 numbers.");

  const postalCode = getOptionalText(formData, "postalCode", "Postal code", 10);
  if (postalCode && !/^\d+$/.test(postalCode))
    throw new SiteValidationError("Postal code must contain numbers only.");

  const previousActive = getText(formData, "previousSiteActive").toLowerCase() === "true";
  const siteActive = getBoolean(formData, "siteActive");
  const notes = getOptionalText(formData, "notes", "Notes", 255);
  if (mode === "update" && previousActive !== siteActive) {
    const previousNotes = getText(formData, "previousNotes");
    if (!notes || notes === previousNotes) {
      throw new SiteValidationError("A new note is required when changing the site active status.");
    }
  }

  return {
    departmentCode: getCode(formData, "departmentCode", "Department"),
    description: getRequiredText(formData, "description", "Site description", 75),
    responsiblePerson: getOptionalText(formData, "responsiblePerson", "Responsible person", 75),
    address1: getOptionalText(formData, "address1", "Address 1", 60),
    address2: getOptionalText(formData, "address2", "Address 2", 60),
    address3: getOptionalText(formData, "address3", "Address 3", 60),
    postalCode,
    telephone: getOptionalText(formData, "telephone", "Telephone", 15),
    telephone2: getOptionalText(formData, "telephone2", "Telephone 2", 15),
    fax: getOptionalText(formData, "fax", "Fax", 15),
    fax1: getOptionalText(formData, "fax1", "Fax 2", 15),
    netAddress: getOptionalText(formData, "netAddress", "Network address", 60),
    departmentNumber,
    mapReference: getOptionalText(formData, "mapReference", "Map reference", 6),
    mapDescription: getOptionalText(formData, "mapDescription", "Map description", 50),
    cellNumber: getOptionalText(formData, "cellNumber", "Cell number", 15),
    siteActive,
    financialSystemCode: getOptionalInteger(
      formData,
      "financialSystemCode",
      "Financial system code",
      255,
    ),
    financialSystemActive: getNullableBoolean(formData, "financialSystemActive"),
    financialSystemActivateDate: getOptionalDate(
      formData,
      "financialSystemActivateDate",
      "Financial system activation date",
      "financialSystemActivateDateOriginal",
    ),
    exportIsActive: getNullableBoolean(formData, "exportIsActive"),
    dateLastExported: getOptionalDate(
      formData,
      "dateLastExported",
      "Last exported date",
      "dateLastExportedOriginal",
    ),
    serviceKilometres: getNonNegativeInteger(
      formData,
      "serviceKilometres",
      "Service kilometres",
      2_147_483_647,
    ),
    serviceYears: getNonNegativeInteger(formData, "serviceYears", "Service years", 255),
    overheadPercentage: getDecimal(formData, "overheadPercentage", "Overhead percentage"),
    provinceCode: getOptionalText(formData, "provinceCode", "Province", 3),
    notes,
    userAccessCode: getOptionalInteger(formData, "userAccessCode", "User access code", 32767),
  };
}

async function authorizeSiteMaintenance() {
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
  if (!hasVehicleManagementPermission(session.accessLevel))
    return { ok: false as const, message: "You do not have permission to maintain sites." };
  return { ok: true as const };
}

function apiErrorMessage(error: unknown, operation: string) {
  if (error instanceof SiteApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "unavailable")
      return `The site ${operation} service is temporarily unavailable. Please try again.`;
    return error.message;
  }
  return `The site could not be ${operation}. Please try again.`;
}

function revalidateSiteRoutes() {
  revalidatePath("/validation-data");
  revalidatePath("/validation-data/sites");
  revalidatePath("/Validation/MNT_Site.aspx");
}

export async function createSiteAction(
  _previousState: SiteActionState = initialState,
  formData: FormData,
): Promise<SiteActionState> {
  const access = await authorizeSiteMaintenance();
  if (!access.ok) return { status: "error", message: access.message };
  try {
    await createSite(buildInput(formData, "create"));
  } catch (error) {
    return {
      status: "error",
      message:
        error instanceof SiteValidationError ? error.message : apiErrorMessage(error, "created"),
    };
  }
  revalidateSiteRoutes();
  redirect("/validation-data/sites?saved=created");
}

export async function updateSiteAction(
  _previousState: SiteActionState = initialState,
  formData: FormData,
): Promise<SiteActionState> {
  const access = await authorizeSiteMaintenance();
  let siteCode: number | null;
  try {
    siteCode = getOptionalInteger(formData, "siteCode", "Site code", 32767);
  } catch (error) {
    return {
      status: "error",
      message: error instanceof SiteValidationError ? error.message : "Site code is invalid.",
    };
  }
  const returnPath = siteCode
    ? `/Validation/MNT_Site_Edit.aspx?cmbSite=${siteCode}`
    : "/validation-data/sites";
  if (!access.ok) redirect(`${returnPath}&error=${encodeURIComponent(access.message)}`);
  if (!siteCode) return { status: "error", message: "Site code is required." };
  try {
    await updateSite(siteCode, buildInput(formData, "update"));
  } catch (error) {
    return {
      status: "error",
      message:
        error instanceof SiteValidationError ? error.message : apiErrorMessage(error, "updated"),
    };
  }
  revalidateSiteRoutes();
  redirect("/validation-data/sites?saved=updated");
}

export async function deleteSiteAction(formData: FormData) {
  const access = await authorizeSiteMaintenance();
  let siteCode: number | null;
  try {
    siteCode = getOptionalInteger(formData, "siteCode", "Site code", 32767);
  } catch (error) {
    redirect(
      `/validation-data/sites?error=${encodeURIComponent(error instanceof Error ? error.message : "Site code is invalid.")}`,
    );
  }
  const checkPath = `/Validation/MNT_Site_Del_Check.aspx?code=${siteCode ?? ""}`;
  if (!access.ok) redirect(`${checkPath}&error=${encodeURIComponent(access.message)}`);
  if (!siteCode) redirect("/validation-data/sites?error=Site%20code%20is%20required.");
  let dependencies: Awaited<ReturnType<typeof getSiteDeleteCheck>>;
  try {
    dependencies = await getSiteDeleteCheck(siteCode);
  } catch (error) {
    redirect(`${checkPath}&error=${encodeURIComponent(apiErrorMessage(error, "checked"))}`);
  }
  if (!dependencies.canDelete) {
    redirect(
      `${checkPath}&error=${encodeURIComponent("Contracts issued to this site must be changed before deleting it.")}`,
    );
  }
  try {
    await deleteSite(siteCode);
  } catch (error) {
    redirect(`${checkPath}&error=${encodeURIComponent(apiErrorMessage(error, "deleted"))}`);
  }
  revalidateSiteRoutes();
  redirect("/validation-data/sites?saved=deleted");
}
