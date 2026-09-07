import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type TripAuthorityVehicle = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  licenceDueDate: string | null;
  make: string | null;
  model: string | null;
  contractType: string | null;
  siteCode: number | null;
};

export type TripAuthorityRecord = {
  tripId: number;
  contractCode: number;
  vmfCode: number | null;
  siteCode: number | null;
  endOdometer: number | null;
  issueDate: string | null;
  expiryDate: string | null;
};

export class TripAuthorityApiError extends Error {
  constructor(
    public readonly reason: "unauthorized" | "unavailable" | "invalid-response",
    message: string,
  ) {
    super(message);
    this.name = "TripAuthorityApiError";
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

function getCollection(payload: unknown) {
  if (Array.isArray(payload)) return payload;
  if (isRecord(payload)) {
    const collection = getValue(payload, "data", "items", "results");
    return Array.isArray(collection) ? collection : [];
  }

  return [];
}

function statusDescriptionForCode(statusCode: number) {
  const descriptions: Record<number, string> = {
    1: "In Service",
    2: "Withdrawn",
    3: "Board of Survey",
    4: "Stolen",
    5: "Sold",
    6: "Transferred",
    7: "Subsidized",
    8: "From Focus",
    9: "Privatised",
    10: "Recovered",
    11: "Missing",
    12: "Destroyed",
  };

  return descriptions[statusCode] ?? null;
}

function mapVehicle(value: unknown): TripAuthorityVehicle | null {
  if (!isRecord(value)) return null;

  const vmfCode = asNumber(getValue(value, "vmf_code", "vmfCode"));
  if (vmfCode === null) return null;

  const statusCode = asNumber(getValue(value, "vehicle_status_code", "vehicleStatusCode")) ?? 0;
  return {
    vmfCode,
    fleetNumber: asString(getValue(value, "fleetNumber", "fleet_number")),
    registrationNumber: asString(getValue(value, "registrationNumber", "registration_number")),
    licenceDueDate: asString(getValue(value, "licenceDueDate", "licence_due_date")),
    make: asString(getValue(value, "makeDescription", "make_description", "make")),
    model: asString(getValue(value, "modelDescription", "model_description", "model")),
    contractType: asString(getValue(value, "contractType", "contract_type")) ?? statusDescriptionForCode(statusCode),
    siteCode: asNumber(getValue(value, "siteCode", "site_code")),
  };
}

function mapTrip(value: unknown): TripAuthorityRecord | null {
  if (!isRecord(value)) return null;

  const tripId = asNumber(getValue(value, "tripAuthorityCode", "trip_authority_code", "tripId"));
  const contractCode = asNumber(getValue(value, "contractCode", "contract_code"));
  if (tripId === null || contractCode === null) return null;

  return {
    tripId,
    contractCode,
    vmfCode: asNumber(getValue(value, "vmfCode", "vmf_code")),
    siteCode: asNumber(getValue(value, "siteCode", "site_code")),
    endOdometer: asNumber(getValue(value, "endOdometer", "end_odo_meter")),
    issueDate: asString(getValue(value, "issueDate", "issue_date")),
    expiryDate: asString(getValue(value, "expiryDate", "expiry_date")),
  };
}

async function requestApi(path: string) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new TripAuthorityApiError("unauthorized", "No FIS access cookie is available.");
  }

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);

  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      cache: "no-store",
      headers: {
        accept: "application/json",
        cookie: cookieHeader,
      },
      signal: controller.signal,
    });

    if (response.status === 401 || response.status === 403) {
      throw new TripAuthorityApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (!response.ok) {
      throw new TripAuthorityApiError("unavailable", `FIS API returned HTTP ${response.status}.`);
    }

    try {
      return (await response.json()) as unknown;
    } catch {
      throw new TripAuthorityApiError("invalid-response", "The FIS API returned invalid JSON.");
    }
  } catch (error) {
    if (error instanceof TripAuthorityApiError) throw error;
    throw new TripAuthorityApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

export async function getTripAuthorityVehicles() {
  const payload = await requestApi("api/Trip/vehicles");
  return getCollection(payload)
    .map(mapVehicle)
    .filter((vehicle): vehicle is TripAuthorityVehicle => vehicle !== null);
}

export async function getTripAuthorities() {
  const payload = await requestApi("api/Trip");
  return getCollection(payload)
    .map(mapTrip)
    .filter((trip): trip is TripAuthorityRecord => trip !== null);
}
