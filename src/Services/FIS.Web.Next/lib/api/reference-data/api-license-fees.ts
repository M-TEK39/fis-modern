import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type LicenseFeeRecord = {
  licenceFeeCode: number;
  description: string | null;
  fee: number | null;
  dateCreated: string | null;
  dateUpdated: string | null;
  createdByUserCode: number | null;
  modifiedByUserCode: number | null;
  isDeleted: boolean;
};

export type LicenseFeeWriteInput = {
  description: string;
  fee: number | null;
};

export type LicenseFeeDeleteCheck = {
  modelCount: number;
  canDelete: boolean;
};

export type LicenseFeeApiErrorReason = "unauthorized" | "unavailable" | "invalid-response";

export class LicenseFeeApiError extends Error {
  constructor(
    public readonly reason: LicenseFeeApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "LicenseFeeApiError";
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

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader)
    throw new LicenseFeeApiError("unauthorized", "No FIS access cookie is available.");

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
      throw new LicenseFeeApiError(
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
      throw new LicenseFeeApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
        response.status,
      );
    }
    return response;
  } catch (error) {
    if (error instanceof LicenseFeeApiError) throw error;
    throw new LicenseFeeApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new LicenseFeeApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapLicenseFee(value: unknown): LicenseFeeRecord | null {
  if (!isRecord(value)) return null;
  const licenceFeeCode = asNumber(
    getValue(value, "LicenceFeeCode", "licence_fee_code", "licenceFeeCode"),
  );
  if (licenceFeeCode === null) return null;
  return {
    licenceFeeCode,
    description: asString(
      getValue(value, "Description", "description", "licence_description", "licenceDescription"),
    ),
    fee: asNumber(getValue(value, "Fee", "fee", "licence_fee", "licenceFee")),
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

export async function getLicenseFees() {
  const payload = await readJson(await requestApi("api/licensefee"));
  if (!Array.isArray(payload))
    throw new LicenseFeeApiError("invalid-response", "The licence fee response was not a list.");
  return payload.map(mapLicenseFee).filter((item): item is LicenseFeeRecord => item !== null);
}

export async function getLicenseFee(licenceFeeCode: number) {
  return mapLicenseFee(
    await readJson(await requestApi(`api/licensefee/${encodeURIComponent(licenceFeeCode)}`)),
  );
}

export async function getLicenseFeeDeleteCheck(
  licenceFeeCode: number,
): Promise<LicenseFeeDeleteCheck> {
  const payload = await readJson(
    await requestApi(`api/licensefee/${encodeURIComponent(licenceFeeCode)}/delete-check`),
  );
  if (!isRecord(payload))
    throw new LicenseFeeApiError(
      "invalid-response",
      "The licence fee dependency response was invalid.",
    );
  return {
    modelCount: asNumber(getValue(payload, "modelCount", "ModelCount")) ?? 0,
    canDelete: asBoolean(getValue(payload, "canDelete", "CanDelete")),
  };
}

export async function createLicenseFee(input: LicenseFeeWriteInput) {
  return mapLicenseFee(
    await readJson(
      await requestApi("api/licensefee", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ licence_description: input.description, licence_fee: input.fee }),
      }),
    ),
  );
}

export async function updateLicenseFee(licenceFeeCode: number, input: LicenseFeeWriteInput) {
  return mapLicenseFee(
    await readJson(
      await requestApi(`api/licensefee/${encodeURIComponent(licenceFeeCode)}`, {
        method: "PUT",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          licence_fee_code: licenceFeeCode,
          licence_description: input.description,
          licence_fee: input.fee,
        }),
      }),
    ),
  );
}

export async function deleteLicenseFee(licenceFeeCode: number) {
  await requestApi(`api/licensefee/${encodeURIComponent(licenceFeeCode)}`, { method: "DELETE" });
}
