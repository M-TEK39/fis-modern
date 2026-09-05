import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type ExtraCodeRecord = {
  extraCode: number;
  description: string | null;
  categoryTypeCode: number | null;
  specific: number | null;
  additional: number | null;
  dateCreated: string | null;
  dateUpdated: string | null;
  createdByUserCode: number | null;
  modifiedByUserCode: number | null;
  isDeleted: boolean;
};

export type ExtraCodeWriteInput = {
  description: string;
  categoryTypeCode?: number | null;
  specific?: number | null;
  additional?: number | null;
};

export type ExtraCodeDeleteCheck = {
  vehicleCount: number;
  fleetNumbers: string[];
  canDelete: boolean;
  checkAvailable: boolean;
};

export type ExtraCodeApiErrorReason = "unauthorized" | "unavailable" | "invalid-response";

export class ExtraCodeApiError extends Error {
  constructor(
    public readonly reason: ExtraCodeApiErrorReason,
    message: string,
    public readonly status?: number,
    public readonly fleetNumbers: string[] = [],
  ) {
    super(message);
    this.name = "ExtraCodeApiError";
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

function asStringList(value: unknown) {
  if (!Array.isArray(value)) return [];
  return value.map(asString).filter((item): item is string => item !== null);
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) throw new ExtraCodeApiError("unauthorized", "No FIS access cookie is available.");

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
      throw new ExtraCodeApiError("unauthorized", "The FIS access cookie was rejected.", response.status);
    }
    if (!response.ok) {
      let message = `FIS API returned HTTP ${response.status}.`;
      let fleetNumbers: string[] = [];
      try {
        const payload = await response.clone().json();
        if (isRecord(payload)) {
          message = asString(getValue(payload, "message", "Message", "error")) ?? message;
          fleetNumbers = asStringList(getValue(payload, "fleetNumbers", "FleetNumbers"));
        }
      } catch {
        // Keep the status-based message when the API body is not JSON.
      }
      throw new ExtraCodeApiError(response.status >= 500 ? "unavailable" : "invalid-response", message, response.status, fleetNumbers);
    }
    return response;
  } catch (error) {
    if (error instanceof ExtraCodeApiError) throw error;
    throw new ExtraCodeApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new ExtraCodeApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapExtraCode(value: unknown): ExtraCodeRecord | null {
  if (!isRecord(value)) return null;
  const extraCode = asNumber(getValue(value, "extra_code", "ExtraCode", "extraCode"));
  if (extraCode === null) return null;
  return {
    extraCode,
    description: asString(getValue(value, "extra_description", "Description", "description")),
    categoryTypeCode: asNumber(getValue(value, "category_type_code", "CategoryTypeCode", "categoryTypeCode")),
    specific: asNumber(getValue(value, "specific", "Specific")),
    additional: asNumber(getValue(value, "additional", "Additional")),
    dateCreated: asString(getValue(value, "date_created", "DateCreated", "dateCreated")),
    dateUpdated: asString(getValue(value, "date_updated", "DateUpdated", "dateUpdated")),
    createdByUserCode: asNumber(getValue(value, "created_by_user_code", "CreatedByUserCode", "createdByUserCode")),
    modifiedByUserCode: asNumber(getValue(value, "modified_by_user_code", "ModifiedByUserCode", "modifiedByUserCode")),
    isDeleted: asBoolean(getValue(value, "is_deleted", "IsDeleted", "isDeleted")),
  };
}

export async function getExtraCodes() {
  const payload = await readJson(await requestApi("api/extracode"));
  if (!Array.isArray(payload)) throw new ExtraCodeApiError("invalid-response", "The extra code response was not a list.");
  return payload.map(mapExtraCode).filter((item): item is ExtraCodeRecord => item !== null);
}

export async function getExtraCode(extraCode: number) {
  return mapExtraCode(await readJson(await requestApi(`api/extracode/${encodeURIComponent(extraCode)}`)));
}

export async function getExtraCodeDeleteCheck(extraCode: number): Promise<ExtraCodeDeleteCheck> {
  const payload = await readJson(await requestApi(`api/extracode/${encodeURIComponent(extraCode)}/delete-check`));
  if (!isRecord(payload)) throw new ExtraCodeApiError("invalid-response", "The extra code dependency response was invalid.");
  return {
    vehicleCount: asNumber(getValue(payload, "vehicleCount", "VehicleCount")) ?? 0,
    fleetNumbers: asStringList(getValue(payload, "fleetNumbers", "FleetNumbers")),
    canDelete: asBoolean(getValue(payload, "canDelete", "CanDelete")),
    checkAvailable: asBoolean(getValue(payload, "checkAvailable", "CheckAvailable")),
  };
}

export async function createExtraCode(input: ExtraCodeWriteInput) {
  return mapExtraCode(await readJson(await requestApi("api/extracode", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({
      extra_description: input.description,
      category_type_code: input.categoryTypeCode ?? null,
      specific: input.specific ?? null,
      additional: input.additional ?? null,
    }),
  })));
}

export async function deleteExtraCode(extraCode: number) {
  await requestApi(`api/extracode/${encodeURIComponent(extraCode)}`, { method: "DELETE" });
}
