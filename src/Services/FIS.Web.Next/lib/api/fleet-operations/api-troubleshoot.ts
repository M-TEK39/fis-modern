import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
export const DEFAULT_TROUBLESHOOT_PAGE_SIZE = 24;
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

export type TroubleshootPage<T> = {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
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

export type TroubleshootApiErrorReason =
  "unauthorized" | "unavailable" | "invalid-response" | "not-found";

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
    return isRecord(payload)
      ? (asString(getValue(payload, "message", "Message", "error")) ?? fallback)
      : fallback;
  } catch {
    return fallback;
  }
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader)
    throw new TroubleshootApiError("unauthorized", "No FIS access cookie is available.");

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers: {
        accept: "application/json",
        cookie: cookieHeader,
        ...(init.body ? { "content-type": "application/json" } : {}),
        ...init.headers,
      },
      signal: controller.signal,
    });
    if (response.status === 401 || response.status === 403)
      throw new TroubleshootApiError(
        "unauthorized",
        "The FIS access cookie was rejected.",
        response.status,
      );
    if (response.status === 404)
      throw new TroubleshootApiError(
        "not-found",
        "The requested Troubleshoot record was not found.",
        response.status,
      );
    if (!response.ok)
      throw new TroubleshootApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        await readErrorMessage(response, `FIS API returned HTTP ${response.status}.`),
        response.status,
      );
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
  const userAccessCode = asNumber(
    getValue(value, "userAccessCode", "UserAccessCode", "user_access_code"),
  );
  if (userAccessCode === null) return null;
  return {
    userAccessCode,
    name: asString(getValue(value, "name", "Name")) ?? "",
    siteDescription: asString(
      getValue(value, "siteDescription", "SiteDescription", "site_description"),
    ),
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
    tripAuthorityCode: asNumber(
      getValue(value, "tripAuthorityCode", "TripAuthorityCode", "trip_authority_code"),
    ),
    contractCode: asNumber(getValue(value, "contractCode", "ContractCode", "contract_code")),
    issueDate: asString(getValue(value, "issueDate", "IssueDate", "issue_date")),
    tripReason: asString(getValue(value, "tripReason", "TripReason", "trip_reason")),
    tripRequestNumber: asString(
      getValue(value, "tripRequestNumber", "TripRequestNumber", "trip_request_number"),
    ),
    approverName: asString(getValue(value, "approverName", "ApproverName", "approver_name")),
  };
}

function mapRank(value: unknown): ApproverRank | null {
  if (!isRecord(value)) return null;
  const id = asNumber(getValue(value, "id", "Id", "rankCode", "rank_code"));
  if (id === null) return null;
  const description = asString(getValue(value, "description", "Description"));
  return {
    id,
    rankName: asString(getValue(value, "rankName", "RankName")) ?? description,
    description,
  };
}

function mapVehicle(value: unknown): VehicleMasterLookup | null {
  if (!isRecord(value)) return null;
  const vmfCode = asNumber(getValue(value, "vmfCode", "VmfCode", "vmf_code"));
  if (vmfCode === null) return null;
  return {
    vmfCode,
    fleetNumber: asString(getValue(value, "fleetNumber", "FleetNumber", "fleet_number")),
    registrationNumber: asString(
      getValue(value, "registrationNumber", "RegistrationNumber", "registration_number"),
    ),
    currentOdometer: asNumber(getValue(value, "currentOdometer", "CurrentOdometer", "current_odo")),
    recoveredGg: asString(getValue(value, "recoveredGg", "RecoveredGg", "recovered_gg")),
  };
}

function readPageMetadata(payload: JsonRecord) {
  const page = asNumber(getValue(payload, "page", "Page"));
  const pageSize = asNumber(getValue(payload, "pageSize", "PageSize", "page_size"));
  const total = asNumber(getValue(payload, "total", "Total"));
  const totalPages = asNumber(getValue(payload, "totalPages", "TotalPages", "total_pages"));
  if (
    page === null ||
    pageSize === null ||
    total === null ||
    totalPages === null ||
    !Number.isInteger(page) ||
    !Number.isInteger(pageSize) ||
    !Number.isInteger(total) ||
    !Number.isInteger(totalPages) ||
    page < 1 ||
    pageSize < 1 ||
    total < 0 ||
    totalPages < 1
  ) {
    return null;
  }
  return { page, pageSize, total, totalPages };
}

async function readTroubleshootPage<T>(
  response: Response,
  mapItem: (value: unknown) => T | null,
  label: string,
): Promise<TroubleshootPage<T>> {
  const payload = await readJson(response);
  if (!isRecord(payload) || !Array.isArray(payload.items))
    throw new TroubleshootApiError("invalid-response", `The FIS API returned an invalid ${label}.`);

  const metadata = readPageMetadata(payload);
  if (!metadata)
    throw new TroubleshootApiError(
      "invalid-response",
      `The FIS API returned incomplete ${label} pagination metadata.`,
    );

  return {
    items: payload.items.map(mapItem).filter((item): item is T => item !== null),
    ...metadata,
  };
}

function normalizePage(value: number | undefined) {
  return Number.isSafeInteger(value) && (value ?? 0) > 0 ? (value ?? 1) : 1;
}

function normalizePageSize(value: number | undefined) {
  const pageSize =
    Number.isSafeInteger(value) && (value ?? 0) > 0
      ? (value ?? DEFAULT_TROUBLESHOOT_PAGE_SIZE)
      : DEFAULT_TROUBLESHOOT_PAGE_SIZE;
  return Math.min(100, pageSize);
}

export async function getTroubleshootUsers() {
  const payload = await readJson(await requestApi("api/troubleshoot/users"));
  return getCollection(payload)
    .map(mapUser)
    .filter((value): value is TroubleshootUser => value !== null);
}

export async function getTroubleshootUsersPage(page?: number, pageSize?: number) {
  const query = new URLSearchParams({
    page: String(normalizePage(page)),
    pageSize: String(normalizePageSize(pageSize)),
  });
  return readTroubleshootPage(
    await requestApi(`api/troubleshoot/users/page?${query.toString()}`),
    mapUser,
    "Troubleshoot user page",
  );
}

export async function getTroubleshootSiteUsers() {
  const payload = await readJson(await requestApi("api/troubleshoot/departmentsites"));
  return getCollection(payload)
    .map(mapSiteUser)
    .filter((value): value is TroubleshootSiteUser => value !== null);
}

export async function searchTroubleshootLogs(userAccessCode: number) {
  const payload = await readJson(
    await requestApi("api/troubleshoot/log/search", {
      method: "POST",
      body: JSON.stringify({ userAccessCode }),
    }),
  );
  return getCollection(payload)
    .map(mapLog)
    .filter((value): value is TroubleshootLogEntry => value !== null);
}

export async function searchTroubleshootLogsPage(input: {
  userAccessCode: number;
  page?: number;
  pageSize?: number;
}): Promise<TroubleshootPage<TroubleshootLogEntry>> {
  const payload = await requestApi("api/troubleshoot/log/search/page", {
    method: "POST",
    body: JSON.stringify({
      userAccessCode: input.userAccessCode,
      page: normalizePage(input.page),
      pageSize: normalizePageSize(input.pageSize),
    }),
  });
  return readTroubleshootPage(payload, mapLog, "Troubleshoot log page");
}

export async function updateTroubleshootLogs() {
  const payload = await readJson(
    await requestApi("api/troubleshoot/log/update", { method: "POST", body: JSON.stringify({}) }),
  );
  return isRecord(payload) ? (asNumber(getValue(payload, "updated", "Updated")) ?? 0) : 0;
}

export async function getTroubleshootReports(input: {
  problemKeyword?: string;
  userAccessCode?: number;
  fromDate?: string;
  toDate?: string;
  openInExcel?: boolean;
}) {
  const payload = await readJson(
    await requestApi("api/troubleshoot/reports/general", {
      method: "POST",
      body: JSON.stringify({
        problemKeyword: input.problemKeyword || null,
        userAccessCode: input.userAccessCode ?? null,
        fromDate: input.fromDate || null,
        toDate: input.toDate || null,
        openInExcel: input.openInExcel === true,
      }),
    }),
  );
  return getCollection(payload)
    .map(mapLog)
    .filter((value): value is TroubleshootLogEntry => value !== null);
}

export async function getTroubleshootReportsPage(input: {
  problemKeyword?: string;
  userAccessCode?: number;
  fromDate?: string;
  toDate?: string;
  openInExcel?: boolean;
  page?: number;
  pageSize?: number;
}): Promise<TroubleshootPage<TroubleshootLogEntry>> {
  const payload = await requestApi("api/troubleshoot/reports/general/page", {
    method: "POST",
    body: JSON.stringify({
      problemKeyword: input.problemKeyword || null,
      userAccessCode: input.userAccessCode ?? null,
      fromDate: input.fromDate || null,
      toDate: input.toDate || null,
      openInExcel: input.openInExcel === true,
      page: normalizePage(input.page),
      pageSize: normalizePageSize(input.pageSize),
    }),
  });
  return readTroubleshootPage(payload, mapLog, "Troubleshoot report page");
}

export async function searchTroubleshootOdometer(input: {
  searchMode: "GG" | "REG" | "TA";
  searchValue: string;
}) {
  const payload = await readJson(
    await requestApi("api/troubleshoot/odometer/search", {
      method: "POST",
      body: JSON.stringify(input),
    }),
  );
  return getCollection(payload)
    .map(mapOdometer)
    .filter((value): value is TroubleshootOdometerResult => value !== null);
}

export async function searchTroubleshootOdometerPage(input: {
  searchMode: "GG" | "REG" | "TA";
  searchValue: string;
  page?: number;
  pageSize?: number;
}): Promise<TroubleshootPage<TroubleshootOdometerResult>> {
  const payload = await requestApi("api/troubleshoot/odometer/search/page", {
    method: "POST",
    body: JSON.stringify({
      searchMode: input.searchMode,
      searchValue: input.searchValue,
      page: normalizePage(input.page),
      pageSize: normalizePageSize(input.pageSize),
    }),
  });
  return readTroubleshootPage(payload, mapOdometer, "Troubleshoot odometer page");
}

export async function getTripsWithoutRoutes() {
  const payload = await readJson(await requestApi("api/troubleshoot/trips-without-routes"));
  return getCollection(payload)
    .map(mapTripWithoutRoutes)
    .filter((value): value is TripsWithoutRoutes => value !== null);
}

export async function getTripsWithoutRoutesPage(
  options: { page?: number; pageSize?: number } = {},
): Promise<TroubleshootPage<TripsWithoutRoutes>> {
  const params = new URLSearchParams({
    page: String(normalizePage(options.page)),
    pageSize: String(normalizePageSize(options.pageSize)),
  });
  const payload = await requestApi(`api/troubleshoot/trips-without-routes/page?${params}`);
  return readTroubleshootPage(payload, mapTripWithoutRoutes, "Trips without routes page");
}

export async function removeTripsWithoutRoutes(input: { fromDate?: string; toDate?: string }) {
  const payload = await readJson(
    await requestApi("api/troubleshoot/remove-trips-no-routes", {
      method: "POST",
      body: JSON.stringify({
        fromDate: input.fromDate || null,
        toDate: input.toDate || null,
      }),
    }),
  );
  return isRecord(payload) ? (asNumber(getValue(payload, "removed", "Removed")) ?? 0) : 0;
}

export async function getApproverRanks() {
  const payload = await readJson(await requestApi("api/authorisers/ranks"));
  return getCollection(payload)
    .map(mapRank)
    .filter((value): value is ApproverRank => value !== null);
}

export async function getApproverRanksPage(
  options: { page?: number; pageSize?: number } = {},
): Promise<TroubleshootPage<ApproverRank>> {
  const params = new URLSearchParams({
    page: String(normalizePage(options.page)),
    pageSize: String(normalizePageSize(options.page)),
  });
  return readTroubleshootPage(
    await requestApi(`api/authorisers/ranks/page?${params}`),
    mapRank,
    "Approver ranks page",
  );
}

export async function saveApproverRanks(
  ranks: { id: number; rankName: string; description: string }[],
) {
  const payload = await readJson(
    await requestApi("api/authorisers/ranks", {
      method: "POST",
      body: JSON.stringify(
        ranks.map((rank) => ({
          id: rank.id,
          rankName: rank.rankName,
          description: rank.description,
        })),
      ),
    }),
  );
  return getCollection(payload)
    .map(mapRank)
    .filter((value): value is ApproverRank => value !== null);
}

export async function getVehicleMasterLookup(vehicleIdentifier: string) {
  const payload = await readJson(
    await requestApi("api/troubleshoot/vehicle-master-edit", {
      method: "POST",
      body: JSON.stringify({ vehicleIdentifier }),
    }),
  );
  return getCollection(payload)
    .map(mapVehicle)
    .filter((value): value is VehicleMasterLookup => value !== null);
}

export async function getVehicleMasterLookupPage(input: {
  vehicleIdentifier?: string;
  page?: number;
  pageSize?: number;
}): Promise<TroubleshootPage<VehicleMasterLookup>> {
  const payload = await requestApi("api/troubleshoot/vehicle-master-edit/page", {
    method: "POST",
    body: JSON.stringify({
      vehicleIdentifier: input.vehicleIdentifier || null,
      page: normalizePage(input.page),
      pageSize: normalizePageSize(input.pageSize),
    }),
  });
  return readTroubleshootPage(payload, mapVehicle, "Vehicle master page");
}
