import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type ClearanceVehicle = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
};

export type ClearanceRecord = {
  clearanceCode: number;
  vmfCode: number;
  clearanceNumber: number | null;
  clearanceDate: string | null;
  merchantCode: number | null;
  amount: number | null;
  comment: string | null;
  kilos: number | null;
};

export type MerchantRecord = {
  merchantCode: number;
  merchantName: string | null;
};

export type ClearanceRequest = {
  clearance_code?: number;
  vmf_code: number;
  clearance_number: number;
  Clearance_date: string;
  Merchant_code: number | null;
  Clearance_amount: number | null;
  clearance_comment: string;
  clearance_kilo: number | null;
};

export type MerchantRequest = {
  Merchant_Name: string;
};

export type ClearanceApiErrorReason = "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class ClearanceApiError extends Error {
  constructor(
    public readonly reason: ClearanceApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "ClearanceApiError";
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
    if (key in record) {
      return record[key];
    }
  }

  return undefined;
}

function asString(value: unknown) {
  if (typeof value === "string") {
    return value.trim() || null;
  }

  if (typeof value === "number" || typeof value === "bigint") {
    return String(value);
  }

  return null;
}

function asNumber(value: unknown) {
  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }

  if (typeof value === "string" && value.trim()) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }

  return null;
}

function getCollection(payload: unknown) {
  if (Array.isArray(payload)) {
    return payload;
  }

  if (isRecord(payload)) {
    const value = getValue(payload, "data", "items", "results");
    return Array.isArray(value) ? value : [];
  }

  return [];
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new ClearanceApiError("unauthorized", "No FIS access cookie is available.");
  }

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
      throw new ClearanceApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (response.status === 404) {
      throw new ClearanceApiError("not-found", "The requested clearance record was not found.");
    }

    if (!response.ok) {
      throw new ClearanceApiError("invalid-response", `FIS API returned HTTP ${response.status}.`);
    }

    return response;
  } catch (error) {
    if (error instanceof ClearanceApiError) {
      throw error;
    }

    throw new ClearanceApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new ClearanceApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapVehicle(value: unknown): ClearanceVehicle | null {
  if (!isRecord(value)) {
    return null;
  }

  const vmfCode = asNumber(getValue(value, "vmf_code", "vmfCode"));
  if (vmfCode === null) {
    return null;
  }

  return {
    vmfCode,
    fleetNumber: asString(getValue(value, "fleet_number", "fleetNumber")),
    registrationNumber: asString(getValue(value, "registration_number", "registrationNumber")),
  };
}

function mapClearance(value: unknown): ClearanceRecord | null {
  if (!isRecord(value)) {
    return null;
  }

  const clearanceCode = asNumber(getValue(value, "clearance_code", "clearanceCode"));
  const vmfCode = asNumber(getValue(value, "vmf_code", "vmfCode"));
  if (clearanceCode === null || vmfCode === null) {
    return null;
  }

  return {
    clearanceCode,
    vmfCode,
    clearanceNumber: asNumber(getValue(value, "clearance_number", "clearanceNumber")),
    clearanceDate: asString(getValue(value, "Clearance_date", "clearance_date", "clearanceDate")),
    merchantCode: asNumber(getValue(value, "Merchant_code", "merchant_code", "merchantCode")),
    amount: asNumber(getValue(value, "Clearance_amount", "clearance_amount", "clearanceAmount")),
    comment: asString(getValue(value, "clearance_comment", "Clearance_Comment", "comment")),
    kilos: asNumber(getValue(value, "clearance_kilo", "clearanceKilo", "kilos")),
  };
}

function mapMerchant(value: unknown): MerchantRecord | null {
  if (!isRecord(value)) {
    return null;
  }

  const merchantCode = asNumber(getValue(value, "Merchant_code", "merchant_code", "merchantCode"));
  if (merchantCode === null) {
    return null;
  }

  return {
    merchantCode,
    merchantName: asString(getValue(value, "Merchant_Name", "merchant_name", "merchantName")),
  };
}

export async function lookupClearanceVehicle(identifier: string) {
  const response = await requestApi(`api/clearance/lookup/${encodeURIComponent(identifier)}`);
  return mapVehicle(await readJson(response));
}

export async function getClearanceVehicle(vmfCode: number) {
  const response = await requestApi(`api/vehicles/${encodeURIComponent(vmfCode)}`);
  const vehicle = mapVehicle(await readJson(response));
  if (!vehicle) {
    throw new ClearanceApiError("invalid-response", "The FIS API returned an invalid vehicle record.");
  }

  return vehicle;
}

export async function getClearancesForVehicle(vmfCode: number) {
  const response = await requestApi(`api/clearance/vehicle/${encodeURIComponent(vmfCode)}`);
  return getCollection(await readJson(response))
    .map(mapClearance)
    .filter((record): record is ClearanceRecord => record !== null)
    .sort((left, right) => (right.clearanceDate ?? "").localeCompare(left.clearanceDate ?? ""));
}

export async function getClearance(clearanceCode: number) {
  const response = await requestApi(`api/clearance/${encodeURIComponent(clearanceCode)}`);
  const record = mapClearance(await readJson(response));
  if (!record) {
    throw new ClearanceApiError("invalid-response", "The FIS API returned an invalid clearance record.");
  }

  return record;
}

export async function getMerchants() {
  const response = await requestApi("api/merchant");
  return getCollection(await readJson(response))
    .map(mapMerchant)
    .filter((merchant): merchant is MerchantRecord => merchant !== null)
    .sort((left, right) => (left.merchantName ?? "").localeCompare(right.merchantName ?? ""));
}

export async function createClearanceAgainstApi(request: ClearanceRequest) {
  const response = await requestApi("api/clearance", { method: "POST", body: JSON.stringify(request) });
  return mapClearance(await readJson(response));
}

export async function updateClearanceAgainstApi(clearanceCode: number, request: ClearanceRequest) {
  const response = await requestApi(`api/clearance/${encodeURIComponent(clearanceCode)}`, {
    method: "PUT",
    body: JSON.stringify({ ...request, clearance_code: clearanceCode }),
  });
  return mapClearance(await readJson(response));
}

export async function deleteClearanceAgainstApi(clearanceCode: number) {
  await requestApi(`api/clearance/${encodeURIComponent(clearanceCode)}`, { method: "DELETE" });
}

export async function createMerchantAgainstApi(request: MerchantRequest) {
  const response = await requestApi("api/merchant", { method: "POST", body: JSON.stringify(request) });
  return mapMerchant(await readJson(response));
}

export async function updateMerchantAgainstApi(merchantCode: number, request: MerchantRequest) {
  const response = await requestApi(`api/merchant/${encodeURIComponent(merchantCode)}`, {
    method: "PUT",
    body: JSON.stringify(request),
  });
  return mapMerchant(await readJson(response));
}
