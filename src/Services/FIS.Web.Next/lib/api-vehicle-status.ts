import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;

type JsonRecord = Record<string, unknown>;

export type VehicleStatusOption = {
  code: number;
  description: string;
};

export type VehicleStatusVehicle = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  modelName: string | null;
  typeName: string | null;
  statusCode: number;
  statusDescription: string | null;
  statusDate: string | null;
  locationCode: number | null;
  locationDescription: string | null;
  siteCode: number | null;
  yearManufactured: number | null;
  colour: string | null;
  chassisNumber: string | null;
  engineNumber: string | null;
  hiredFrom: string | null;
  currentOdo: number | null;
};

export type VehicleStatusSite = {
  code: number;
  description: string;
};

export type VehicleStatusChangeResult = {
  vmfCode: number;
  previousStatusCode: number | null;
  newStatusCode: number | null;
  newStatusDescription: string | null;
  effectiveDate: string | null;
  locationCode: number | null;
  actionsPerformed: string[];
};

export type VehicleStatusApiErrorReason = "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class VehicleStatusApiError extends Error {
  constructor(
    public readonly reason: VehicleStatusApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "VehicleStatusApiError";
  }
}

export const VEHICLE_STATUS_OPTIONS: readonly VehicleStatusOption[] = [
  { code: 1, description: "In Service" },
  { code: 2, description: "Withdrawn" },
  { code: 3, description: "Board of Survey" },
  { code: 4, description: "Stolen" },
  { code: 5, description: "Sold" },
  { code: 6, description: "Transferred" },
  { code: 7, description: "Subsidized" },
  { code: 8, description: "From Focus" },
  { code: 9, description: "Privatised" },
  { code: 10, description: "Recovered" },
  { code: 11, description: "Missing" },
  { code: 12, description: "Destroyed" },
];

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
    return value.trim();
  }

  if (typeof value === "number" || typeof value === "bigint") {
    return String(value);
  }

  return "";
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

function mapPresent<T>(values: readonly unknown[], mapper: (value: unknown) => T | null) {
  const result: T[] = [];
  for (const value of values) {
    const mapped = mapper(value);
    if (mapped !== null) {
      result.push(mapped);
    }
  }
  return result;
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new VehicleStatusApiError("unauthorized", "No FIS access cookie is available.");
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
      throw new VehicleStatusApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (response.status === 404) {
      throw new VehicleStatusApiError("not-found", "The requested vehicle was not found.");
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

      throw new VehicleStatusApiError(response.status >= 500 ? "unavailable" : "invalid-response", message);
    }

    return response;
  } catch (error) {
    if (error instanceof VehicleStatusApiError) {
      throw error;
    }

    throw new VehicleStatusApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new VehicleStatusApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapVehicle(value: unknown): VehicleStatusVehicle | null {
  if (!isRecord(value)) {
    return null;
  }

  const vmfCode = asNumber(getValue(value, "vmf_code", "vmfCode"));
  if (vmfCode === null) {
    return null;
  }

  return {
    vmfCode,
    fleetNumber: asString(getValue(value, "fleet_number", "fleetNumber")) || null,
    registrationNumber: asString(getValue(value, "registration_number", "registrationNumber")) || null,
    modelName:
      asString(getValue(value, "model_name", "modelName", "model_description", "modelDescription")) || null,
    typeName: asString(getValue(value, "type_name", "typeName", "type_description", "typeDescription")) || null,
    statusCode: asNumber(getValue(value, "vehicle_status_code", "vehicleStatusCode")) ?? 0,
    statusDescription:
      asString(getValue(value, "status_description", "statusDescription", "vehicle_status_description")) || null,
    statusDate: asString(getValue(value, "vehicle_status_date", "vehicleStatusDate")) || null,
    locationCode: asNumber(getValue(value, "location_code", "locationCode")),
    locationDescription: asString(getValue(value, "location_description", "locationDescription")) || null,
    siteCode: asNumber(getValue(value, "site_code", "siteCode", "location_code", "locationCode")),
    yearManufactured: asNumber(getValue(value, "year_manufactured", "yearManufactured")),
    colour: asString(getValue(value, "colour")) || null,
    chassisNumber: asString(getValue(value, "chassis_number", "chassisNumber")) || null,
    engineNumber: asString(getValue(value, "engine_number_1", "engineNumber1", "engine_number")) || null,
    hiredFrom: asString(getValue(value, "purchased_from", "purchasedFrom", "hired_from", "hiredFrom")) || null,
    currentOdo: asNumber(getValue(value, "current_odo", "currentOdo")),
  };
}

export async function searchVehiclesForStatus(searchTerm: string) {
  const response = await requestApi(`api/vehicles/search?searchTerm=${encodeURIComponent(searchTerm)}`);
  return getCollection(await readJson(response))
    .map(mapVehicle)
    .filter((vehicle): vehicle is VehicleStatusVehicle => vehicle !== null);
}

export async function getVehicleForStatus(vmfCode: number) {
  const response = await requestApi(`api/vehicles/${encodeURIComponent(vmfCode)}`);
  const vehicle = mapVehicle(await readJson(response));
  if (!vehicle) {
    throw new VehicleStatusApiError("invalid-response", "The FIS API returned an invalid vehicle.");
  }

  return vehicle;
}

export async function getSitesForVehicleStatus() {
  const response = await requestApi("api/site");
  return getCollection(await readJson(response))
    .map((value) => {
      if (!isRecord(value)) {
        return null;
      }

      const code = asNumber(getValue(value, "site_code", "siteCode"));
      const description = asString(getValue(value, "description", "site_description", "siteDescription"));
      return code !== null && description ? { code, description } satisfies VehicleStatusSite : null;
    })
    .filter((site): site is VehicleStatusSite => site !== null)
    .toSorted((left, right) => left.description.localeCompare(right.description));
}

export async function changeVehicleStatusAgainstApi(
  vmfCode: number,
  newStatusCode: number,
  siteCode: number | null,
  effectiveDate: string,
  notes: string,
) {
  const response = await requestApi(`api/vehicles/${encodeURIComponent(vmfCode)}/status`, {
    method: "PATCH",
    body: JSON.stringify({
      new_status_code: newStatusCode,
      site_code: siteCode,
      effective_date: `${effectiveDate}T00:00:00.000Z`,
      notes: notes || null,
    }),
  });

  const payload = await readJson(response);
  if (!isRecord(payload)) {
    throw new VehicleStatusApiError("invalid-response", "The FIS API returned an invalid status response.");
  }

  return {
    vmfCode: asNumber(getValue(payload, "vmf_code", "vmfCode")) ?? vmfCode,
    previousStatusCode: asNumber(getValue(payload, "previous_status_code", "previousStatusCode")),
    newStatusCode: asNumber(getValue(payload, "new_status_code", "newStatusCode")),
    newStatusDescription:
      asString(getValue(payload, "new_status_description", "newStatusDescription")) || null,
    effectiveDate: asString(getValue(payload, "effective_date", "effectiveDate")) || null,
    locationCode: asNumber(getValue(payload, "location_code", "locationCode")),
    actionsPerformed: mapPresent(getCollection(getValue(payload, "actions_performed", "actionsPerformed")), asString),
  } satisfies VehicleStatusChangeResult;
}
