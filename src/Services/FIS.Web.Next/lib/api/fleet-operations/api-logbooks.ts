import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type LogbookRecord = {
  logbookCode: number;
  vmfCode: number | null;
  ggNumber: string | null;
  registrationNumber: string | null;
  beginNumber: string | null;
  endNumber: string | null;
  handoutDate: string | null;
  siteCode: number | null;
  siteDescription: string | null;
  receiverName: string | null;
  telephoneNumber: string | null;
  comment: string | null;
  dateCreated: string | null;
  dateUpdated: string | null;
  createdByUserCode: number | null;
  modifiedByUserCode: number | null;
  isDeleted: boolean;
};

export type LogbookWriteInput = {
  vmf_code: number | null;
  begin_num?: string | null;
  end_num?: string | null;
  handout_date?: string | null;
  site_code?: number | null;
  lb_receiver_name?: string | null;
  lb_tel_num?: string | null;
  lb_comment?: string | null;
  date_created?: string | null;
};

export type LogbookApiErrorReason =
  "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class LogbookApiError extends Error {
  constructor(
    public readonly reason: LogbookApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "LogbookApiError";
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

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader)
    throw new LogbookApiError("unauthorized", "No FIS access cookie is available.");

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
      throw new LogbookApiError("unauthorized", "The FIS access cookie was rejected.");
    }
    if (response.status === 404)
      throw new LogbookApiError("not-found", "The logbook was not found.");
    if (!response.ok)
      throw new LogbookApiError("invalid-response", `FIS API returned HTTP ${response.status}.`);
    return response;
  } catch (error) {
    if (error instanceof LogbookApiError) throw error;
    throw new LogbookApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new LogbookApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapLogbook(value: unknown): LogbookRecord | null {
  if (!isRecord(value)) return null;
  const logbookCode = asNumber(getValue(value, "logbookcode", "logbookCode"));
  if (logbookCode === null) return null;

  const vehicle = getValue(value, "vehicle", "Vehicle");
  const site = getValue(value, "site", "Site");
  const vehicleRecord = isRecord(vehicle) ? vehicle : null;
  const siteRecord = isRecord(site) ? site : null;
  return {
    logbookCode,
    vmfCode: asNumber(getValue(value, "vmf_code", "vmfCode")),
    ggNumber:
      asString(getValue(value, "fleet_number", "fleetNumber")) ??
      asString(vehicleRecord && getValue(vehicleRecord, "fleet_number", "fleetNumber")),
    registrationNumber:
      asString(getValue(value, "registration_number", "registrationNumber")) ??
      asString(
        vehicleRecord && getValue(vehicleRecord, "registration_number", "registrationNumber"),
      ),
    beginNumber: asString(getValue(value, "begin_num", "beginNum")),
    endNumber: asString(getValue(value, "end_num", "endNum")),
    handoutDate: asString(getValue(value, "handout_date", "handoutDate")),
    siteCode: asNumber(getValue(value, "site_code", "siteCode")),
    siteDescription:
      asString(getValue(value, "site_description", "siteDescription")) ??
      asString(siteRecord && getValue(siteRecord, "description", "Description")),
    receiverName: asString(getValue(value, "lb_receiver_name", "receiverName", "lbReceiverName")),
    telephoneNumber: asString(getValue(value, "lb_tel_num", "telephoneNumber", "lbTelNum")),
    comment: asString(getValue(value, "lb_comment", "comment")),
    dateCreated: asString(getValue(value, "date_created", "dateCreated")),
    dateUpdated: asString(getValue(value, "date_updated", "dateUpdated")),
    createdByUserCode: asNumber(getValue(value, "created_by_user_code", "createdByUserCode")),
    modifiedByUserCode: asNumber(getValue(value, "modified_by_user_code", "modifiedByUserCode")),
    isDeleted: asBoolean(getValue(value, "is_deleted", "isDeleted")),
  };
}

async function mutate(path: string, method: string, body?: unknown) {
  return requestApi(path, {
    method,
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
}

export async function getLogbooks() {
  const payload = await readJson(await requestApi("api/logbook"));
  return getCollection(payload)
    .map(mapLogbook)
    .filter((item): item is LogbookRecord => item !== null && !item.isDeleted);
}

export async function getLogbook(logbookCode: number) {
  const record = mapLogbook(
    await readJson(await requestApi(`api/logbook/${encodeURIComponent(logbookCode)}`)),
  );
  if (!record)
    throw new LogbookApiError("invalid-response", "The FIS API returned an invalid logbook.");
  return record;
}

export async function createLogbook(input: LogbookWriteInput) {
  const record = mapLogbook(await readJson(await mutate("api/logbook", "POST", input)));
  if (!record)
    throw new LogbookApiError(
      "invalid-response",
      "The FIS API returned an invalid created logbook.",
    );
  return record;
}

export async function updateLogbook(logbookCode: number, input: LogbookWriteInput) {
  const record = mapLogbook(
    await readJson(
      await mutate(`api/logbook/${encodeURIComponent(logbookCode)}`, "PUT", {
        logbookcode: logbookCode,
        ...input,
      }),
    ),
  );
  if (!record)
    throw new LogbookApiError(
      "invalid-response",
      "The FIS API returned an invalid updated logbook.",
    );
  return record;
}

export async function deleteLogbook(logbookCode: number) {
  await mutate(`api/logbook/${encodeURIComponent(logbookCode)}`, "DELETE");
}
