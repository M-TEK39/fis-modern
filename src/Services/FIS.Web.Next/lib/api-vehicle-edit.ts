import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type VehicleEditVehicle = {
  vmfCode: number;
  fleetNumber: string;
  registrationNumber: string;
  modelCode: number;
  typeCode: number;
  vehicleStatusCode: number;
  statusDescription: string | null;
  locationCode: number;
  locationDescription: string | null;
  takeOnOdo: number;
  currentOdo: number;
  engineNumber: string;
  chassisNumber: string;
  tare: number | null;
  gvm: number | null;
  yearManufactured: number | null;
  colour: string;
  ifmsVehicleRegisterNumber: string | null;
  natisModelNumber: string | null;
};

export type VehicleUpdateRequest = {
  model_code: number;
  type_code: number;
  vehicle_status_code: number;
  location_code: number;
  fleet_number: string;
  registration_number: string;
  engine_number_1: string;
  chassis_number: string;
  take_on_odo: number;
  current_odo: number;
  tare: number;
  gvm: number | null;
  year_manufactured: number;
  colour: string;
  ifms_vehicle_register_number: string | null;
  natis_model_number: string | null;
  recalculate_tariff: boolean;
};

export type VehicleEditApiErrorReason = "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class VehicleEditApiError extends Error {
  constructor(
    public readonly reason: VehicleEditApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "VehicleEditApiError";
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

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new VehicleEditApiError("unauthorized", "No FIS access cookie is available.");
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
      throw new VehicleEditApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (response.status === 404) {
      throw new VehicleEditApiError("not-found", "The requested vehicle was not found.");
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

      throw new VehicleEditApiError(response.status >= 500 ? "unavailable" : "invalid-response", message);
    }

    return response;
  } catch (error) {
    if (error instanceof VehicleEditApiError) {
      throw error;
    }

    throw new VehicleEditApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new VehicleEditApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapVehicle(payload: unknown): VehicleEditVehicle {
  if (!isRecord(payload)) {
    throw new VehicleEditApiError("invalid-response", "The FIS API returned an invalid vehicle.");
  }

  const vmfCode = asNumber(getValue(payload, "vmf_code", "vmfCode"));
  if (vmfCode === null) {
    throw new VehicleEditApiError("invalid-response", "The FIS API returned a vehicle without a VMF code.");
  }

  return {
    vmfCode,
    fleetNumber: asString(getValue(payload, "fleet_number", "fleetNumber")),
    registrationNumber: asString(getValue(payload, "registration_number", "registrationNumber")),
    modelCode: asNumber(getValue(payload, "model_code", "modelCode")) ?? 0,
    typeCode: asNumber(getValue(payload, "type_code", "typeCode")) ?? 0,
    vehicleStatusCode: asNumber(getValue(payload, "vehicle_status_code", "vehicleStatusCode")) ?? 0,
    statusDescription:
      asString(getValue(payload, "status_description", "statusDescription", "vehicle_status_description")) || null,
    locationCode: asNumber(getValue(payload, "location_code", "locationCode")) ?? 0,
    locationDescription: asString(getValue(payload, "location_description", "locationDescription")) || null,
    takeOnOdo: asNumber(getValue(payload, "take_on_odo", "takeOnOdo")) ?? 0,
    currentOdo: asNumber(getValue(payload, "current_odo", "currentOdo")) ?? 0,
    engineNumber: asString(getValue(payload, "engine_number_1", "engineNumber1", "engine_number")),
    chassisNumber: asString(getValue(payload, "chassis_number", "chassisNumber")),
    tare: asNumber(getValue(payload, "tare")),
    gvm: asNumber(getValue(payload, "gvm")),
    yearManufactured: asNumber(getValue(payload, "year_manufactured", "yearManufactured")),
    colour: asString(getValue(payload, "colour")),
    ifmsVehicleRegisterNumber:
      asString(getValue(payload, "ifms_vehicle_register_number", "ifmsVehicleRegisterNumber")) || null,
    natisModelNumber: asString(getValue(payload, "natis_model_number", "natisModelNumber")) || null,
  };
}

export async function getVehicleForEdit(vmfCode: number) {
  const response = await requestApi(`api/vehicles/${encodeURIComponent(vmfCode)}`);
  return mapVehicle(await readJson(response));
}

export async function updateVehicleAgainstApi(vmfCode: number, request: VehicleUpdateRequest) {
  const response = await requestApi(`api/vehicles/${encodeURIComponent(vmfCode)}`, {
    method: "PUT",
    body: JSON.stringify(request),
  });

  await readJson(response);
  return { ok: true as const };
}
