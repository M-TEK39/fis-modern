import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type VehicleBarcodeVehicle = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  barcode: string | null;
};

export type VehicleBarcodeApiErrorReason =
  "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class VehicleBarcodeApiError extends Error {
  constructor(
    public readonly reason: VehicleBarcodeApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "VehicleBarcodeApiError";
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
    const collection = getValue(payload, "data", "items", "results");
    return Array.isArray(collection) ? collection : [];
  }

  return [];
}

function mapVehicle(value: unknown): VehicleBarcodeVehicle | null {
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
    barcode: asString(getValue(value, "barcode", "bar_code", "barCode")),
  };
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new VehicleBarcodeApiError("unauthorized", "No FIS access cookie is available.");
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
      throw new VehicleBarcodeApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (response.status === 404) {
      throw new VehicleBarcodeApiError("not-found", "The requested vehicle was not found.");
    }

    if (!response.ok) {
      let message = `FIS API returned HTTP ${response.status}.`;
      try {
        const payload = (await response.json()) as unknown;
        if (isRecord(payload)) {
          message = asString(getValue(payload, "message", "error")) || message;
        } else if (typeof payload === "string" && payload.trim()) {
          message = payload.trim();
        }
      } catch {
        // Keep the status-based message when the API has no JSON error body.
      }

      throw new VehicleBarcodeApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
      );
    }

    return response;
  } catch (error) {
    if (error instanceof VehicleBarcodeApiError) {
      throw error;
    }

    throw new VehicleBarcodeApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

export async function searchVehiclesForBarcode(searchTerm: string) {
  const response = await requestApi(
    `api/vehicles/search?searchTerm=${encodeURIComponent(searchTerm)}`,
  );
  const payload = (await response.json()) as unknown;
  return getCollection(payload)
    .map(mapVehicle)
    .filter((vehicle): vehicle is VehicleBarcodeVehicle => vehicle !== null);
}

export async function updateVehicleBarcode(vmfCode: number, barcode: string) {
  await requestApi(`api/vehicles/${encodeURIComponent(vmfCode)}/barcode`, {
    method: "PUT",
    body: JSON.stringify({ barcode }),
  });
}
