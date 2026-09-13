import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
export const DEFAULT_LICENSE_REPORT_PAGE_SIZE = 24;
type JsonRecord = Record<string, unknown>;

export const LICENSE_REPORT_MODES = [
  "gg-number",
  "gp-number",
  "register-number",
  "engine-number",
  "chassis-number",
  "site",
  "all",
  "dept-period",
  "expire-date",
  "month-fees",
  "old-expire",
  "sap",
  "cof",
  "model-fees",
  "gg-model-fees",
  "workgroup",
  "workgroup-latest",
  "ggmt-received",
] as const;

export type LicenseReportMode = (typeof LICENSE_REPORT_MODES)[number];

export type LicenseReportFilters = {
  search?: string;
  searchMode?: "GG" | "GP" | "REGISTER" | "ENGINE" | "CHASSIS";
  departmentCode?: string;
  location?: "jhb" | "pta";
  status?: "inservice" | "notinservice";
  from?: string;
  to?: string;
  month?: number;
  year?: number;
};

export type LicenseReportRequestOptions = {
  page?: number;
  pageSize?: number;
};

export type LicenseReport = {
  columns: Array<{ key: string; header: string }>;
  rows: Array<Record<string, string | null>>;
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
};

export class LicenseReportApiError extends Error {
  constructor(
    public readonly reason: "unauthorized" | "unavailable" | "invalid-response",
    message: string,
  ) {
    super(message);
    this.name = "LicenseReportApiError";
  }
}

function apiBaseUrl() {
  const value = process.env.API_BASE_URL?.trim() || "http://localhost:5010";
  return `${value.replace(/\/$/, "")}/`;
}

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function valueOf(record: JsonRecord, ...keys: string[]) {
  for (const key of keys) if (key in record) return record[key];
  return undefined;
}

function asString(value: unknown) {
  if (typeof value === "string") return value;
  if (typeof value === "number" || typeof value === "bigint" || typeof value === "boolean")
    return String(value);
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

function nonNegativeInteger(value: unknown) {
  const number = asNumber(value);
  return number !== null && Number.isSafeInteger(number) && number >= 0 ? number : null;
}

function positiveInteger(value: unknown) {
  const number = asNumber(value);
  return number !== null && Number.isSafeInteger(number) && number > 0 ? number : null;
}

async function requestApi(mode: LicenseReportMode, body: Record<string, unknown>) {
  const cookie = await getForwardedAuthCookieHeader();
  if (!cookie)
    throw new LicenseReportApiError("unauthorized", "No FIS access cookie is available.");

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  try {
    const response = await fetch(new URL(`api/report/licences/${mode}`, apiBaseUrl()), {
      method: "POST",
      cache: "no-store",
      headers: { accept: "application/json", "content-type": "application/json", cookie },
      body: JSON.stringify(body),
      signal: controller.signal,
    });
    if (response.status === 401 || response.status === 403) {
      throw new LicenseReportApiError("unauthorized", "The FIS access cookie was rejected.");
    }
    if (!response.ok) {
      let message = `FIS API returned HTTP ${response.status}.`;
      try {
        const payload = await response.clone().json();
        if (isRecord(payload))
          message = asString(valueOf(payload, "message", "Message", "error")) ?? message;
      } catch {
        // Keep the status message when the API has no JSON error body.
      }
      throw new LicenseReportApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
      );
    }
    try {
      return (await response.json()) as unknown;
    } catch {
      throw new LicenseReportApiError(
        "invalid-response",
        "The FIS API returned invalid report JSON.",
      );
    }
  } catch (error) {
    if (error instanceof LicenseReportApiError) throw error;
    throw new LicenseReportApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

function reportRows(payload: unknown) {
  if (isRecord(payload)) {
    const rows = valueOf(payload, "rows", "Rows");
    return Array.isArray(rows) ? rows : null;
  }
  return null;
}

function reportColumns(payload: JsonRecord) {
  const columns = valueOf(payload, "columns", "Columns");
  if (!Array.isArray(columns)) return null;
  return columns
    .map((column) => {
      if (!isRecord(column)) return null;
      const key = asString(valueOf(column, "key", "Key"));
      const header = asString(valueOf(column, "header", "Header"));
      return key?.trim() && header?.trim() ? { key, header } : null;
    })
    .filter((column): column is { key: string; header: string } => column !== null);
}

export async function getLicenseReport(
  mode: LicenseReportMode,
  filters: LicenseReportFilters = {},
  options: LicenseReportRequestOptions = {},
): Promise<LicenseReport> {
  const payload = await requestApi(mode, {
    search: filters.search,
    mode: filters.searchMode,
    department_code: filters.departmentCode,
    location: filters.location,
    status: filters.status,
    from: filters.from,
    to: filters.to,
    month: filters.month,
    year: filters.year,
    page: options.page ?? 1,
    pageSize: options.pageSize ?? DEFAULT_LICENSE_REPORT_PAGE_SIZE,
  });
  const rawRows = reportRows(payload);
  if (!rawRows || !isRecord(payload)) {
    throw new LicenseReportApiError(
      "invalid-response",
      "The FIS API returned an invalid licence report.",
    );
  }

  const columns = reportColumns(payload);
  if (!columns) {
    throw new LicenseReportApiError(
      "invalid-response",
      "The FIS API returned an invalid licence report.",
    );
  }

  const rows = rawRows.reduce<Array<Record<string, string | null>>>((result, row) => {
    if (!isRecord(row)) return result;
    result.push(
      Object.fromEntries(Object.entries(row).map(([key, value]) => [key, asString(value)])),
    );
    return result;
  }, []);
  const totalCount =
    nonNegativeInteger(valueOf(payload, "totalCount", "TotalCount")) ?? rows.length;
  const page = positiveInteger(valueOf(payload, "page", "Page")) ?? 1;
  const pageSize =
    positiveInteger(valueOf(payload, "pageSize", "PageSize")) ?? DEFAULT_LICENSE_REPORT_PAGE_SIZE;
  const totalPages =
    positiveInteger(valueOf(payload, "totalPages", "TotalPages")) ??
    Math.max(1, Math.ceil(totalCount / pageSize));

  return { columns, rows, totalCount, page, pageSize, totalPages };
}
