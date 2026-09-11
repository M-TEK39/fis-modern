import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
export const DEFAULT_DRIVER_LICENCE_PAGE_SIZE = 24;
type JsonRecord = Record<string, unknown>;

export type DriverLicenceRecord = {
  licenceCode: number;
  description: string | null;
  dateCreated: string | null;
  dateUpdated: string | null;
  createdByUserCode: number | null;
  modifiedByUserCode: number | null;
  isDeleted: boolean;
};

export type DriverLicencePage = {
  items: DriverLicenceRecord[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
};

export type DriverLicenceWriteInput = {
  description: string;
};

export type DriverLicenceDeleteCheck = {
  modelCount: number;
  canDelete: boolean;
};

export type DriverLicenceApiErrorReason = "unauthorized" | "unavailable" | "invalid-response";

export class DriverLicenceApiError extends Error {
  constructor(
    public readonly reason: DriverLicenceApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "DriverLicenceApiError";
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
      ? (value ?? DEFAULT_DRIVER_LICENCE_PAGE_SIZE)
      : DEFAULT_DRIVER_LICENCE_PAGE_SIZE;
  return Math.min(100, pageSize);
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader)
    throw new DriverLicenceApiError("unauthorized", "No FIS access cookie is available.");

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
      throw new DriverLicenceApiError(
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
      throw new DriverLicenceApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
        response.status,
      );
    }
    return response;
  } catch (error) {
    if (error instanceof DriverLicenceApiError) throw error;
    throw new DriverLicenceApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new DriverLicenceApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapDriverLicence(value: unknown): DriverLicenceRecord | null {
  if (!isRecord(value)) return null;
  const licenceCode = asNumber(getValue(value, "LicenceCode", "licence_code", "licenceCode"));
  if (licenceCode === null) return null;

  return {
    licenceCode,
    description: asString(getValue(value, "Description", "description")),
    dateCreated: asString(getValue(value, "DateCreated", "date_created", "dateCreated")),
    dateUpdated: asString(getValue(value, "DateUpdated", "date_updated", "dateUpdated")),
    createdByUserCode: asNumber(
      getValue(value, "CreatedByUserCode", "created_by_user_code", "createdByUserCode"),
    ),
    modifiedByUserCode: asNumber(
      getValue(value, "ModifiedByUserCode", "modified_by_user_code", "modifiedByUserCode"),
    ),
    isDeleted: asBoolean(getValue(value, "IsDeleted", "is_deleted", "isDeleted")),
  };
}

export async function getDriverLicences() {
  const payload = await readJson(await requestApi("api/DriverLicence"));
  if (!Array.isArray(payload))
    throw new DriverLicenceApiError(
      "invalid-response",
      "The driver licence response was not a list.",
    );
  return payload.map(mapDriverLicence).filter((item): item is DriverLicenceRecord => item !== null);
}

export async function getDriverLicencesPage(
  options: { page?: number; pageSize?: number; searchTerm?: string } = {},
): Promise<DriverLicencePage> {
  const params = new URLSearchParams({
    page: String(normalizePage(options.page)),
    pageSize: String(normalizePageSize(options.pageSize)),
  });
  const searchTerm = options.searchTerm?.trim();
  if (searchTerm) params.set("searchTerm", searchTerm);

  const payload = await readJson(await requestApi(`api/DriverLicence/page?${params.toString()}`));
  if (!isRecord(payload) || !Array.isArray(payload.items)) {
    throw new DriverLicenceApiError(
      "invalid-response",
      "The FIS API returned an invalid driver licence page.",
    );
  }

  const metadata = readPageMetadata(payload);
  if (!metadata) {
    throw new DriverLicenceApiError(
      "invalid-response",
      "The FIS API returned incomplete driver licence pagination metadata.",
    );
  }

  return {
    items: payload.items
      .map(mapDriverLicence)
      .filter((item): item is DriverLicenceRecord => item !== null),
    ...metadata,
  };
}

export async function getDriverLicence(licenceCode: number) {
  return mapDriverLicence(
    await readJson(await requestApi(`api/DriverLicence/${encodeURIComponent(licenceCode)}`)),
  );
}

export async function getDriverLicenceDeleteCheck(
  licenceCode: number,
): Promise<DriverLicenceDeleteCheck> {
  const payload = await readJson(
    await requestApi(`api/DriverLicence/${encodeURIComponent(licenceCode)}/delete-check`),
  );
  if (!isRecord(payload))
    throw new DriverLicenceApiError(
      "invalid-response",
      "The driver licence dependency response was invalid.",
    );
  return {
    modelCount: asNumber(getValue(payload, "modelCount", "ModelCount")) ?? 0,
    canDelete: asBoolean(getValue(payload, "canDelete", "CanDelete")),
  };
}

export async function createDriverLicence(input: DriverLicenceWriteInput) {
  return mapDriverLicence(
    await readJson(
      await requestApi("api/DriverLicence", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ description: input.description }),
      }),
    ),
  );
}

export async function updateDriverLicence(licenceCode: number, input: DriverLicenceWriteInput) {
  return mapDriverLicence(
    await readJson(
      await requestApi(`api/DriverLicence/${encodeURIComponent(licenceCode)}`, {
        method: "PUT",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ licence_code: licenceCode, description: input.description }),
      }),
    ),
  );
}

export async function deleteDriverLicence(licenceCode: number) {
  await requestApi(`api/DriverLicence/${encodeURIComponent(licenceCode)}`, { method: "DELETE" });
}
