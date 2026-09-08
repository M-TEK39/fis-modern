import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type LegacyReport = {
  reportKey: string;
  title: string;
  legacyTarget: string | null;
  isApproximate: boolean;
  approximationReason: string | null;
  columns: Array<{ key: string; header: string }>;
  rows: Array<Record<string, string | null>>;
  totalCount: number;
};

export type LegacyReportApiErrorReason = "unauthorized" | "unavailable" | "invalid-response";

export class LegacyReportApiError extends Error {
  constructor(
    public readonly reason: LegacyReportApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "LegacyReportApiError";
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

async function requestApi(path: string) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) throw new LegacyReportApiError("unauthorized", "No FIS access cookie is available.");

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      cache: "no-store",
      headers: { accept: "application/json", cookie: cookieHeader },
      signal: controller.signal,
    });

    if (response.status === 401 || response.status === 403) {
      throw new LegacyReportApiError("unauthorized", "The FIS access cookie was rejected.", response.status);
    }

    if (!response.ok) {
      let message = `FIS API returned HTTP ${response.status}.`;
      try {
        const payload = await response.clone().json();
        if (isRecord(payload)) message = asString(getValue(payload, "message", "Message", "error")) ?? message;
      } catch {
        // Keep the status-based message when the API has no JSON body.
      }

      throw new LegacyReportApiError(response.status >= 500 ? "unavailable" : "invalid-response", message, response.status);
    }

    try {
      return (await response.json()) as unknown;
    } catch {
      throw new LegacyReportApiError("invalid-response", "The FIS API returned invalid JSON.");
    }
  } catch (error) {
    if (error instanceof LegacyReportApiError) throw error;
    throw new LegacyReportApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

function mapReport(value: unknown): LegacyReport | null {
  if (!isRecord(value)) return null;
  const rawColumns = getValue(value, "columns", "Columns");
  const rawRows = getValue(value, "rows", "Rows");
  if (!Array.isArray(rawColumns) || !Array.isArray(rawRows)) return null;

  const columns = rawColumns
    .map((column) => {
      if (!isRecord(column)) return null;
      const key = asString(getValue(column, "key", "Key"));
      const header = asString(getValue(column, "header", "Header"));
      return key && header ? { key, header } : null;
    })
    .filter((column): column is { key: string; header: string } => column !== null);
  const rows = rawRows
    .map((row) => {
      if (!isRecord(row)) return null;
      return Object.fromEntries(Object.entries(row).map(([key, item]) => [key, asString(item)]));
    })
    .filter((row): row is Record<string, string | null> => row !== null);
  const title = asString(getValue(value, "title", "Title"));
  if (!title || columns.length === 0) return null;

  return {
    reportKey: asString(getValue(value, "reportKey", "ReportKey")) ?? "",
    title,
    legacyTarget: asString(getValue(value, "legacyTarget", "LegacyTarget")),
    isApproximate: getValue(value, "isApproximate", "IsApproximate") === true,
    approximationReason: asString(getValue(value, "approximationReason", "ApproximationReason")),
    columns,
    rows,
    totalCount: asNumber(getValue(value, "totalCount", "TotalCount")) ?? rows.length,
  };
}

export async function getLegacyReport(
  reportKey: string,
  filters: Record<string, string | number | undefined> = {},
) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(filters)) {
    if (value !== undefined && String(value).trim() !== "") params.set(key, String(value));
  }

  const query = params.size > 0 ? `?${params.toString()}` : "";
  const report = mapReport(await requestApi(`api/report/dynamic/${encodeURIComponent(reportKey)}${query}`));
  if (!report) throw new LegacyReportApiError("invalid-response", "The FIS API returned an invalid legacy report.");
  return report;
}

export type ReportHelp = {
  sections: Array<{ title: string; content: string }>;
};

export async function getReportHelp(): Promise<ReportHelp> {
  const value = await requestApi("api/report/help");
  if (!isRecord(value)) return { sections: [] };

  const rawSections = getValue(value, "sections", "Sections");
  if (!Array.isArray(rawSections)) return { sections: [] };

  return {
    sections: rawSections
      .map((section) => {
        if (!isRecord(section)) return null;
        const title = asString(getValue(section, "title", "Title"));
        const content = asString(getValue(section, "content", "Content"));
        return title && content ? { title, content } : null;
      })
      .filter((section): section is { title: string; content: string } => section !== null),
  };
}
