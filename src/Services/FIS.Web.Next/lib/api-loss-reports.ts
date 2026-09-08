import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type LossReportMode = "vehicle" | "all" | "no-report" | "with-report" | "dept-period";

export type LossReportFilters = {
  vmfCode?: number;
  vehicleNumber?: string;
  searchMode?: "GG" | "GP";
  lossTypeCode?: number;
  department?: string;
  beginDate?: string;
  endDate?: string;
  reportStatus?: string;
  hireType?: "VIP" | "GG" | "Permanent";
};

export type LossReport = {
  columns: string[];
  rows: Array<Record<string, string | null>>;
};

export type LossReportApiErrorReason = "unauthorized" | "unavailable" | "invalid-response";

export class LossReportApiError extends Error {
  constructor(
    public readonly reason: LossReportApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "LossReportApiError";
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
  if (typeof value === "string") return value;
  if (typeof value === "number" || typeof value === "bigint") return String(value);
  return null;
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader)
    throw new LossReportApiError("unauthorized", "No FIS access cookie is available.");

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers: {
        accept: "application/json",
        "content-type": "application/json",
        cookie: cookieHeader,
        ...init.headers,
      },
      signal: controller.signal,
    });

    if (response.status === 401 || response.status === 403) {
      throw new LossReportApiError(
        "unauthorized",
        "The FIS access cookie was rejected.",
        response.status,
      );
    }

    if (!response.ok) {
      let message = `FIS API returned HTTP ${response.status}.`;
      try {
        const payload = await response.clone().json();
        if (isRecord(payload)) {
          message = asString(getValue(payload, "message", "Message", "error")) ?? message;
        }
      } catch {
        // Keep the status-based message when the API has no JSON body.
      }

      throw new LossReportApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
        response.status,
      );
    }

    return response;
  } catch (error) {
    if (error instanceof LossReportApiError) throw error;
    throw new LossReportApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new LossReportApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function getRows(payload: unknown) {
  if (Array.isArray(payload)) return payload;
  if (isRecord(payload)) {
    const rows = getValue(payload, "rows", "Rows", "data", "items", "results");
    return Array.isArray(rows) ? rows : [];
  }

  return [];
}

function mapRow(value: unknown): Record<string, string | null> | null {
  if (!isRecord(value)) return null;
  return Object.fromEntries(Object.entries(value).map(([key, item]) => [key, asString(item)]));
}

function pathForMode(mode: LossReportMode) {
  return {
    vehicle: "api/report/losses/vehicle",
    all: "api/report/losses/all",
    "no-report": "api/report/losses/no-report",
    "with-report": "api/report/losses/with-report",
    "dept-period": "api/report/losses/dept-period",
  }[mode];
}

function toPayload(filters: LossReportFilters) {
  return {
    vmf: filters.vmfCode,
    vehicle_number: filters.vehicleNumber,
    search_mode: filters.searchMode,
    loss_type_code: filters.lossTypeCode,
    department: filters.department,
    begin_date: filters.beginDate,
    end_date: filters.endDate,
    report_status: filters.reportStatus,
    hire_type: filters.hireType,
  };
}

export async function getLossReport(
  mode: LossReportMode,
  filters: LossReportFilters = {},
): Promise<LossReport> {
  const payload = await readJson(
    await requestApi(pathForMode(mode), {
      method: "POST",
      body: JSON.stringify(toPayload(filters)),
    }),
  );
  const rows = getRows(payload)
    .map(mapRow)
    .filter((row): row is Record<string, string | null> => row !== null);
  const columns = rows.length > 0 ? Object.keys(rows[0]) : [];
  return { columns, rows };
}
