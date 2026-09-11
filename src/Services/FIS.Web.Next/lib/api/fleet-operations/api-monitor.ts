import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
export const DEFAULT_MONITOR_PAGE_SIZE = 24;
type JsonRecord = Record<string, unknown>;

export type MonitorRecord = {
  monitorCode: number;
  vmfCode: number | null;
  captureDate: string | null;
  userAccessCode: number | null;
  inquiryType: string | null;
  inquiryDescription: string | null;
  driverName: string | null;
  driverPersalNo: string | null;
  driverSite: number | null;
  isDeleted: boolean;
  fleetNumber: string | null;
  registrationNumber: string | null;
};

export type MonitorPage = {
  items: MonitorRecord[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
};

export type MonitorWriteInput = {
  vmf_code: number;
  Capture_dat: string;
  Inquiry_type: string;
  Inquiry_Desc: string | null;
  Driver_name: string | null;
  Driver_persalno: string | null;
  Driver_Site: number | null;
};

export type MonitorDriverOption = {
  code: number;
  siteCode: number | null;
  surname: string | null;
  firstname: string | null;
  persalNumber: string | null;
};

export type MonitorReportRow = {
  monitorCode: number;
  vmfCode: number | null;
  captureDate: string | null;
  inquiryType: string;
  inquiryDescription: string;
  driverName: string;
  driverPersalNo: string;
  driverSite: number | null;
};

export type MonitorStatistic = { inquiryType: string; count: number };

export type MonitorApiErrorReason =
  "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class MonitorApiError extends Error {
  constructor(
    public readonly reason: MonitorApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "MonitorApiError";
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

function getCollection(payload: unknown) {
  if (Array.isArray(payload)) return payload;
  if (isRecord(payload)) {
    const data = getValue(payload, "data", "items", "results");
    return Array.isArray(data) ? data : [];
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
      ? (value ?? DEFAULT_MONITOR_PAGE_SIZE)
      : DEFAULT_MONITOR_PAGE_SIZE;
  return Math.min(100, pageSize);
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader)
    throw new MonitorApiError("unauthorized", "No FIS access cookie is available.");

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers: { accept: "application/json", cookie: cookieHeader, ...init.headers },
      signal: controller.signal,
    });
    if (response.status === 401 || response.status === 403)
      throw new MonitorApiError(
        "unauthorized",
        "The FIS access cookie was rejected.",
        response.status,
      );
    if (response.status === 404)
      throw new MonitorApiError("not-found", "The monitor inquiry was not found.", response.status);
    if (!response.ok)
      throw new MonitorApiError(
        "unavailable",
        `FIS API returned HTTP ${response.status}.`,
        response.status,
      );
    return response;
  } catch (error) {
    if (error instanceof MonitorApiError) throw error;
    throw new MonitorApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new MonitorApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapMonitor(value: unknown): MonitorRecord | null {
  if (!isRecord(value)) return null;
  const monitorCode = asNumber(getValue(value, "monitor_code", "monitorCode"));
  if (monitorCode === null) return null;
  const vehicle = getValue(value, "vehicle", "Vehicle");
  const vehicleRecord = isRecord(vehicle) ? vehicle : {};
  return {
    monitorCode,
    vmfCode: asNumber(getValue(value, "vmf_code", "vmfCode")),
    captureDate: asString(getValue(value, "Capture_dat", "capture_dat", "captureDate")),
    userAccessCode: asNumber(
      getValue(value, "User_access_code", "user_access_code", "userAccessCode"),
    ),
    inquiryType: asString(getValue(value, "Inquiry_type", "inquiry_type", "inquiryType")),
    inquiryDescription: asString(
      getValue(value, "Inquiry_Desc", "inquiry_Desc", "inquiryDescription"),
    ),
    driverName: asString(getValue(value, "Driver_name", "driver_name", "driverName")),
    driverPersalNo: asString(
      getValue(value, "Driver_persalno", "driver_persalno", "driverPersalNo"),
    ),
    driverSite: asNumber(getValue(value, "Driver_Site", "driver_site", "driverSite")),
    isDeleted: asBoolean(getValue(value, "is_deleted", "isDeleted")),
    fleetNumber:
      asString(getValue(value, "fleet_number", "fleetNumber")) ??
      asString(getValue(vehicleRecord, "fleet_number", "fleetNumber")),
    registrationNumber:
      asString(getValue(value, "registration_number", "registrationNumber")) ??
      asString(getValue(vehicleRecord, "registration_number", "registrationNumber")),
  };
}

function mapReportRow(value: unknown): MonitorReportRow | null {
  if (!isRecord(value)) return null;
  const monitorCode = asNumber(getValue(value, "monitorCode", "monitor_code"));
  if (monitorCode === null) return null;
  return {
    monitorCode,
    vmfCode: asNumber(getValue(value, "vmfCode", "vmf_code")),
    captureDate: asString(getValue(value, "captureDate", "Capture_dat")),
    inquiryType: asString(getValue(value, "inquiryType", "Inquiry_type")) ?? "",
    inquiryDescription: asString(getValue(value, "inquiryDescription", "Inquiry_Desc")) ?? "",
    driverName: asString(getValue(value, "driverName", "Driver_name")) ?? "",
    driverPersalNo: asString(getValue(value, "driverPersalNo", "Driver_persalno")) ?? "",
    driverSite: asNumber(getValue(value, "driverSite", "Driver_Site")),
  };
}

export async function getMonitors() {
  const payload = await readJson(await requestApi("api/monitor"));
  return getCollection(payload)
    .map(mapMonitor)
    .filter((item): item is MonitorRecord => item !== null);
}

export async function getMonitorPage(
  options: {
    page?: number;
    pageSize?: number;
    search?: string;
  } = {},
): Promise<MonitorPage> {
  const params = new URLSearchParams({
    search: options.search?.trim() ?? "",
    page: String(normalizePage(options.page)),
    pageSize: String(normalizePageSize(options.pageSize)),
  });
  const payload = await readJson(await requestApi(`api/monitor/page?${params.toString()}`));

  if (!isRecord(payload) || !Array.isArray(payload.items)) {
    throw new MonitorApiError("invalid-response", "The FIS API returned an invalid monitor page.");
  }

  const metadata = readPageMetadata(payload);
  if (!metadata) {
    throw new MonitorApiError(
      "invalid-response",
      "The FIS API returned incomplete monitor pagination metadata.",
    );
  }

  return {
    items: payload.items.map(mapMonitor).filter((item): item is MonitorRecord => item !== null),
    ...metadata,
  };
}

export async function getMonitor(monitorCode: number) {
  const payload = await readJson(
    await requestApi(`api/monitor/${encodeURIComponent(monitorCode)}`),
  );
  return mapMonitor(payload);
}

export async function getMonitorDrivers() {
  const payload = await readJson(await requestApi("api/site-drivers"));
  return getCollection(payload)
    .map((value): MonitorDriverOption | null => {
      if (!isRecord(value)) return null;
      const code = asNumber(
        getValue(value, "siteDriverCode", "SiteDriverCode", "site_driver_code"),
      );
      if (code === null) return null;
      return {
        code,
        siteCode: asNumber(getValue(value, "siteCode", "SiteCode", "site_code")),
        surname: asString(getValue(value, "driverSurname", "DriverSurname", "driver_surname")),
        firstname: asString(
          getValue(value, "driverFirstname", "DriverFirstname", "driver_firstname"),
        ),
        persalNumber: asString(
          getValue(
            value,
            "driverPersonalNumber",
            "DriverPersonalNumber",
            "driver_persalnumber",
            "persalNumber",
          ),
        ),
      };
    })
    .filter((item): item is MonitorDriverOption => item !== null)
    .sort((left, right) =>
      `${left.surname ?? ""} ${left.firstname ?? ""}`.localeCompare(
        `${right.surname ?? ""} ${right.firstname ?? ""}`,
      ),
    );
}

async function mutate(path: string, method: "POST" | "PUT", input: MonitorWriteInput) {
  const response = await requestApi(path, {
    method,
    headers: { "content-type": "application/json" },
    body: JSON.stringify(input),
  });
  return response.status === 204 ? null : mapMonitor(await readJson(response));
}

export async function createMonitor(input: MonitorWriteInput) {
  return mutate("api/monitor", "POST", input);
}

export async function updateMonitor(monitorCode: number, input: MonitorWriteInput) {
  return mutate(`api/monitor/${encodeURIComponent(monitorCode)}`, "PUT", input);
}

export async function getMonitorReportByReference(monitorCode: number) {
  const payload = await readJson(
    await requestApi(`api/monitor/reports/one-reference-number/${encodeURIComponent(monitorCode)}`),
  );
  return getCollection(isRecord(payload) ? getValue(payload, "data", "Data") : payload)
    .map(mapReportRow)
    .filter((item): item is MonitorReportRow => item !== null);
}

export async function getInquiryStatistics(startDate: string, endDate: string) {
  const response = await requestApi("api/monitor/reports/inquiry-statistics", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ startDate, endDate }),
  });
  const payload = await readJson(response);
  const data = isRecord(payload) ? getValue(payload, "data", "Data") : payload;
  return getCollection(data)
    .filter(isRecord)
    .map((item) => ({
      inquiryType: asString(getValue(item, "inquiryType", "InquiryType")) ?? "(Unknown)",
      count: asNumber(getValue(item, "count", "Count")) ?? 0,
    }))
    .sort(
      (left, right) =>
        right.count - left.count || left.inquiryType.localeCompare(right.inquiryType),
    );
}
