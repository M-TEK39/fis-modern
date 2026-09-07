import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
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

export type WorkshopApiErrorReason = "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class WorkshopApiError extends Error {
  constructor(public readonly reason: WorkshopApiErrorReason, message: string) {
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

export async function getWorkshops() {
  const response = await requestApi("api/workshop");
  return getCollection(await readJson(response))
    .map(mapWorkshop)
    .filter((record): record is WorkshopRecord => record !== null);
}

export async function getWorkshop(wwCode: number) {
  const response = await requestApi(`api/workshop/${encodeURIComponent(wwCode)}`);
  const record = mapWorkshop(await readJson(response));
  if (!record) throw new WorkshopApiError("invalid-response", "The FIS API returned an invalid workshop record.");
  return record;
}

export async function createWorkshop(input: WorkshopInput) {
  const response = await requestApi("api/workshop", { method: "POST", body: JSON.stringify(input) });
  const record = mapWorkshop(await readJson(response));
  if (!record) throw new WorkshopApiError("invalid-response", "The FIS API returned an invalid workshop record.");
  return record;
}

export async function updateWorkshop(wwCode: number, input: WorkshopInput) {
  const response = await requestApi(`api/workshop/${encodeURIComponent(wwCode)}`, {
    method: "PUT",
    body: JSON.stringify({ ...input, ww_code: wwCode }),
  });
  const record = mapWorkshop(await readJson(response));
  if (!record) throw new WorkshopApiError("invalid-response", "The FIS API returned an invalid workshop record.");
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
  const response = await requestApi(`api/vehicles/search?searchTerm=${encodeURIComponent(searchTerm.trim())}`);
  return getCollection(await readJson(response))
    .map(mapVehicle)
    .filter((vehicle): vehicle is WorkshopVehicle => vehicle !== null);
}
