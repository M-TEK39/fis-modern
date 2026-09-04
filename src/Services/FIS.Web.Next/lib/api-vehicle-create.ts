import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;

type JsonRecord = Record<string, unknown>;

export type VehicleMakeOption = {
  code: number;
  name: string;
};

export type VehicleModelOption = {
  code: number;
  makeCode: number;
  name: string;
  typeCode: number | null;
};

export type VehicleLocationOption = {
  code: number;
  name: string;
};

export type VehicleCreateReferenceData = {
  makes: VehicleMakeOption[];
  models: VehicleModelOption[];
  locations: VehicleLocationOption[];
};

export type VehicleSearchResult = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  chassisNumber: string | null;
  engineNumber: string | null;
  invoiceNumber: string | null;
};

export type CreateVehicleRequest = {
  model_code: number;
  type_code: number;
  vehicle_status_code: number;
  location_code: number;
  fleet_number: string;
  registration_number: string;
  engine_number_1: string;
  chassis_number: string;
  take_on_date: string;
  take_on_odo: number;
  current_odo: number;
  tare: number;
  gvm: number | null;
  year_manufactured: number;
  colour: string;
  purchase_date: string;
  purchase_amount: number;
  ifms_vehicle_register_number: string | null;
  natis_model_number: string | null;
  recalculate_tariff: boolean;
};

export type VehicleCreateApiErrorReason = "unauthorized" | "unavailable" | "invalid-response";

export class VehicleCreateApiError extends Error {
  constructor(
    public readonly reason: VehicleCreateApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "VehicleCreateApiError";
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

function asString(value: unknown) {
  return typeof value === "string" ? value.trim() : "";
}

function getCollection(payload: unknown) {
  if (Array.isArray(payload)) {
    return payload;
  }

  if (isRecord(payload)) {
    const data = getValue(payload, "data", "items", "results");
    return Array.isArray(data) ? data : [];
  }

  return [];
}

async function fetchApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new VehicleCreateApiError("unauthorized", "No FIS access cookie is available.");
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
        ...init.headers,
      },
      signal: controller.signal,
    });

    if (response.status === 401 || response.status === 403) {
      throw new VehicleCreateApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (!response.ok) {
      throw new VehicleCreateApiError("unavailable", `FIS API returned HTTP ${response.status}.`);
    }

    return response;
  } catch (error) {
    if (error instanceof VehicleCreateApiError) {
      throw error;
    }

    throw new VehicleCreateApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new VehicleCreateApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapMakes(payload: unknown) {
  return getCollection(payload)
    .filter(isRecord)
    .map((item) => {
      const code = asNumber(getValue(item, "make_code", "makeCode"));
      const name = asString(getValue(item, "make_description", "makeDescription", "make_name", "makeName"));
      return code !== null && name ? { code, name } : null;
    })
    .filter((item): item is VehicleMakeOption => item !== null);
}

function mapModels(payload: unknown) {
  return getCollection(payload)
    .filter(isRecord)
    .map((item) => {
      const code = asNumber(getValue(item, "model_code", "modelCode"));
      const makeCode = asNumber(getValue(item, "make_code", "makeCode"));
      const name = asString(getValue(item, "model_description", "modelDescription", "model_name", "modelName"));
      const typeCode = asNumber(getValue(item, "type_code", "typeCode"));
      return code !== null && makeCode !== null && name
        ? { code, makeCode, name, typeCode }
        : null;
    })
    .filter((item): item is VehicleModelOption => item !== null);
}

function mapLocations(payload: unknown) {
  return getCollection(payload)
    .filter(isRecord)
    .map((item) => {
      const code = asNumber(getValue(item, "locationId", "location_id", "locationCode", "location_code"));
      const name = asString(getValue(item, "locationName", "location_name", "description"));
      return code !== null && name ? { code, name } : null;
    })
    .filter((item): item is VehicleLocationOption => item !== null);
}

function mapSearchResults(payload: unknown) {
  return getCollection(payload)
    .filter(isRecord)
    .map((item) => {
      const vmfCode = asNumber(getValue(item, "vmf_code", "vmfCode"));
      if (vmfCode === null) {
        return null;
      }

      return {
        vmfCode,
        fleetNumber: asString(getValue(item, "fleet_number", "fleetNumber")) || null,
        registrationNumber: asString(getValue(item, "registration_number", "registrationNumber")) || null,
        chassisNumber: asString(getValue(item, "chassis_number", "chassisNumber")) || null,
        engineNumber: asString(getValue(item, "engine_number_1", "engineNumber1", "engine_number")) || null,
        invoiceNumber: asString(getValue(item, "invoice_number", "invoiceNumber")) || null,
      } satisfies VehicleSearchResult;
    })
    .filter((item): item is VehicleSearchResult => item !== null);
}

export async function getVehicleCreateReferenceData(): Promise<VehicleCreateReferenceData> {
  const [makes, models, locations] = await Promise.all([
    fetchApi("api/make").then(readJson).then(mapMakes),
    fetchApi("api/model").then(readJson).then(mapModels),
    fetchApi("api/Location").then(readJson).then(mapLocations),
  ]);

  return { makes, models, locations };
}

export async function searchVehiclesAgainstApi(searchTerm: string) {
  const response = await fetchApi(`api/vehicles/search?searchTerm=${encodeURIComponent(searchTerm)}`);
  return mapSearchResults(await readJson(response));
}

export async function createVehicleAgainstApi(request: CreateVehicleRequest) {
  const response = await fetchApi("api/vehicles", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(request),
  });

  await readJson(response);
  return { ok: true as const };
}
