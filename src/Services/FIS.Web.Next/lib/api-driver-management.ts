import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type DriverManagementDepartment = {
  code: number;
  description: string;
};

export type DriverManagementSite = {
  code: number;
  departmentCode: number | null;
  description: string;
};

export type DriverManagementAuthoriser = {
  authoriserCode: number;
  rankCode: number;
  surname: string | null;
  firstname: string | null;
  persalNumber: string | null;
  telephoneNumber: string | null;
  isActive: boolean;
  siteCode: number | null;
  departmentCode: number;
  legacyFieldsAvailable: boolean;
};

export type DriverManagementRank = {
  id: number;
  description: string | null;
};

export type DriverManagementAuthoriserInput = {
  rankCode: number;
  surname: string;
  firstname: string;
  persalNumber: string | null;
  telephoneNumber: string | null;
  isActive: boolean;
  siteCode: number;
  departmentCode: number;
};

export type DriverManagementDriver = {
  siteDriverCode: number;
  siteCode: number;
  driverLicenceTypeId: number;
  driverSurname: string | null;
  driverFirstname: string | null;
  driverSAId: string | null;
  driverPassportNumber: string | null;
  driverPersonalNumber: string | null;
  driverContractNumber: string | null;
  driverLicenceNumber: string | null;
  driverLicenceIssueDate: string | null;
  driverLicenceLastVerifiedDate: string | null;
  driverHasPDP: boolean;
  driverPDPExpiryDate: string | null;
  driverLicenceExpiryDate: string | null;
  driverActive: boolean;
};

export type DriverManagementDriverInput = {
  siteCode: number;
  driverLicenceTypeId: number;
  driverSurname: string;
  driverFirstname: string;
  driverSAId: string | null;
  driverPassportNumber: string | null;
  driverPersonalNumber: string | null;
  driverContractNumber: string | null;
  driverLicenceNumber: string;
  driverLicenceIssueDate: string;
  driverLicenceLastVerifiedDate: string;
  driverHasPDP: boolean;
  driverPDPExpiryDate: string | null;
  driverLicenceExpiryDate: string | null;
  driverActive: boolean;
};

export type DriverManagementLicenceType = {
  id: number;
  code: string | null;
  description: string | null;
};

export type DriverManagementApiErrorReason = "unauthorized" | "unavailable" | "invalid-response" | "not-found" | "rejected";

export class DriverManagementApiError extends Error {
  constructor(
    public readonly reason: DriverManagementApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "DriverManagementApiError";
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
  for (const key of keys) {
    if (key in record) {
      return record[key];
    }
  }

  return undefined;
}

function asString(value: unknown) {
  if (typeof value === "string") {
    return value.trim() || null;
  }

  if (typeof value === "number" || typeof value === "bigint") {
    return String(value);
  }

  return null;
}

function asNumber(value: unknown) {
  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }

  if (typeof value === "string" && value.trim()) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }

  return null;
}

function asBoolean(value: unknown) {
  if (typeof value === "boolean") {
    return value;
  }

  if (typeof value === "number") {
    return value !== 0;
  }

  return ["true", "1", "yes", "y"].includes(String(value).trim().toLowerCase());
}

function getCollection(value: unknown) {
  if (Array.isArray(value)) {
    return value;
  }

  if (isRecord(value)) {
    const nested = getValue(value, "items", "data", "results");
    return Array.isArray(nested) ? nested : [];
  }

  return [];
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new DriverManagementApiError("unauthorized", "No FIS access cookie is available.");
  }

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);

  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers: {
        accept: "application/json",
        cookie: cookieHeader,
        ...init.headers,
      },
      signal: controller.signal,
    });

    if (response.status === 401 || response.status === 403) {
      throw new DriverManagementApiError("unauthorized", "The FIS access cookie was rejected.", response.status);
    }

    if (!response.ok) {
      let message = `FIS API returned HTTP ${response.status}.`;
      try {
        const payload = await response.clone().json();
        if (isRecord(payload)) {
          message = asString(getValue(payload, "message", "Message", "error")) ?? message;
        }
      } catch {
        // Keep the status-based message when the error body is not JSON.
      }

      throw new DriverManagementApiError(
        response.status === 404 ? "not-found" : response.status === 400 || response.status === 409 ? "rejected" : response.status >= 500 ? "unavailable" : "invalid-response",
        message,
        response.status,
      );
    }

    return response;
  } catch (error) {
    if (error instanceof DriverManagementApiError) {
      throw error;
    }

    throw new DriverManagementApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new DriverManagementApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapDepartment(value: unknown): DriverManagementDepartment | null {
  if (!isRecord(value)) {
    return null;
  }

  const code = asNumber(getValue(value, "code", "Code", "departmentCode", "DepartmentCode", "department_code"));
  const description = asString(getValue(value, "description", "Description", "departmentDescription", "department_description"));
  return code !== null && description ? { code, description } : null;
}

function mapSite(value: unknown): DriverManagementSite | null {
  if (!isRecord(value)) {
    return null;
  }

  const code = asNumber(getValue(value, "code", "Code", "siteCode", "SiteCode", "Site_code"));
  const departmentCode = asNumber(getValue(value, "departmentCode", "DepartmentCode", "Depatrment_code"));
  const description = asString(getValue(value, "description", "Description"));
  return code !== null && description ? { code, departmentCode, description } : null;
}

function mapAuthoriser(value: unknown): DriverManagementAuthoriser | null {
  if (!isRecord(value)) {
    return null;
  }

  const authoriserCode = asNumber(getValue(value, "authoriserCode", "AuthoriserCode", "approver_code"));
  if (authoriserCode === null) {
    return null;
  }

  return {
    authoriserCode,
    rankCode: asNumber(getValue(value, "rankCode", "RankCode", "rank_code")) ?? 0,
    surname: asString(getValue(value, "surname", "Surname")),
    firstname: asString(getValue(value, "firstname", "Firstname", "firstName", "FirstName")),
    persalNumber: asString(getValue(value, "persalNumber", "PersalNumber")),
    telephoneNumber: asString(getValue(value, "telephoneNumber", "TelephoneNumber", "telephone")),
    isActive: asBoolean(getValue(value, "isActive", "IsActive", "approver_active")),
    siteCode: asNumber(getValue(value, "siteCode", "SiteCode", "site_code")),
    departmentCode: asNumber(getValue(value, "departmentCode", "DepartmentCode", "department_code")) ?? 0,
    legacyFieldsAvailable: asBoolean(getValue(value, "legacyFieldsAvailable", "LegacyFieldsAvailable")),
  };
}

function mapRank(value: unknown): DriverManagementRank | null {
  if (!isRecord(value)) {
    return null;
  }

  const id = asNumber(getValue(value, "id", "Id", "rankCode", "RankCode", "rank_code"));
  return id === null ? null : { id, description: asString(getValue(value, "description", "Description")) };
}

function mapDriver(value: unknown): DriverManagementDriver | null {
  if (!isRecord(value)) {
    return null;
  }

  const siteDriverCode = asNumber(getValue(value, "siteDriverCode", "SiteDriverCode", "site_driver_code"));
  if (siteDriverCode === null) {
    return null;
  }

  return {
    siteDriverCode,
    siteCode: asNumber(getValue(value, "siteCode", "SiteCode", "site_code")) ?? 0,
    driverLicenceTypeId: asNumber(getValue(value, "driverLicenceTypeId", "DriverLicenceTypeId", "driver_licence_type_id")) ?? 0,
    driverSurname: asString(getValue(value, "driverSurname", "DriverSurname", "driver_surname")),
    driverFirstname: asString(getValue(value, "driverFirstname", "DriverFirstname", "driver_firstname")),
    driverSAId: asString(getValue(value, "driverSAId", "DriverSAId", "driver_SA_id", "southAfricanId")),
    driverPassportNumber: asString(getValue(value, "driverPassportNumber", "DriverPassportNumber", "driver_passportnumber", "passportNumber")),
    driverPersonalNumber: asString(getValue(value, "driverPersonalNumber", "DriverPersonalNumber", "driver_persalnumber", "persalNumber")),
    driverContractNumber: asString(getValue(value, "driverContractNumber", "DriverContractNumber", "driver_contractnumber", "contractNumber")),
    driverLicenceNumber: asString(getValue(value, "driverLicenceNumber", "DriverLicenceNumber", "driver_licence_number", "licenceNumber")),
    driverLicenceIssueDate: asString(getValue(value, "driverLicenceIssueDate", "DriverLicenceIssueDate", "driver_licence_issuedate", "licenceIssueDate")),
    driverLicenceLastVerifiedDate: asString(getValue(value, "driverLicenceLastVerifiedDate", "DriverLicenceLastVerifiedDate", "driver_licence_lastVerifiedDate", "licenceLastVerifiedDate")),
    driverHasPDP: asBoolean(getValue(value, "driverHasPDP", "DriverHasPDP", "driver_hasPDP")),
    driverPDPExpiryDate: asString(getValue(value, "driverPDPExpiryDate", "DriverPDPExpiryDate", "driver_PDP_ExpiryDate", "pdpExpiryDate")),
    driverLicenceExpiryDate: asString(getValue(value, "driverLicenceExpiryDate", "DriverLicenceExpiryDate", "driver_licence_ExpiryDate", "licenceExpiryDate")),
    driverActive: asBoolean(getValue(value, "driverActive", "DriverActive", "driver_active")),
  };
}

function mapLicenceType(value: unknown): DriverManagementLicenceType | null {
  if (!isRecord(value)) {
    return null;
  }

  const id = asNumber(getValue(value, "id", "Id", "driverLicenceTypeId", "driver_licence_type_id"));
  return id === null
    ? null
    : {
        id,
        code: asString(getValue(value, "code", "Code", "driverLicenceTypeCode", "driver_licence_type_code")),
        description: asString(getValue(value, "description", "Description", "driverLicenceTypeDescription", "driver_licence_type_description")),
      };
}

function mapCollection<T>(payload: unknown, mapper: (value: unknown) => T | null) {
  return getCollection(payload).map(mapper).filter((value): value is T => value !== null);
}

export async function getDriverManagementDepartments() {
  const response = await requestApi("api/department");
  return mapCollection(await readJson(response), mapDepartment).sort((left, right) => left.description.localeCompare(right.description));
}

export async function getDriverManagementSites() {
  const response = await requestApi("api/site");
  return mapCollection(await readJson(response), mapSite).sort((left, right) => left.description.localeCompare(right.description));
}

export async function getDriverManagementAuthorisers(siteCode: number) {
  const response = await requestApi(`api/authorisers?siteCode=${encodeURIComponent(siteCode)}`);
  return mapCollection(await readJson(response), mapAuthoriser).sort(
    (left, right) => (left.surname ?? "").localeCompare(right.surname ?? "") || (left.firstname ?? "").localeCompare(right.firstname ?? "") || left.authoriserCode - right.authoriserCode,
  );
}

export async function getDriverManagementAuthoriser(authoriserCode: number) {
  try {
    const response = await requestApi(`api/authorisers/${encodeURIComponent(authoriserCode)}`);
    return mapAuthoriser(await readJson(response));
  } catch (error) {
    if (error instanceof DriverManagementApiError && error.reason === "not-found") {
      return null;
    }

    throw error;
  }
}

export async function getDriverManagementRanks() {
  const response = await requestApi("api/authorisers/ranks");
  return mapCollection(await readJson(response), mapRank).sort((left, right) => (left.description ?? "").localeCompare(right.description ?? "") || left.id - right.id);
}

export async function getDriverManagementSiteDrivers(siteCode: number) {
  const response = await requestApi(`api/site-drivers?siteCode=${encodeURIComponent(siteCode)}`);
  return mapCollection(await readJson(response), mapDriver).sort(
    (left, right) => (left.driverSurname ?? "").localeCompare(right.driverSurname ?? "") || (left.driverFirstname ?? "").localeCompare(right.driverFirstname ?? "") || left.siteDriverCode - right.siteDriverCode,
  );
}

export async function getDriverManagementSiteDriver(siteDriverCode: number) {
  try {
    const response = await requestApi(`api/site-drivers/${encodeURIComponent(siteDriverCode)}`);
    return mapDriver(await readJson(response));
  } catch (error) {
    if (error instanceof DriverManagementApiError && error.reason === "not-found") {
      return null;
    }

    throw error;
  }
}

export async function getDriverManagementLicenceTypes() {
  const response = await requestApi("api/site-drivers/licence-types");
  return mapCollection(await readJson(response), mapLicenceType).sort((left, right) => (left.description ?? "").localeCompare(right.description ?? "") || left.id - right.id);
}

async function runMutation(path: string, method: "POST" | "PUT" | "DELETE", body?: unknown) {
  try {
    const response = await requestApi(path, {
      method,
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
      headers: body === undefined ? undefined : { "content-type": "application/json" },
    });
    return { ok: true as const, payload: response.status === 204 ? null : await readJson(response) };
  } catch (error) {
    if (error instanceof DriverManagementApiError) {
      return { ok: false as const, error };
    }

    return { ok: false as const, error: new DriverManagementApiError("unavailable", "The FIS API could not be reached.") };
  }
}

export async function createDriverManagementAuthoriser(input: DriverManagementAuthoriserInput) {
  return runMutation("api/authorisers", "POST", {
    RankCode: input.rankCode,
    Surname: input.surname,
    Firstname: input.firstname,
    PersalNumber: input.persalNumber,
    TelephoneNumber: input.telephoneNumber,
    IsActive: input.isActive,
    SiteCode: input.siteCode,
    DepartmentCode: input.departmentCode,
  });
}

export async function updateDriverManagementAuthoriser(authoriserCode: number, input: DriverManagementAuthoriserInput) {
  return runMutation(`api/authorisers/${encodeURIComponent(authoriserCode)}`, "PUT", {
    RankCode: input.rankCode,
    Surname: input.surname,
    Firstname: input.firstname,
    PersalNumber: input.persalNumber,
    TelephoneNumber: input.telephoneNumber,
    IsActive: input.isActive,
    SiteCode: input.siteCode,
    DepartmentCode: input.departmentCode,
  });
}

export async function deleteDriverManagementAuthoriser(authoriserCode: number) {
  return runMutation(`api/authorisers/${encodeURIComponent(authoriserCode)}`, "DELETE");
}

export async function createDriverManagementSiteDriver(input: DriverManagementDriverInput) {
  return runMutation("api/site-drivers", "POST", input);
}

export async function updateDriverManagementSiteDriver(siteDriverCode: number, input: DriverManagementDriverInput) {
  return runMutation(`api/site-drivers/${encodeURIComponent(siteDriverCode)}`, "PUT", input);
}

export async function deleteDriverManagementSiteDriver(siteDriverCode: number) {
  return runMutation(`api/site-drivers/${encodeURIComponent(siteDriverCode)}`, "DELETE");
}
