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

export type VehicleTypeOption = {
  code: number;
  name: string;
};

export type VehicleSourceOption = {
  code: number;
  name: string;
};

export type VehicleSiteOption = {
  code: number;
  name: string;
};

export type VehicleExtraOption = {
  code: number;
  name: string;
};

export type VehicleMaintenanceTypeOption = {
  code: number;
  name: string;
};

export type VehicleCreateReferenceData = {
  makes: VehicleMakeOption[];
  models: VehicleModelOption[];
  locations: VehicleLocationOption[];
  types: VehicleTypeOption[];
  sources: VehicleSourceOption[];
  sites: VehicleSiteOption[];
  extras: VehicleExtraOption[];
  maintenanceTypes: VehicleMaintenanceTypeOption[];
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
  fleet_number: string | null;
  registration_number: string | null;
  chassis_number: string;
  engine_number: string;
  model_code: number;
  colour: string;
  year_manufactured: number;
  location_code: number;
  vehicle_status_code: number;
  type_code: number;
  vs_code: number;
  take_on_date: string;
  take_on_odo: number;
  purchase_date: string;
  purchase_amount: number;
  purchase_from: string;
  replaced_gg_number: string | null;
  site_code: number;
  invoice_number: string | null;
  gp_number: string | null;
  comment: string;
  damage_status: "Y" | "N";
  damages_comment: string | null;
  fleet_notes: string | null;
  extra_codes: number[];
  maintenance_type_code: number | null;
  maintenance_start_date: string | null;
  maintenance_period_months: number | null;
  maintenance_kilos: number | null;
  maintenance_value: number | null;
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
  return mapPresent(getCollection(payload), (item) => {
    if (!isRecord(item)) return null;
      const code = asNumber(getValue(item, "make_code", "makeCode"));
      const name = asString(getValue(item, "make_description", "makeDescription", "make_name", "makeName"));
      return code !== null && name ? { code, name } : null;
  });
}

function mapModels(payload: unknown) {
  return mapPresent(getCollection(payload), (item) => {
    if (!isRecord(item)) return null;
      const code = asNumber(getValue(item, "model_code", "modelCode"));
      const makeCode = asNumber(getValue(item, "make_code", "makeCode"));
      const name = asString(getValue(item, "model_description", "modelDescription", "model_name", "modelName"));
      const typeCode = asNumber(getValue(item, "type_code", "typeCode"));
      return code !== null && makeCode !== null && name
        ? { code, makeCode, name, typeCode }
        : null;
  });
}

function mapLocations(payload: unknown) {
  return mapPresent(getCollection(payload), (item) => {
    if (!isRecord(item)) return null;
      const code = asNumber(getValue(item, "locationId", "location_id", "locationCode", "location_code"));
      const name = asString(getValue(item, "locationName", "location_name", "description"));
      return code !== null && name ? { code, name } : null;
  });
}

function mapTypes(payload: unknown) {
  return mapPresent(getCollection(payload), (item) => {
    if (!isRecord(item)) return null;
    const code = asNumber(getValue(item, "type_code", "typeCode"));
    const name = asString(getValue(item, "type_description", "typeDescription", "name"));
    return code !== null && name ? { code, name } : null;
  });
}

function mapSources(payload: unknown) {
  const items = isRecord(payload) ? getCollection(getValue(payload, "items")) : getCollection(payload);
  return mapPresent(items, (item) => {
    if (!isRecord(item)) return null;
    const code = asNumber(getValue(item, "vsCode", "vs_code", "sourceCode"));
    const name = asString(getValue(item, "name", "description"));
    return code !== null && name ? { code, name } : null;
  });
}

function mapSites(payload: unknown) {
  return mapPresent(getCollection(payload), (item) => {
    if (!isRecord(item)) return null;
    const code = asNumber(getValue(item, "siteCode", "site_code"));
    const name = asString(getValue(item, "description", "siteDescription", "name"));
    return code !== null && name ? { code, name } : null;
  });
}

function mapExtras(payload: unknown) {
  return mapPresent(getCollection(payload), (item) => {
    if (!isRecord(item)) return null;
    const code = asNumber(getValue(item, "extra_code", "extraCode"));
    const name = asString(getValue(item, "extra_description", "description", "extraDescription"));
    return code !== null && name ? { code, name } : null;
  });
}

function mapMaintenanceTypes(payload: unknown) {
  return mapPresent(getCollection(payload), (item) => {
    if (!isRecord(item)) return null;
    const code = asNumber(getValue(item, "code", "maintenanceTypeId", "maintenance_type_id", "Maintenance_TypeId"));
    const name = asString(getValue(item, "name", "description", "Name"));
    return code !== null && name ? { code, name } : null;
  });
}

function mapSearchResults(payload: unknown) {
  return mapPresent(getCollection(payload), (item) => {
    if (!isRecord(item)) return null;
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
  });
}

export async function getVehicleCreateReferenceData(): Promise<VehicleCreateReferenceData> {
  const [makes, models, locations, types, sources, sites, extras, maintenanceTypes] = await Promise.all([
    fetchApi("api/make").then(readJson).then(mapMakes),
    fetchApi("api/model").then(readJson).then(mapModels),
    fetchApi("api/Location").then(readJson).then(mapLocations),
    fetchApi("api/Type").then(readJson).then(mapTypes),
    fetchApi("api/vehicle-source").then(readJson).then(mapSources),
    fetchApi("api/Site").then(readJson).then(mapSites),
    fetchApi("api/ExtraCode").then(readJson).then(mapExtras),
    fetchApi("api/vehicle/authorization/maintenance-types").then(readJson).then(mapMaintenanceTypes),
  ]);

  return { makes, models, locations, types, sources, sites, extras, maintenanceTypes };
}

export async function searchVehiclesAgainstApi(searchTerm: string) {
  const response = await fetchApi(`api/vehicles/search?searchTerm=${encodeURIComponent(searchTerm)}`);
  return mapSearchResults(await readJson(response));
}

export async function createVehicleAgainstApi(request: CreateVehicleRequest) {
  const response = await fetchApi("api/vehicle/authorization", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(request),
  });

  await readJson(response);
  return { ok: true as const };
}
