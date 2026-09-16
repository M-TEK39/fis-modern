"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  createThirdPartyAllocation,
  deleteThirdPartyAllocation,
  saveThirdPartyProject,
  saveThirdPartySupplier,
  ThirdPartyApiError,
  type ThirdPartyAllocationInput,
  type ThirdPartyProjectInput,
  type ThirdPartySupplierInput,
} from "@/lib/api/fleet-operations/api-third-party";
import { getSession } from "@/lib/auth/session";

const THIRD_PARTY_ROLE = "Third Party Rental";

function text(formData: FormData, name: string) {
  const value = formData.get(name);
  return typeof value === "string" ? value.trim() : "";
}

function optionalText(formData: FormData, name: string) {
  const value = text(formData, name);
  return value || undefined;
}

function integer(formData: FormData, name: string, label: string): number;
function integer(
  formData: FormData,
  name: string,
  label: string,
  required: false,
): number | undefined;
function integer(
  formData: FormData,
  name: string,
  label: string,
  required = true,
): number | undefined {
  const value = text(formData, name);
  if (!value && !required) return undefined;
  const parsed = Number(value);
  if (!value || !Number.isSafeInteger(parsed) || parsed <= 0)
    throw new Error(`${label} is required.`);
  return parsed;
}

function date(formData: FormData, name: string, label: string) {
  const value = text(formData, name);
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) throw new Error(`${label} is required.`);
  return value;
}

function redirectError(path: string, message: string): never {
  redirect(`${path}?error=${encodeURIComponent(message)}`);
}

function apiMessage(error: unknown, operation: string) {
  if (error instanceof ThirdPartyApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "not-found") return "The selected third-party record no longer exists.";
    if (error.reason === "unavailable")
      return `The third-party ${operation} service is temporarily unavailable. Please try again.`;
    return error.message;
  }
  return `The third-party ${operation} could not be completed. Please try again.`;
}

async function authorize() {
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
  const hasRole = session.roles.some(
    (role) => role.localeCompare(THIRD_PARTY_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
  return hasRole
    ? { ok: true as const }
    : {
        ok: false as const,
        message: "You do not have permission to maintain third-party rentals.",
      };
}

function revalidateThirdParty() {
  revalidatePath("/third-party");
  revalidatePath("/ThirdParty/Rental.aspx");
}

export async function saveThirdPartySupplierAction(formData: FormData) {
  const access = await authorize();
  if (!access.ok) redirectError("/third-party", access.message);
  let supplierId: number | null = null;
  try {
    supplierId = integer(formData, "supplierId", "Supplier", false) ?? null;
    const name = text(formData, "name");
    if (!name) throw new Error("Supplier name is required.");
    const input: ThirdPartySupplierInput = {
      name,
      address: optionalText(formData, "address"),
      postal_address: optionalText(formData, "postalAddress"),
      tel: optionalText(formData, "tel"),
      fax: optionalText(formData, "fax"),
      cell: optionalText(formData, "cell"),
      email: optionalText(formData, "email"),
      contact_person: optionalText(formData, "contactPerson"),
      notes: optionalText(formData, "notes"),
      service_code: optionalText(formData, "serviceCode"),
      active: text(formData, "active") !== "0",
      ctg_code: integer(formData, "ctgCode", "Category", false),
      is_third_party: true,
    };
    await saveThirdPartySupplier(supplierId, input);
  } catch (error) {
    redirectError(
      "/third-party?tab=suppliers",
      error instanceof Error && !(error instanceof ThirdPartyApiError)
        ? error.message
        : apiMessage(error, "supplier save"),
    );
  }
  revalidateThirdParty();
  redirect(`/third-party?tab=suppliers&saved=${supplierId === null ? "created" : "updated"}`);
}

export async function saveThirdPartyProjectAction(formData: FormData) {
  const access = await authorize();
  if (!access.ok) redirectError("/third-party", access.message);
  let projectId: number | null = null;
  try {
    projectId = integer(formData, "projectId", "Project", false) ?? null;
    const departmentCode = integer(formData, "departmentCode", "Department");
    const description = text(formData, "description");
    if (!description) throw new Error("Project description is required.");
    const input: ThirdPartyProjectInput = {
      department_code: departmentCode,
      site_code: integer(formData, "siteCode", "Site", false),
      description,
      start_date: date(formData, "startDate", "Project start date"),
      end_date: date(formData, "endDate", "Project end date"),
      responsible_person: optionalText(formData, "responsiblePerson"),
      rp_physical_address: optionalText(formData, "responsibleAddress"),
      rp_postal_address: optionalText(formData, "responsiblePostalAddress"),
      rp_tel: optionalText(formData, "responsibleTel"),
      rp_fax: optionalText(formData, "responsibleFax"),
      rp_email: optionalText(formData, "responsibleEmail"),
      rp_cell: optionalText(formData, "responsibleCell"),
      notes: optionalText(formData, "notes"),
      order_reference: optionalText(formData, "orderReference"),
      class_configuration: optionalText(formData, "classConfiguration"),
    };
    if (input.end_date < input.start_date)
      throw new Error("Project end date cannot be earlier than its start date.");
    await saveThirdPartyProject(projectId, input);
  } catch (error) {
    redirectError(
      "/third-party?tab=projects",
      error instanceof Error && !(error instanceof ThirdPartyApiError)
        ? error.message
        : apiMessage(error, "project save"),
    );
  }
  revalidateThirdParty();
  redirect(`/third-party?tab=projects&saved=${projectId === null ? "created" : "updated"}`);
}

export async function createThirdPartyAllocationAction(formData: FormData) {
  const access = await authorize();
  if (!access.ok) redirectError("/third-party", access.message);
  let input: ThirdPartyAllocationInput | null = null;
  try {
    input = {
      project_id: integer(formData, "projectId", "Project"),
      supplier_id: integer(formData, "supplierId", "Supplier"),
      vehicle_id: integer(formData, "vehicleId", "Vehicle"),
      class_id: integer(formData, "classId", "Class"),
      quantity: integer(formData, "quantity", "Quantity", false) ?? 1,
    };
    await createThirdPartyAllocation(input!);
  } catch (error) {
    redirectError(
      "/third-party?tab=allocation",
      error instanceof Error && !(error instanceof ThirdPartyApiError)
        ? error.message
        : apiMessage(error, "allocation"),
    );
  }
  revalidateThirdParty();
  redirect(
    `/third-party?tab=allocation&departmentCode=${encodeURIComponent(text(formData, "departmentCode"))}&projectId=${input!.project_id}&supplierId=${input!.supplier_id}&saved=allocated`,
  );
}

export async function deleteThirdPartyAllocationAction(formData: FormData) {
  const access = await authorize();
  if (!access.ok) redirectError("/third-party", access.message);
  let allocationId = 0;
  try {
    allocationId = integer(formData, "allocationId", "Allocation");
    await deleteThirdPartyAllocation(allocationId);
  } catch (error) {
    redirectError(
      "/third-party?tab=allocation",
      error instanceof Error && !(error instanceof ThirdPartyApiError)
        ? error.message
        : apiMessage(error, "allocation deletion"),
    );
  }
  revalidateThirdParty();
  redirect(
    `/third-party?tab=allocation&projectId=${encodeURIComponent(text(formData, "projectId"))}&deleted=1`,
  );
}
