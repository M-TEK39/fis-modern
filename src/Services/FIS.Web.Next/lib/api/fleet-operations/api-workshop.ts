import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
export const DEFAULT_WORKSHOP_PAGE_SIZE = 24;
type JsonRecord = Record<string, unknown>;

export type WorkshopRecord = {
  wwCode: number;
  vmfCode: number | null;
  receiveTime: string | null;
  receiveDate: string | null;
  completeTime: string | null;
  completeDate: string | null;
  fetchTime: string | null;
  fetchDate: string | null;
  driverName: string | null;
  contactName: string | null;
  contactTel: string | null;
  remarks: string | null;
  reason: string | null;
  merchantCode: number | null;
  jobClose: string | null;
  isDeleted: boolean;
};

export type WorkshopInput = {
  vmf_code: number | null;
  receive_time: string | null;
  receive_date: string | null;
  complete_time: string | null;
  complete_date: string | null;
};

export type WorkshopVehicle = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  locationCode: number | null;
  locationDescription: string | null;
};

export type WorkshopPageItem = {
  wwCode: number;
  vmfCode: number | null;
  receiveDate: string | null;
  completeTime: string | null;
  completeDate: string | null;
  fleetNumber: string | null;
  registrationNumber: string | null;
  locationCode: number | null;
  status: string;
};

export type WorkshopPage = {
  items: WorkshopPageItem[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
};

export type WorkshopApiErrorReason =
  "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class WorkshopApiError extends Error {
  constructor(
    public readonly reason: WorkshopApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "WorkshopApiError";
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
    if (key in record) return record[key];
  }
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
  return typeof value === "string" && ["true", "1", "y"].includes(value.trim().toLowerCase());
}

function asDate(value: unknown) {
  return asString(value);
}

function asTime(value: unknown) {
  const normalized = asString(value);
  if (!normalized) return null;

  const timeMatch = normalized.match(/(?:T|\s)(\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?)/);
  return timeMatch?.[1] ?? normalized;
}

function getCollection(payload: unknown) {
  if (Array.isArray(payload)) return payload;
  if (isRecord(payload)) {
    const collection = getValue(payload, "data", "items", "results");
    return Array.isArray(collection) ? collection : [];
  }
  return [];
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new WorkshopApiError("unauthorized", "No FIS access cookie is available.");
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
        ...(init.body ? { "content-type": "application/json" } : {}),
        ...init.headers,
      },
      signal: controller.signal,
    });

    if (response.status === 401 || response.status === 403) {
      throw new WorkshopApiError("unauthorized", "The FIS access cookie was rejected.");
    }
    if (response.status === 404) {
      throw new WorkshopApiError("not-found", "The workshop record was not found.");
    }
    if (!response.ok) {
      throw new WorkshopApiError("invalid-response", `FIS API returned HTTP ${response.status}.`);
    }
    return response;
  } catch (error) {
    if (error instanceof WorkshopApiError) throw error;
    throw new WorkshopApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new WorkshopApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapWorkshop(value: unknown): WorkshopRecord | null {
  if (!isRecord(value)) return null;
  const wwCode = asNumber(getValue(value, "ww_code", "wwCode"));
  if (wwCode === null) return null;

  return {
    wwCode,
    vmfCode: asNumber(getValue(value, "vmf_code", "vmfCode")),
    receiveTime: asTime(getValue(value, "receive_time", "receiveTime")),
    receiveDate: asDate(getValue(value, "receive_date", "receiveDate")),
    completeTime: asTime(getValue(value, "complete_time", "completeTime")),
    completeDate: asDate(getValue(value, "complete_date", "completeDate")),
    fetchTime: asTime(getValue(value, "fetch_time", "fetchTime")),
    fetchDate: asDate(getValue(value, "fetch_date", "fetchDate")),
    driverName: asString(getValue(value, "driver_name", "driverName")),
    contactName: asString(getValue(value, "contact_name", "contactName")),
    contactTel: asString(getValue(value, "contact_tel", "contactTel")),
    remarks: asString(getValue(value, "ww_remarks", "remarks")),
    reason: asString(getValue(value, "ww_reason", "reason")),
    merchantCode: asNumber(getValue(value, "merch_code", "merchantCode")),
    jobClose: asString(getValue(value, "job_close", "jobClose")),
    isDeleted: asBoolean(getValue(value, "is_deleted", "isDeleted")),
  };
}

function mapVehicle(value: unknown): WorkshopVehicle | null {
  if (!isRecord(value)) return null;
  const vmfCode = asNumber(getValue(value, "vmf_code", "vmfCode"));
  if (vmfCode === null) return null;

  return {
    vmfCode,
    fleetNumber: asString(getValue(value, "fleet_number", "fleetNumber")),
    registrationNumber: asString(getValue(value, "registration_number", "registrationNumber")),
    locationCode: asNumber(getValue(value, "location_code", "locationCode")),
    locationDescription: asString(getValue(value, "location_description", "locationDescription")),
  };
}

function mapWorkshopPageItem(value: unknown): WorkshopPageItem | null {
  if (!isRecord(value)) return null;
  const wwCode = asNumber(getValue(value, "ww_code", "wwCode"));
  if (wwCode === null) return null;

  return {
    wwCode,
    vmfCode: asNumber(getValue(value, "vmf_code", "vmfCode")),
    receiveDate: asDate(getValue(value, "receive_date", "receiveDate")),
    completeTime: asTime(getValue(value, "complete_time", "completeTime")),
    completeDate: asDate(getValue(value, "complete_date", "completeDate")),
    fleetNumber: asString(getValue(value, "fleet_number", "fleetNumber")),
    registrationNumber: asString(getValue(value, "registration_number", "registrationNumber")),
    locationCode: asNumber(getValue(value, "location_code", "locationCode")),
    status: asString(getValue(value, "status", "Status")) ?? "Open",
  };
}

function readWorkshopPage(payload: unknown): WorkshopPage {
  if (!isRecord(payload) || !Array.isArray(payload.items)) {
    throw new WorkshopApiError(
      "invalid-response",
      "The FIS API returned an invalid workshop page.",
    );
  }

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
    throw new WorkshopApiError(
      "invalid-response",
      "The FIS API returned incomplete workshop pagination metadata.",
    );
  }

  return {
    items: payload.items
      .map(mapWorkshopPageItem)
      .filter((item): item is WorkshopPageItem => item !== null),
    page,
    pageSize,
    total,
    totalPages,
  };
}

function normalizePage(value: number | undefined) {
  return Number.isInteger(value) && (value ?? 0) > 0 ? (value ?? 1) : 1;
}

function normalizePageSize(value: number | undefined) {
  const pageSize =
    Number.isInteger(value) && (value ?? 0) > 0
      ? (value ?? DEFAULT_WORKSHOP_PAGE_SIZE)
      : DEFAULT_WORKSHOP_PAGE_SIZE;
  return Math.min(100, pageSize);
}

export async function getWorkshops() {
  const response = await requestApi("api/workshop");
  return getCollection(await readJson(response))
    .map(mapWorkshop)
    .filter((record): record is WorkshopRecord => record !== null);
}

export async function getWorkshopPage(
  options: {
    page?: number;
    pageSize?: number;
    search?: string;
    status?: string;
    searchField?: "fleet" | "registration";
  } = {},
): Promise<WorkshopPage> {
  const params = new URLSearchParams({
    search: options.search?.trim() ?? "",
    status: options.status?.trim().toLowerCase() ?? "",
    searchField: options.searchField ?? "",
    page: String(normalizePage(options.page)),
    pageSize: String(normalizePageSize(options.pageSize)),
  });

  return readWorkshopPage(
    await readJson(await requestApi(`api/workshop/page?${params.toString()}`)),
  );
}

export async function getWorkshop(wwCode: number) {
  const response = await requestApi(`api/workshop/${encodeURIComponent(wwCode)}`);
  const record = mapWorkshop(await readJson(response));
  if (!record)
    throw new WorkshopApiError(
      "invalid-response",
      "The FIS API returned an invalid workshop record.",
    );
  return record;
}

export async function createWorkshop(input: WorkshopInput) {
  const response = await requestApi("api/workshop", {
    method: "POST",
    body: JSON.stringify(input),
  });
  const record = mapWorkshop(await readJson(response));
  if (!record)
    throw new WorkshopApiError(
      "invalid-response",
      "The FIS API returned an invalid workshop record.",
    );
  return record;
}

export async function updateWorkshop(wwCode: number, input: WorkshopInput) {
  const response = await requestApi(`api/workshop/${encodeURIComponent(wwCode)}`, {
    method: "PUT",
    body: JSON.stringify({ ...input, ww_code: wwCode }),
  });
  const record = mapWorkshop(await readJson(response));
  if (!record)
    throw new WorkshopApiError(
      "invalid-response",
      "The FIS API returned an invalid workshop record.",
    );
  return record;
}

export async function deleteWorkshop(wwCode: number) {
  await requestApi(`api/workshop/${encodeURIComponent(wwCode)}`, { method: "DELETE" });
}

export async function getWorkshopVehicles() {
  const response = await requestApi("api/vehicles");
  return getCollection(await readJson(response))
    .map(mapVehicle)
    .filter((vehicle): vehicle is WorkshopVehicle => vehicle !== null);
}

export async function searchWorkshopVehicles(searchTerm: string) {
  const response = await requestApi(
    `api/vehicles/search?searchTerm=${encodeURIComponent(searchTerm.trim())}`,
  );
  return getCollection(await readJson(response))
    .map(mapVehicle)
    .filter((vehicle): vehicle is WorkshopVehicle => vehicle !== null);
}
