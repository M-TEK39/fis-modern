import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
export const DEFAULT_LOSS_TYPE_PAGE_SIZE = 24;
type JsonRecord = Record<string, unknown>;

export type LossTypeRecord = {
  lossTypeCode: number;
  description: string | null;
  dateCreated: string | null;
  dateUpdated: string | null;
  createdByUserCode: number | null;
  modifiedByUserCode: number | null;
  isDeleted: boolean;
};

export type LossTypePage = {
  items: LossTypeRecord[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
};

export type LossTypeWriteInput = {
  description: string;
};

export type LossTypeDeleteDependency = {
  fleetNumber: string | null;
  lossDate: string | null;
  lossReference: string | null;
};

export type LossTypeDeleteCheck = {
  lossCount: number;
  losses: LossTypeDeleteDependency[];
  canDelete: boolean;
  checkAvailable: boolean;
};

export type LossTypeApiErrorReason = "unauthorized" | "unavailable" | "invalid-response";

export class LossTypeApiError extends Error {
  constructor(
    public readonly reason: LossTypeApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "LossTypeApiError";
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

function asNumber(value: unknown) {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  if (typeof value === "string" && value.trim()) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }
  return null;
}

function asString(value: unknown) {
  if (typeof value === "string") return value.trim() || null;
  if (typeof value === "number" || typeof value === "bigint") return String(value);
  return null;
}

function asBoolean(value: unknown) {
  if (typeof value === "boolean") return value;
  if (typeof value === "string") return value.toLowerCase() === "true" || value === "1";
  if (typeof value === "number") return value !== 0;
  return false;
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
      ? (value ?? DEFAULT_LOSS_TYPE_PAGE_SIZE)
      : DEFAULT_LOSS_TYPE_PAGE_SIZE;
  return Math.min(100, pageSize);
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader)
    throw new LossTypeApiError("unauthorized", "No FIS access cookie is available.");

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers: { accept: "application/json", cookie: cookieHeader, ...init.headers },
      signal: controller.signal,
    });
    if (response.status === 401 || response.status === 403) {
      throw new LossTypeApiError(
        "unauthorized",
        "The FIS access cookie was rejected.",
        response.status,
      );
    }
    if (!response.ok) {
      let message = `FIS API returned HTTP ${response.status}.`;
      try {
        const payload = await response.clone().json();
        if (isRecord(payload))
          message = asString(getValue(payload, "message", "Message", "error")) ?? message;
      } catch {
        // Keep the status-based message when the API body is not JSON.
      }
      throw new LossTypeApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
        response.status,
      );
    }
    return response;
  } catch (error) {
    if (error instanceof LossTypeApiError) throw error;
    throw new LossTypeApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new LossTypeApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapLossType(value: unknown): LossTypeRecord | null {
  if (!isRecord(value)) return null;
  const lossTypeCode = asNumber(
    getValue(value, "loss_type_code", "LossTypeCode", "lossTypeCode", "loss_code", "LossCode"),
  );
  if (lossTypeCode === null) return null;

  return {
    lossTypeCode,
    description: asString(getValue(value, "loss_description", "Description", "description")),
    dateCreated: asString(getValue(value, "date_created", "DateCreated", "dateCreated")),
    dateUpdated: asString(getValue(value, "date_updated", "DateUpdated", "dateUpdated")),
    createdByUserCode: asNumber(
      getValue(value, "created_by_user_code", "CreatedByUserCode", "createdByUserCode"),
    ),
    modifiedByUserCode: asNumber(
      getValue(value, "modified_by_user_code", "ModifiedByUserCode", "modifiedByUserCode"),
    ),
    isDeleted: asBoolean(getValue(value, "is_deleted", "IsDeleted", "isDeleted")),
  };
}

function mapLossDependency(value: unknown): LossTypeDeleteDependency | null {
  if (!isRecord(value)) return null;
  return {
    fleetNumber: asString(getValue(value, "fleetNumber", "FleetNumber", "fleet_number")),
    lossDate: asString(getValue(value, "lossDate", "LossDate", "loss_date")),
    lossReference: asString(getValue(value, "lossReference", "LossReference", "loss_reference")),
  };
}

export async function getLossTypes() {
  const payload = await readJson(await requestApi("api/losstype"));
  if (!Array.isArray(payload))
    throw new LossTypeApiError("invalid-response", "The loss type response was not a list.");
  return payload.map(mapLossType).filter((item): item is LossTypeRecord => item !== null);
}

export async function getLossTypesPage(
  options: { page?: number; pageSize?: number; searchTerm?: string } = {},
): Promise<LossTypePage> {
  const params = new URLSearchParams({
    page: String(normalizePage(options.page)),
    pageSize: String(normalizePageSize(options.pageSize)),
  });
  const searchTerm = options.searchTerm?.trim();
  if (searchTerm) params.set("searchTerm", searchTerm);

  const payload = await readJson(await requestApi(`api/losstype/page?${params.toString()}`));
  if (!isRecord(payload) || !Array.isArray(payload.items)) {
    throw new LossTypeApiError(
      "invalid-response",
      "The FIS API returned an invalid loss type page.",
    );
  }

  const metadata = readPageMetadata(payload);
  if (!metadata) {
    throw new LossTypeApiError(
      "invalid-response",
      "The FIS API returned incomplete loss type pagination metadata.",
    );
  }

  return {
    items: payload.items.map(mapLossType).filter((item): item is LossTypeRecord => item !== null),
    ...metadata,
  };
}

export async function getLossType(lossTypeCode: number) {
  return mapLossType(
    await readJson(await requestApi(`api/losstype/${encodeURIComponent(lossTypeCode)}`)),
  );
}

export async function getLossTypeDeleteCheck(lossTypeCode: number): Promise<LossTypeDeleteCheck> {
  const payload = await readJson(
    await requestApi(`api/losstype/${encodeURIComponent(lossTypeCode)}/delete-check`),
  );
  if (!isRecord(payload))
    throw new LossTypeApiError(
      "invalid-response",
      "The loss type dependency response was invalid.",
    );
  const rawLosses = getValue(payload, "losses", "Losses");
  return {
    lossCount:
      asNumber(getValue(payload, "lossCount", "LossCount")) ??
      (Array.isArray(rawLosses) ? rawLosses.length : 0),
    losses: Array.isArray(rawLosses)
      ? rawLosses
          .map(mapLossDependency)
          .filter((item): item is LossTypeDeleteDependency => item !== null)
      : [],
    canDelete: asBoolean(getValue(payload, "canDelete", "CanDelete")),
    checkAvailable: asBoolean(getValue(payload, "checkAvailable", "CheckAvailable")),
  };
}

export async function createLossType(input: LossTypeWriteInput) {
  return mapLossType(
    await readJson(
      await requestApi("api/losstype", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ loss_description: input.description }),
      }),
    ),
  );
}

export async function updateLossType(lossTypeCode: number, input: LossTypeWriteInput) {
  return mapLossType(
    await readJson(
      await requestApi(`api/losstype/${encodeURIComponent(lossTypeCode)}`, {
        method: "PUT",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ loss_type_code: lossTypeCode, loss_description: input.description }),
      }),
    ),
  );
}

export async function deleteLossType(lossTypeCode: number) {
  await requestApi(`api/losstype/${encodeURIComponent(lossTypeCode)}`, { method: "DELETE" });
}
