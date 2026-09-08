import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type LogsheetRecord = {
  logCode: number;
  vmfCode: number;
  ggNumber: string | null;
  registrationNumber: string | null;
  startOdometer: number;
  endOdometer: number;
  month: string;
  siteCode: number;
  siteDescription: string | null;
  requisitionNumber: string | null;
  daysUsed: number | null;
  bundleNumber: number | null;
  dateCreated: string | null;
  dateUpdated: string | null;
  createdByUserCode: number | null;
  modifiedByUserCode: number | null;
  isDeleted: boolean;
};

export type LogsheetWriteInput = {
  vmf_code: number;
  start_odo: number;
  end_odo: number;
  month: string;
  site_code: number;
  rek_num: string;
  days_used: number | null;
  bund_num: number | null;
};

export type LogsheetApiErrorReason =
  "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class LogsheetApiError extends Error {
  constructor(
    public readonly reason: LogsheetApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "LogsheetApiError";
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

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader)
    throw new LogsheetApiError("unauthorized", "No FIS access cookie is available.");

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
      throw new LogsheetApiError("unauthorized", "The FIS access cookie was rejected.");
    }
    if (response.status === 404)
      throw new LogsheetApiError("not-found", "The logsheet was not found.");
    if (!response.ok)
      throw new LogsheetApiError("invalid-response", `FIS API returned HTTP ${response.status}.`);
    return response;
  } catch (error) {
    if (error instanceof LogsheetApiError) throw error;
    throw new LogsheetApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new LogsheetApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapLogsheet(value: unknown): LogsheetRecord | null {
  if (!isRecord(value)) return null;
  const logCode = asNumber(getValue(value, "log_code", "logCode"));
  const vmfCode = asNumber(getValue(value, "vmf_code", "vmfCode"));
  const month = asString(getValue(value, "month"));
  if (logCode === null || vmfCode === null || month === null) return null;

  const vehicle = getValue(value, "vehicle", "Vehicle");
  const site = getValue(value, "site", "Site");
  const vehicleRecord = isRecord(vehicle) ? vehicle : null;
  const siteRecord = isRecord(site) ? site : null;

  return {
    logCode,
    vmfCode,
    ggNumber:
      asString(getValue(value, "fleet_number", "fleetNumber")) ??
      asString(vehicleRecord && getValue(vehicleRecord, "fleet_number", "fleetNumber")),
    registrationNumber:
      asString(getValue(value, "registration_number", "registrationNumber")) ??
      asString(
        vehicleRecord && getValue(vehicleRecord, "registration_number", "registrationNumber"),
      ),
    startOdometer: asNumber(getValue(value, "start_odo", "startOdo")) ?? 0,
    endOdometer: asNumber(getValue(value, "end_odo", "endOdo")) ?? 0,
    month,
    siteCode: asNumber(getValue(value, "site_code", "siteCode")) ?? 0,
    siteDescription:
      asString(getValue(value, "site_description", "siteDescription")) ??
      asString(siteRecord && getValue(siteRecord, "description", "Description")),
    requisitionNumber: asString(getValue(value, "rek_num", "rekNum")),
    daysUsed: asNumber(getValue(value, "days_used", "daysUsed")),
    bundleNumber: asNumber(getValue(value, "bund_num", "bundNum")),
    dateCreated: asString(getValue(value, "date_created", "dateCreated")),
    dateUpdated: asString(getValue(value, "date_updated", "dateUpdated")),
    createdByUserCode: asNumber(getValue(value, "created_by_user_code", "createdByUserCode")),
    modifiedByUserCode: asNumber(getValue(value, "modified_by_user_code", "modifiedByUserCode")),
    isDeleted: asBoolean(getValue(value, "is_deleted", "isDeleted")),
  };
}

async function mutate(path: string, method: "POST" | "PUT" | "DELETE", body?: unknown) {
  return requestApi(path, {
    method,
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
}

export async function getLogsheets() {
  const payload = await readJson(await requestApi("api/logsheet"));
  return getCollection(payload)
    .map(mapLogsheet)
    .filter((item): item is LogsheetRecord => item !== null && !item.isDeleted);
}

export async function getLogsheet(logCode: number) {
  const record = mapLogsheet(
    await readJson(await requestApi(`api/logsheet/${encodeURIComponent(logCode)}`)),
  );
  if (!record)
    throw new LogsheetApiError("invalid-response", "The FIS API returned an invalid logsheet.");
  return record;
}

export async function createLogsheet(input: LogsheetWriteInput) {
  const payload = await readJson(
    await mutate("api/logsheet/entry", "POST", {
      vmfCode: input.vmf_code,
      startOdometer: input.start_odo,
      endOdometer: input.end_odo,
      month: input.month,
      siteCode: input.site_code,
      requisitionNumber: input.rek_num,
      daysUsed: input.days_used,
      bundleNumber: input.bund_num,
    }),
  );
  const logCode = isRecord(payload) ? asNumber(getValue(payload, "logCode", "LogCode")) : null;
  if (logCode === null)
    throw new LogsheetApiError(
      "invalid-response",
      "The FIS API returned an invalid created logsheet.",
    );
  return getLogsheet(logCode);
}

export async function updateLogsheet(logCode: number, input: LogsheetWriteInput) {
  const response = await mutate(`api/logsheet/edit/${encodeURIComponent(logCode)}`, "PUT", {
    vmfCode: input.vmf_code,
    startOdometer: input.start_odo,
    endOdometer: input.end_odo,
    month: input.month,
    siteCode: input.site_code,
    requisitionNumber: input.rek_num,
    daysUsed: input.days_used,
    bundleNumber: input.bund_num,
  });
  const payload = await readJson(response);
  if (isRecord(payload) && getValue(payload, "success", "Success") === false) {
    throw new LogsheetApiError(
      "invalid-response",
      asString(getValue(payload, "message", "Message")) ?? "The logsheet could not be updated.",
    );
  }
  return getLogsheet(logCode);
}

export async function deleteLogsheet(logCode: number) {
  await mutate(`api/logsheet/entry/${encodeURIComponent(logCode)}`, "DELETE");
}
