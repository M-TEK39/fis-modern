import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type ThirdPartySupplier = {
  supplierId: number;
  name: string | null;
  address: string | null;
  postalAddress: string | null;
  tel: string | null;
  fax: string | null;
  cell: string | null;
  email: string | null;
  contactPerson: string | null;
  notes: string | null;
  serviceCode: string | null;
  active: boolean | null;
};

export type ThirdPartyService = { code: number; name: string };
export type ThirdPartyDepartment = { departmentCode: number; description: string | null };
export type ThirdPartySite = { siteCode: number; description: string | null };

export type ThirdPartyProject = {
  projectId: number;
  departmentCode: number | null;
  siteCode: number | null;
  description: string | null;
  startDate: string | null;
  endDate: string | null;
  responsiblePerson: string | null;
  responsibleAddress: string | null;
  responsiblePostalAddress: string | null;
  responsibleTel: string | null;
  responsibleFax: string | null;
  responsibleEmail: string | null;
  responsibleCell: string | null;
  notes: string | null;
  orderReference: string | null;
  classConfiguration: string | null;
};

export type ThirdPartyVehicle = {
  vehicleId: number;
  registrationNumber: string | null;
  modelDescription: string | null;
  modelYear: string | null;
  chassisNumber: string | null;
};

export type ThirdPartyClassRequirement = { classId: number; className: string | null; requiredCount: number | null };
export type ThirdPartyAllocation = {
  allocationId: number;
  projectId: number;
  supplierId: number | null;
  vehicleId: number | null;
  classId: number | null;
  quantity: number | null;
};

export type ThirdPartySupplierInput = {
  name: string;
  address?: string;
  postal_address?: string;
  tel?: string;
  fax?: string;
  cell?: string;
  email?: string;
  contact_person?: string;
  notes?: string;
  service_code?: string;
  active?: boolean;
  ctg_code?: number;
  is_third_party?: boolean;
};

export type ThirdPartyProjectInput = {
  department_code: number;
  site_code?: number;
  description: string;
  start_date: string;
  end_date: string;
  responsible_person?: string;
  rp_physical_address?: string;
  rp_postal_address?: string;
  rp_tel?: string;
  rp_fax?: string;
  rp_email?: string;
  rp_cell?: string;
  notes?: string;
  order_reference?: string;
  class_configuration?: string;
};

export type ThirdPartyAllocationInput = {
  project_id: number;
  supplier_id: number;
  vehicle_id: number;
  class_id: number;
  quantity?: number;
};

export type ThirdPartyApiErrorReason = "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class ThirdPartyApiError extends Error {
  constructor(public readonly reason: ThirdPartyApiErrorReason, message: string, public readonly status?: number) {
    super(message);
    this.name = "ThirdPartyApiError";
  }
}

function getApiBaseUrl() {
  const value = process.env.API_BASE_URL?.trim() || "http://localhost:5010";
  return `${value.replace(/\/$/, "")}/`;
}

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function getValue(record: JsonRecord, ...keys: string[]) {
  for (const key of keys) if (key in record) return record[key];
  return undefined;
}

function asString(value: unknown) {
  if (typeof value === "string") return value.trim() || null;
  if (typeof value === "number" || typeof value === "bigint") return String(value);
  return null;
}

function asNumber(value: unknown) {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  if (typeof value === "string" && value.trim()) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }
  return null;
}

function asBoolean(value: unknown) {
  if (typeof value === "boolean") return value;
  if (typeof value === "number") return value !== 0;
  if (typeof value === "string") return ["true", "1", "yes", "y"].includes(value.trim().toLowerCase());
  return null;
}

function collection(value: unknown) {
  if (Array.isArray(value)) return value;
  if (isRecord(value)) {
    const nested = getValue(value, "items", "data", "results");
    return Array.isArray(nested) ? nested : [];
  }
  return [];
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookie = await getForwardedAuthCookieHeader();
  if (!cookie) throw new ThirdPartyApiError("unauthorized", "No FIS access cookie is available.");
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers: {
        accept: "application/json",
        cookie,
        ...(init.body ? { "content-type": "application/json" } : {}),
        ...init.headers,
      },
      signal: controller.signal,
    });
    if (response.status === 401 || response.status === 403) throw new ThirdPartyApiError("unauthorized", "The FIS access cookie was rejected.", response.status);
    if (response.status === 404) throw new ThirdPartyApiError("not-found", "The requested third-party record was not found.", response.status);
    if (!response.ok) {
      let message = `FIS API returned HTTP ${response.status}.`;
      try {
        const payload = await response.clone().json();
        if (isRecord(payload)) message = asString(getValue(payload, "message", "Message", "error", "title")) ?? message;
      } catch {
        // Preserve the status message when the API response is not JSON.
      }
      throw new ThirdPartyApiError(response.status >= 500 ? "unavailable" : "invalid-response", message, response.status);
    }
    return response;
  } catch (error) {
    if (error instanceof ThirdPartyApiError) throw error;
    throw new ThirdPartyApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new ThirdPartyApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapSupplier(value: unknown): ThirdPartySupplier | null {
  if (!isRecord(value)) return null;
  const supplierId = asNumber(getValue(value, "supplier_id", "supplierId", "SupplierId"));
  if (supplierId === null) return null;
  return {
    supplierId,
    name: asString(getValue(value, "name", "supplier_name", "supplierName")),
    address: asString(getValue(value, "address", "physical_address", "physicalAddress")),
    postalAddress: asString(getValue(value, "postal_address", "postalAddress")),
    tel: asString(getValue(value, "tel", "phone_number", "tel_number", "phone")),
    fax: asString(getValue(value, "fax", "fax_number")),
    cell: asString(getValue(value, "cell", "cell_number")),
    email: asString(getValue(value, "email", "email_address")),
    contactPerson: asString(getValue(value, "contact_person", "contactPerson")),
    notes: asString(getValue(value, "notes", "Note", "note")),
    serviceCode: asString(getValue(value, "service_code", "serviceCode", "supplier_type", "type_code")),
    active: asBoolean(getValue(value, "active", "is_active", "supplierActive")),
  };
}

function mapProject(value: unknown): ThirdPartyProject | null {
  if (!isRecord(value)) return null;
  const projectId = asNumber(getValue(value, "project_id", "projectId", "ProjectId"));
  if (projectId === null) return null;
  return {
    projectId,
    departmentCode: asNumber(getValue(value, "department_code", "departmentCode", "DepartmentCode")),
    siteCode: asNumber(getValue(value, "site_code", "siteCode", "SiteCode")),
    description: asString(getValue(value, "description", "Project_Description")),
    startDate: asString(getValue(value, "start_date", "Project_Start_Date")),
    endDate: asString(getValue(value, "end_date", "Project_End_Date")),
    responsiblePerson: asString(getValue(value, "responsible_person", "Project_Responsible_Person")),
    responsibleAddress: asString(getValue(value, "rp_physical_address", "RP_Physical_Address")),
    responsiblePostalAddress: asString(getValue(value, "rp_postal_address", "RP_Postal_Address")),
    responsibleTel: asString(getValue(value, "rp_tel", "RP_TelNumber")),
    responsibleFax: asString(getValue(value, "rp_fax", "RP_FaxNumber")),
    responsibleEmail: asString(getValue(value, "rp_email", "RP_Email_Address")),
    responsibleCell: asString(getValue(value, "rp_cell", "RP_CellNumber")),
    notes: asString(getValue(value, "notes", "Notes", "Project_Notes")),
    orderReference: asString(getValue(value, "order_reference", "Client_OrderReference_Number")),
    classConfiguration: asString(getValue(value, "class_configuration", "ClassConfiguration")),
  };
}

function mapVehicle(value: unknown): ThirdPartyVehicle | null {
  if (!isRecord(value)) return null;
  const vehicleId = asNumber(getValue(value, "vehicle_id", "vehicleId", "Third_Party_Vehicle_ID", "vmf_code"));
  if (vehicleId === null) return null;
  return {
    vehicleId,
    registrationNumber: asString(getValue(value, "registration_number", "registrationNumber", "RegistrationNumber")),
    modelDescription: asString(getValue(value, "model_description", "modelDescription", "model_desc", "Third_Party_Model_ID")),
    modelYear: asString(getValue(value, "model_year", "modelYear", "ModelYear", "year_manufactured")),
    chassisNumber: asString(getValue(value, "chassis_number", "chassisNumber", "ChassisNumber")),
  };
}

function mapRequirement(value: unknown): ThirdPartyClassRequirement | null {
  if (!isRecord(value)) return null;
  const classId = asNumber(getValue(value, "class_id", "classId"));
  if (classId === null) return null;
  return { classId, className: asString(getValue(value, "class_name", "className")), requiredCount: asNumber(getValue(value, "required_count", "requiredCount")) };
}

function mapAllocation(value: unknown): ThirdPartyAllocation | null {
  if (!isRecord(value)) return null;
  const allocationId = asNumber(getValue(value, "allocation_id", "allocationId", "Third_Party_Vehicle_AllocationsID"));
  const projectId = asNumber(getValue(value, "project_id", "projectId", "Third_Party_ProjectID"));
  if (allocationId === null || projectId === null) return null;
  return {
    allocationId,
    projectId,
    supplierId: asNumber(getValue(value, "supplier_id", "supplierId", "Third_Party_SupplierID")),
    vehicleId: asNumber(getValue(value, "vehicle_id", "vehicleId", "Third_Party_vs_code")),
    classId: asNumber(getValue(value, "class_id", "classId")),
    quantity: asNumber(getValue(value, "quantity")),
  };
}

export async function getThirdPartySuppliers() {
  return collection(await readJson(await requestApi("api/thirdparty/suppliers"))).map(mapSupplier).filter((item): item is ThirdPartySupplier => item !== null);
}

export async function getThirdPartySupplier(supplierId: number) {
  return mapSupplier(await readJson(await requestApi(`api/thirdparty/suppliers/${encodeURIComponent(supplierId)}`)));
}

export async function saveThirdPartySupplier(supplierId: number | null, input: ThirdPartySupplierInput) {
  const response = await requestApi(supplierId === null ? "api/thirdparty/suppliers" : `api/thirdparty/suppliers/${encodeURIComponent(supplierId)}`, {
    method: supplierId === null ? "POST" : "PUT",
    body: JSON.stringify(input),
  });
  const record = mapSupplier(await readJson(response));
  if (!record) throw new ThirdPartyApiError("invalid-response", "The FIS API returned an invalid supplier.");
  return record;
}

export async function getThirdPartyServices() {
  return collection(await readJson(await requestApi("api/thirdparty/services")))
    .map((value): ThirdPartyService | null => {
      if (!isRecord(value)) return null;
      const code = asNumber(getValue(value, "code", "Code"));
      const name = asString(getValue(value, "name", "Name"));
      return code === null || name === null ? null : { code, name };
    })
    .filter((item): item is ThirdPartyService => item !== null);
}

export async function getThirdPartyProjects(departmentCode?: number) {
  const path = departmentCode === undefined ? "api/thirdparty/projects" : `api/thirdparty/projects/department/${encodeURIComponent(departmentCode)}`;
  return collection(await readJson(await requestApi(path))).map(mapProject).filter((item): item is ThirdPartyProject => item !== null);
}

export async function getThirdPartyProject(projectId: number) {
  return mapProject(await readJson(await requestApi(`api/thirdparty/projects/${encodeURIComponent(projectId)}`)));
}

export async function saveThirdPartyProject(projectId: number | null, input: ThirdPartyProjectInput) {
  const response = await requestApi(projectId === null ? "api/thirdparty/projects" : `api/thirdparty/projects/${encodeURIComponent(projectId)}`, {
    method: projectId === null ? "POST" : "PUT",
    body: JSON.stringify(input),
  });
  const record = mapProject(await readJson(response));
  if (!record) throw new ThirdPartyApiError("invalid-response", "The FIS API returned an invalid project.");
  return record;
}

export async function getThirdPartyDepartments() {
  return collection(await readJson(await requestApi("api/thirdparty/departments")))
    .map((value): ThirdPartyDepartment | null => {
      if (!isRecord(value)) return null;
      const departmentCode = asNumber(getValue(value, "department_code", "departmentCode"));
      return departmentCode === null ? null : { departmentCode, description: asString(getValue(value, "description", "department_description")) };
    })
    .filter((item): item is ThirdPartyDepartment => item !== null);
}

export async function getThirdPartySites(departmentCode: number) {
  return collection(await readJson(await requestApi(`api/thirdparty/sites/${encodeURIComponent(departmentCode)}`)))
    .map((value): ThirdPartySite | null => {
      if (!isRecord(value)) return null;
      const siteCode = asNumber(getValue(value, "site_code", "Site_code", "siteCode"));
      return siteCode === null ? null : { siteCode, description: asString(getValue(value, "description", "Description")) };
    })
    .filter((item): item is ThirdPartySite => item !== null);
}

export async function getThirdPartyVehicles(supplierId: number) {
  return collection(await readJson(await requestApi(`api/thirdparty/vehicles/${encodeURIComponent(supplierId)}`))).map(mapVehicle).filter((item): item is ThirdPartyVehicle => item !== null);
}

export async function getThirdPartyRequirements(projectId: number) {
  return collection(await readJson(await requestApi(`api/thirdparty/projects/${encodeURIComponent(projectId)}/requirements`))).map(mapRequirement).filter((item): item is ThirdPartyClassRequirement => item !== null);
}

export async function getThirdPartyAllocations(projectId: number) {
  return collection(await readJson(await requestApi(`api/thirdparty/allocations/project/${encodeURIComponent(projectId)}`))).map(mapAllocation).filter((item): item is ThirdPartyAllocation => item !== null);
}

export async function createThirdPartyAllocation(input: ThirdPartyAllocationInput) {
  const record = mapAllocation(await readJson(await requestApi("api/thirdparty/allocations", { method: "POST", body: JSON.stringify(input) })));
  if (!record) throw new ThirdPartyApiError("invalid-response", "The FIS API returned an invalid allocation.");
  return record;
}

export async function deleteThirdPartyAllocation(allocationId: number) {
  await requestApi(`api/thirdparty/allocations/${encodeURIComponent(allocationId)}`, { method: "DELETE" });
}
