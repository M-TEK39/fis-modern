import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type VehiclePhotoSearchRecord = {
  vmfCode: number;
  ggNumber: string | null;
  registrationNumber: string | null;
  makeAndModel: string | null;
  yearManufactured: number | null;
  colour: string | null;
  hireType: string | null;
  status: string | null;
  hiredFrom: string | null;
  statusDate: string | null;
};

export type VehiclePhotoRecord = {
  vehiclePhotoInfoCode: number;
  vehicleMasterCode: number;
  fileUrl: string | null;
  orientation: number | null;
  description: string | null;
};

export type VehiclePhotoApiErrorReason = "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class VehiclePhotoApiError extends Error {
  constructor(public readonly reason: VehiclePhotoApiErrorReason, message: string) {
    super(message);
    this.name = "VehiclePhotoApiError";
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

function getCollection(payload: unknown) {
  if (Array.isArray(payload)) return payload;
  if (!isRecord(payload)) return [];
  const collection = getValue(payload, "data", "items", "results");
  return Array.isArray(collection) ? collection : [];
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) throw new VehiclePhotoApiError("unauthorized", "No FIS access cookie is available.");

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);

  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers: {
        accept: "application/json",
        cookie: cookieHeader,
        ...(typeof init.body === "string" ? { "content-type": "application/json" } : {}),
        ...init.headers,
      },
      signal: controller.signal,
    });

    if (response.status === 401 || response.status === 403) {
      throw new VehiclePhotoApiError("unauthorized", "The FIS access cookie was rejected.");
    }
    if (response.status === 404) {
      throw new VehiclePhotoApiError("not-found", "The requested vehicle or photo was not found.");
    }
    if (!response.ok) {
      let message = `FIS API returned HTTP ${response.status}.`;
      try {
        const payload = (await response.json()) as unknown;
        if (isRecord(payload)) message = asString(getValue(payload, "message", "error")) ?? message;
        else if (typeof payload === "string" && payload.trim()) message = payload.trim();
      } catch {
        // Keep the status-based message when the API has no JSON error body.
      }
      throw new VehiclePhotoApiError(response.status >= 500 ? "unavailable" : "invalid-response", message);
    }

    return response;
  } catch (error) {
    if (error instanceof VehiclePhotoApiError) throw error;
    throw new VehiclePhotoApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new VehiclePhotoApiError("invalid-response", "The FIS API returned invalid vehicle photo JSON.");
  }
}

function mapVehicle(value: unknown): VehiclePhotoSearchRecord | null {
  if (!isRecord(value)) return null;
  const vmfCode = asNumber(getValue(value, "vmfCode", "vmf_code", "id"));
  if (vmfCode === null) return null;

  const modelCode = asNumber(getValue(value, "modelCode", "model_code"));
  const modelName = asString(getValue(value, "modelName", "model_name"));
  return {
    vmfCode,
    ggNumber: asString(getValue(value, "ggNumber", "GGNumber", "fleetNumber", "fleet_number")),
    registrationNumber: asString(getValue(value, "registrationNumber", "RegistrationNumber", "registration_number")),
    makeAndModel: asString(getValue(value, "makeAndModel", "MakeAndModel", "modelName", "model_name"))
      ?? (modelCode === null ? null : `Model ${modelCode}`),
    yearManufactured: asNumber(getValue(value, "yearManufactured", "YearManufactured", "year_manufactured")),
    colour: asString(getValue(value, "colour", "Colour")),
    hireType: asString(getValue(value, "hireType", "HireType")),
    status: asString(getValue(value, "status", "Status", "statusDescription", "status_description")),
    hiredFrom: asString(getValue(value, "hiredFrom", "HiredFrom")),
    statusDate: asString(getValue(value, "statusDate", "StatusDate", "vehicleStatusDate", "vehicle_status_date")),
  };
}

function mapPhoto(value: unknown): VehiclePhotoRecord | null {
  if (!isRecord(value)) return null;
  const vehiclePhotoInfoCode = asNumber(getValue(value, "vehiclePhotoInfoCode", "VehiclePhotoInfoCode", "id"));
  const vehicleMasterCode = asNumber(getValue(value, "vehicleMasterCode", "VehicleMasterCode", "vmfCode"));
  if (vehiclePhotoInfoCode === null || vehicleMasterCode === null) return null;
  return {
    vehiclePhotoInfoCode,
    vehicleMasterCode,
    fileUrl: asString(getValue(value, "fileUrl", "FileUrl")),
    orientation: asNumber(getValue(value, "orientation", "Orientation")),
    description: asString(getValue(value, "description", "Description")),
  };
}

export async function getVehicleSearchCriteria() {
  const response = await requestApi("api/VehicleSearchCriteria");
  const payload = await readJson(response);
  return getCollection(payload).map(asString).filter((value): value is string => value !== null);
}

export async function searchVehiclePhotos(keyword: string) {
  if (!keyword.trim()) return [];
  const response = await requestApi(`api/VehicleLookup?keyword=${encodeURIComponent(keyword.trim())}&limit=100`);
  return getCollection(await readJson(response))
    .map(mapVehicle)
    .filter((value): value is VehiclePhotoSearchRecord => value !== null);
}

export async function getVehiclePhotoVehicle(vmfCode: number) {
  const response = await requestApi(`api/VehicleLookup/${encodeURIComponent(vmfCode)}`);
  const vehicle = mapVehicle(await readJson(response));
  if (!vehicle) throw new VehiclePhotoApiError("invalid-response", "The FIS API returned an invalid vehicle.");
  return vehicle;
}

export async function getVehiclePhotos(vmfCode: number) {
  const response = await requestApi(`api/vehiclephoto/vehicle/${encodeURIComponent(vmfCode)}`);
  return getCollection(await readJson(response))
    .map(mapPhoto)
    .filter((value): value is VehiclePhotoRecord => value !== null);
}

export async function uploadVehiclePhoto(vmfCode: number, formData: FormData) {
  await requestApi("api/vehiclephoto/upload", { method: "POST", body: formData });
}

export async function createVehiclePhoto(payload: Record<string, unknown>) {
  await requestApi("api/vehiclephoto", { method: "POST", body: JSON.stringify(payload) });
}

export async function updateVehiclePhoto(id: number, payload: Record<string, unknown>) {
  await requestApi(`api/vehiclephoto/${encodeURIComponent(id)}`, { method: "PUT", body: JSON.stringify(payload) });
}

export async function deleteVehiclePhoto(id: number) {
  await requestApi(`api/vehiclephoto/${encodeURIComponent(id)}`, { method: "DELETE" });
}
