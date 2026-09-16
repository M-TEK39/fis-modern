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

export type LogbookPage = {
  items: LogbookRecord[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
};

export type LogbookVehicleOption = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
};

export const DEFAULT_LOGBOOK_PAGE_SIZE = 24;

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
  | "unauthorized"
  | "forbidden"
  | "unavailable"
  | "invalid-response"
  | "not-found";

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

function normalizePage(value: number | undefined) {
  return Number.isInteger(value) && (value ?? 0) > 0 ? (value ?? 1) : 1;
}

function normalizePageSize(value: number | undefined) {
  const pageSize =
    Number.isInteger(value) && (value ?? 0) > 0
      ? (value ?? DEFAULT_LOGBOOK_PAGE_SIZE)
      : DEFAULT_LOGBOOK_PAGE_SIZE;
  return Math.min(100, pageSize);
}

function normalizePositiveInteger(value: number | undefined) {
  return Number.isInteger(value) && (value ?? 0) > 0 ? (value ?? 1) : null;
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
    if (response.status === 401) {
      throw new LogbookApiError("unauthorized", "The FIS access cookie was rejected.");
    }
    if (response.status === 403) {
      throw new LogbookApiError(
        "forbidden",
        "Your account is not assigned the required Logbooks role or site scope.",
      );
    }
    if (response.status === 404)
      throw new LogbookApiError("not-found", "The logbook was not found.");
    if (!response.ok) {
      let message = `FIS API returned HTTP ${response.status}.`;
      try {
        const payload = (await response.json()) as unknown;
        if (isRecord(payload)) {
          const detail = getValue(payload, "message", "error", "detail");
          if (typeof detail === "string" && detail.trim()) message = detail.trim();
        } else if (typeof payload === "string" && payload.trim()) {
          message = payload.trim();
        }
      } catch {
        // Keep the status-based message when the API has no readable body.
      }
      throw new LogbookApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
      );
    }
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

function readLogbookPage(payload: unknown): LogbookPage {
  if (!isRecord(payload) || !Array.isArray(payload.items)) {
    throw new LogbookApiError("invalid-response", "The FIS API returned an invalid logbook page.");
  }

  const metadata = readPageMetadata(payload);
  if (!metadata) {
    throw new LogbookApiError(
      "invalid-response",
      "The FIS API returned incomplete logbook pagination metadata.",
    );
  }

  return {
    items: payload.items
      .map(mapLogbook)
      .filter((item): item is LogbookRecord => item !== null && !item.isDeleted),
    ...metadata,
  };
}

export async function getLogbookPage(
  options: { page?: number; pageSize?: number; search?: string; vmfCode?: number } = {},
): Promise<LogbookPage> {
  const params = new URLSearchParams({
    search: options.search?.trim() ?? "",
    page: String(normalizePage(options.page)),
    pageSize: String(normalizePageSize(options.pageSize)),
  });
  const vmfCode = normalizePositiveInteger(options.vmfCode);
  if (vmfCode !== null) params.set("vmfCode", String(vmfCode));
  return readLogbookPage(await readJson(await requestApi(`api/logbook/page?${params.toString()}`)));
}

export async function getLogbookVehicleOptions(): Promise<LogbookVehicleOption[]> {
  const payload = getCollection(await readJson(await requestApi("api/logbook/vehicle-options")));
  return payload.flatMap((value) => {
    if (!isRecord(value)) return [];
    const vmfCode = asNumber(getValue(value, "vmfCode", "vmf_code"));
    if (vmfCode === null) return [];
    return [
      {
        vmfCode,
        fleetNumber: asString(getValue(value, "fleetNumber", "fleet_number")),
        registrationNumber: asString(getValue(value, "registrationNumber", "registration_number")),
      },
    ];
  });
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
