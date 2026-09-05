import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type UserAdminProfile = {
  userAccessCode: number;
  userName: string | null;
  firstName: string | null;
  lastName: string | null;
  email: string | null;
  telephone: string | null;
  siteCode: number | null;
  siteName: string | null;
  positionCode: number | null;
  positionName: string | null;
  persalNumber: number | null;
  contractNumber: number | null;
  saIdNumber: number | null;
  passportNumber: number | null;
  cellphoneNumber: number | null;
  faxNumber: number | null;
  userStatus: string | null;
  accessLevel: number;
  userActive: boolean;
  lastLogOn: string | null;
};

export type UserAdminApiErrorReason = "unauthorized" | "unavailable" | "invalid-response";

export class UserAdminApiError extends Error {
  constructor(
    public readonly reason: UserAdminApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "UserAdminApiError";
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

async function requestApi(path: string) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new UserAdminApiError("unauthorized", "No FIS access cookie is available.");
  }

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);

  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      cache: "no-store",
      headers: {
        accept: "application/json",
        cookie: cookieHeader,
      },
      signal: controller.signal,
    });

    if (response.status === 401 || response.status === 403) {
      throw new UserAdminApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (!response.ok) {
      throw new UserAdminApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        `FIS API returned HTTP ${response.status}.`,
      );
    }

    return response;
  } catch (error) {
    if (error instanceof UserAdminApiError) {
      throw error;
    }

    throw new UserAdminApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new UserAdminApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapProfile(value: unknown): UserAdminProfile | null {
  if (!isRecord(value)) {
    return null;
  }

  const userAccessCode = asNumber(getValue(value, "userAccessCode", "UserAccessCode", "user_access_code"));
  if (userAccessCode === null) {
    return null;
  }

  return {
    userAccessCode,
    userName: asString(getValue(value, "userName", "UserName", "name")),
    firstName: asString(getValue(value, "firstName", "FirstName")),
    lastName: asString(getValue(value, "lastName", "LastName")),
    email: asString(getValue(value, "email", "Email")),
    telephone: asString(getValue(value, "telephone", "Telephone")),
    siteCode: asNumber(getValue(value, "siteCode", "SiteCode", "Site_code")),
    siteName: asString(getValue(value, "siteName", "SiteName", "site_name")),
    positionCode: asNumber(getValue(value, "positionCode", "PositionCode", "Position_Code")),
    positionName: asString(getValue(value, "positionName", "PositionName", "position_name")),
    persalNumber: asNumber(getValue(value, "persalNumber", "PersalNumber", "Persal_Number")),
    contractNumber: asNumber(getValue(value, "contractNumber", "ContractNumber", "Contract_Number")),
    saIdNumber: asNumber(getValue(value, "saIdNumber", "SaIdNumber", "sa_id_number")),
    passportNumber: asNumber(getValue(value, "passportNumber", "PassportNumber", "passport_number")),
    cellphoneNumber: asNumber(getValue(value, "cellphoneNumber", "CellphoneNumber", "Cellphone_Number")),
    faxNumber: asNumber(getValue(value, "faxNumber", "FaxNumber", "Fax_Number")),
    userStatus: asString(getValue(value, "userStatus", "UserStatus", "user_status")),
    accessLevel: asNumber(getValue(value, "accessLevel", "AccessLevel")) ?? 0,
    userActive: asBoolean(getValue(value, "userActive", "UserActive", "user_active")),
    lastLogOn: asString(getValue(value, "lastLogOn", "LastLogOn", "last_log_on")),
  };
}

export async function getUserAdminProfiles(alphabet: string) {
  const response = await requestApi(`api/userprofile/administration?alphabet=${encodeURIComponent(alphabet)}`);
  const profiles = getCollection(await readJson(response))
    .map(mapProfile)
    .filter((profile): profile is UserAdminProfile => profile !== null);

  return profiles.sort(
    (left, right) =>
      (left.lastName ?? "").localeCompare(right.lastName ?? "") ||
      (left.firstName ?? "").localeCompare(right.firstName ?? "") ||
      left.userAccessCode - right.userAccessCode,
  );
}
