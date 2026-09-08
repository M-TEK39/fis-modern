import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type TroubleshootUser = {
  userAccessCode: number;
  name: string;
  siteDescription: string | null;
  firstName: string | null;
  lastName: string | null;
};

export type TroubleshootSiteUser = {
  userAccessCode: number;
  name: string;
  siteDescription: string | null;
  siteCode: number | null;
};

export type TroubleshootLogEntry = {
  id: number;
  vehicleIdentifier: string | null;
  problemDescription: string | null;
  status: string | null;
  loggedDate: string | null;
  loggedBy: string | null;
};

export type TroubleshootOdometerResult = {
  vehicleIdentifier: string | null;
  tripAuthorityNumber: string | null;
  currentOdometer: number | null;
  lastOdometer: number | null;
};

export type TripsWithoutRoutes = {
  tripAuthorityCode: number | null;
  contractCode: number | null;
  issueDate: string | null;
  tripReason: string | null;
  tripRequestNumber: string | null;
  approverName: string | null;
};

export type ApproverRank = {
  id: number;
  rankName: string | null;
  description: string | null;
};

export type VehicleMasterLookup = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  currentOdometer: number | null;
  recoveredGg: string | null;
};

export type TroubleshootApiErrorReason = "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class TroubleshootApiError extends Error {
  constructor(
    public readonly reason: TroubleshootApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "TroubleshootApiError";
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
  return ["true", "1", "yes", "y"].includes(String(value).trim().toLowerCase());
}

function getCollection(value: unknown) {
  if (Array.isArray(value)) return value;
  if (isRecord(value)) {
    const collection = getValue(value, "data", "items", "results");
    return Array.isArray(collection) ? collection : [];
  }
  return [];
}

async function readErrorMessage(response: Response, fallback: string) {
  try {
    const payload = (await response.json()) as unknown;
    return isRecord(payload) ? asString(getValue(payload, "message", "Message", "error")) ?? fallback : fallback;
  } catch {
    return fallback;
  }
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) throw new TroubleshootApiError("unauthorized", "No FIS access cookie is available.");

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers: { accept: "application/json", cookie: cookieHeader, ...(init.body ? { "content-type": "application/json" } : {}), ...init.headers },
      signal: controller.signal,
    });
    if (response.status === 401 || response.status === 403) throw new TroubleshootApiError("unauthorized", "The FIS access cookie was rejected.", response.status);
    if (response.status === 404) throw new TroubleshootApiError("not-found", "The requested Troubleshoot record was not found.", response.status);
    if (!response.ok) throw new TroubleshootApiError(response.status >= 500 ? "unavailable" : "invalid-response", await readErrorMessage(response, `FIS API returned HTTP ${response.status}.`), response.status);
    return response;
  } catch (error) {
    if (error instanceof TroubleshootApiError) throw error;
    throw new TroubleshootApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new TroubleshootApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapUser(value: unknown): TroubleshootUser | null {
  if (!isRecord(value)) return null;
  const userAccessCode = asNumber(getValue(value, "userAccessCode", "UserAccessCode", "user_access_code"));
  if (userAccessCode === null) return null;
  return {
    userAccessCode,
    name: asString(getValue(value, "name", "Name")) ?? "",
    siteDescription: asString(getValue(value, "siteDescription", "SiteDescription", "site_description")),
    firstName: asString(getValue(value, "firstName", "FirstName", "firstname")),
    lastName: asString(getValue(value, "lastName", "LastName", "surname")),
  };
}

function mapSiteUser(value: unknown): TroubleshootSiteUser | null {
  const user = mapUser(value);
  if (!user || !isRecord(value)) return null;
  return {
    ...user,
    siteCode: asNumber(getValue(value, "siteCode", "SiteCode", "site_code")),
  };
}

function mapLog(value: unknown): TroubleshootLogEntry | null {
  if (!isRecord(value)) return null;
  const id = asNumber(getValue(value, "id", "Id", "errorID", "ErrorID"));
  if (id === null) return null;
  return {
    id,
    vehicleIdentifier: asString(getValue(value, "vehicleIdentifier", "VehicleIdentifier")),
    problemDescription: asString(getValue(value, "problemDescription", "ProblemDescription")),
    status: asString(getValue(value, "status", "Status")),
    loggedDate: asString(getValue(value, "loggedDate", "LoggedDate")),
    loggedBy: asString(getValue(value, "loggedBy", "LoggedBy")),
  };
}

function mapOdometer(value: unknown): TroubleshootOdometerResult | null {
  if (!isRecord(value)) return null;
  return {
    vehicleIdentifier: asString(getValue(value, "vehicleIdentifier", "VehicleIdentifier")),
    tripAuthorityNumber: asString(getValue(value, "tripAuthorityNumber", "TripAuthorityNumber")),
    currentOdometer: asNumber(getValue(value, "currentOdometer", "CurrentOdometer")),
    lastOdometer: asNumber(getValue(value, "lastOdometer", "LastOdometer")),
  };
}

function mapTripWithoutRoutes(value: unknown): TripsWithoutRoutes | null {
  if (!isRecord(value)) return null;
  return {
    tripAuthorityCode: asNumber(getValue(value, "tripAuthorityCode", "TripAuthorityCode", "trip_authority_code")),
    contractCode: asNumber(getValue(value, "contractCode", "ContractCode", "contract_code")),
    issueDate: asString(getValue(value, "issueDate", "IssueDate", "issue_date")),
    tripReason: asString(getValue(value, "tripReason", "TripReason", "trip_reason")),
    tripRequestNumber: asString(getValue(value, "tripRequestNumber", "TripRequestNumber", "trip_request_number")),
    approverName: asString(getValue(value, "approverName", "ApproverName", "approver_name")),
  };
}

function mapRank(value: unknown): ApproverRank | null {
  if (!isRecord(value)) return null;
  const id = asNumber(getValue(value, "id", "Id", "rankCode", "rank_code"));
  if (id === null) return null;
  const description = asString(getValue(value, "description", "Description"));
  return { id, rankName: asString(getValue(value, "rankName", "RankName")) ?? description, description };
}

function mapVehicle(value: unknown): VehicleMasterLookup | null {
  if (!isRecord(value)) return null;
  const vmfCode = asNumber(getValue(value, "vmfCode", "VmfCode", "vmf_code"));
  if (vmfCode === null) return null;
  return {
    vmfCode,
    fleetNumber: asString(getValue(value, "fleetNumber", "FleetNumber", "fleet_number")),
    registrationNumber: asString(getValue(value, "registrationNumber", "RegistrationNumber", "registration_number")),
    currentOdometer: asNumber(getValue(value, "currentOdometer", "CurrentOdometer", "current_odo")),
    recoveredGg: asString(getValue(value, "recoveredGg", "RecoveredGg", "recovered_gg")),
  };
}

export async function getTroubleshootUsers() {
  const payload = await readJson(await requestApi("api/troubleshoot/users"));
  return getCollection(payload).map(mapUser).filter((value): value is TroubleshootUser => value !== null);
}

export async function getTroubleshootSiteUsers() {
  const payload = await readJson(await requestApi("api/troubleshoot/departmentsites"));
  return getCollection(payload).map(mapSiteUser).filter((value): value is TroubleshootSiteUser => value !== null);
}

export async function searchTroubleshootLogs(userAccessCode: number) {
  const payload = await readJson(await requestApi("api/troubleshoot/log/search", { method: "POST", body: JSON.stringify({ userAccessCode }) }));
  return getCollection(payload).map(mapLog).filter((value): value is TroubleshootLogEntry => value !== null);
}

export async function updateTroubleshootLogs() {
  const payload = await readJson(await requestApi("api/troubleshoot/log/update", { method: "POST", body: JSON.stringify({}) }));
  return isRecord(payload) ? asNumber(getValue(payload, "updated", "Updated")) ?? 0 : 0;
}

export async function getTroubleshootReports(input: { problemKeyword?: string; userAccessCode?: number; fromDate?: string; toDate?: string; openInExcel?: boolean }) {
  const payload = await readJson(await requestApi("api/troubleshoot/reports/general", { method: "POST", body: JSON.stringify({
    problemKeyword: input.problemKeyword || null,
    userAccessCode: input.userAccessCode ?? null,
    fromDate: input.fromDate || null,
    toDate: input.toDate || null,
    openInExcel: input.openInExcel === true,
  }) }));
  return getCollection(payload).map(mapLog).filter((value): value is TroubleshootLogEntry => value !== null);
}

export async function searchTroubleshootOdometer(input: { searchMode: "GG" | "REG" | "TA"; searchValue: string }) {
  const payload = await readJson(await requestApi("api/troubleshoot/odometer/search", { method: "POST", body: JSON.stringify(input) }));
  return getCollection(payload).map(mapOdometer).filter((value): value is TroubleshootOdometerResult => value !== null);
}

export async function getTripsWithoutRoutes() {
  const payload = await readJson(await requestApi("api/troubleshoot/trips-without-routes"));
  return getCollection(payload).map(mapTripWithoutRoutes).filter((value): value is TripsWithoutRoutes => value !== null);
}

export async function removeTripsWithoutRoutes(input: { fromDate?: string; toDate?: string }) {
  const payload = await readJson(await requestApi("api/troubleshoot/remove-trips-no-routes", { method: "POST", body: JSON.stringify({
    fromDate: input.fromDate || null,
    toDate: input.toDate || null,
  }) }));
  return isRecord(payload) ? asNumber(getValue(payload, "removed", "Removed")) ?? 0 : 0;
}

export async function getApproverRanks() {
  const payload = await readJson(await requestApi("api/authorisers/ranks"));
  return getCollection(payload).map(mapRank).filter((value): value is ApproverRank => value !== null);
}

export async function saveApproverRanks(ranks: { id: number; rankName: string; description: string }[]) {
  const payload = await readJson(await requestApi("api/authorisers/ranks", { method: "POST", body: JSON.stringify(ranks.map((rank) => ({ id: rank.id, rankName: rank.rankName, description: rank.description }))) }));
  return getCollection(payload).map(mapRank).filter((value): value is ApproverRank => value !== null);
}

export async function getVehicleMasterLookup(vehicleIdentifier: string) {
  const payload = await readJson(await requestApi("api/troubleshoot/vehicle-master-edit", { method: "POST", body: JSON.stringify({ vehicleIdentifier }) }));
  return getCollection(payload).map(mapVehicle).filter((value): value is VehicleMasterLookup => value !== null);
}
