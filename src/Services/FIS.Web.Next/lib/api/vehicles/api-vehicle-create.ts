import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

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

/**
 * The Vehicle Master edit form deliberately has a smaller dependency surface
 * than the inception-capture form.  In particular, it must not require the
 * capture-only maintenance selector (or optional extras/source selectors)
 * merely to load an existing vehicle for editing.
 */
export type VehicleEditReferenceData = Pick<
  VehicleCreateReferenceData,
  "models" | "locations" | "types"
>;

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

export type VehicleCreateApiErrorReason =
  | "unauthorized"
  | "forbidden"
  | "unavailable"
  | "invalid-response";

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

    if (response.status === 401) {
      throw new VehicleCreateApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (response.status === 403) {
      throw new VehicleCreateApiError(
        "forbidden",
        "Your account is not assigned the required Vehicle Master role.",
      );
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
        // Keep the status-based message when the API has no readable body.
      }

      throw new VehicleCreateApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
      );
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
    const name = asString(
      getValue(item, "make_description", "makeDescription", "make_name", "makeName"),
    );
    return code !== null && name ? { code, name } : null;
  });
}

function mapModels(payload: unknown) {
  return mapPresent(getCollection(payload), (item) => {
    if (!isRecord(item)) return null;
    const code = asNumber(getValue(item, "model_code", "modelCode"));
    const makeCode = asNumber(getValue(item, "make_code", "makeCode"));
    const name = asString(
      getValue(item, "model_description", "modelDescription", "model_name", "modelName"),
    );
    const typeCode = asNumber(getValue(item, "type_code", "typeCode"));
    return code !== null && makeCode !== null && name ? { code, makeCode, name, typeCode } : null;
  });
}

function mapLocations(payload: unknown) {
  return mapPresent(getCollection(payload), (item) => {
    if (!isRecord(item)) return null;
    const code = asNumber(
      getValue(item, "locationId", "location_id", "locationCode", "location_code"),
    );
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
  const items = isRecord(payload)
    ? getCollection(getValue(payload, "items"))
    : getCollection(payload);
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
    const code = asNumber(
      getValue(item, "code", "maintenanceTypeId", "maintenance_type_id", "Maintenance_TypeId"),
    );
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
      registrationNumber:
        asString(getValue(item, "registration_number", "registrationNumber")) || null,
      chassisNumber: asString(getValue(item, "chassis_number", "chassisNumber")) || null,
      engineNumber:
        asString(getValue(item, "engine_number_1", "engineNumber1", "engine_number")) || null,
      invoiceNumber: asString(getValue(item, "invoice_number", "invoiceNumber")) || null,
    } satisfies VehicleSearchResult;
  });
}

export async function getVehicleCreateReferenceData(): Promise<VehicleCreateReferenceData> {
  async function optionalLookup<T>(load: () => Promise<T>, fallback: T): Promise<T> {
    try {
      return await load();
    } catch (error) {
      // A rejected access cookie must still take the user through session
      // recovery. Extras and maintenance plans themselves are optional legacy
      // capture sections, however, so a transient failure there must not block
      // the mandatory vehicle identity and allocation workflow.
      if (
        error instanceof VehicleCreateApiError &&
        (error.reason === "unauthorized" || error.reason === "forbidden")
      ) {
        throw error;
      }

      return fallback;
    }
  }

  const [makes, models, locations, types, sources, sites, extras, maintenanceTypes] =
    await Promise.all([
      fetchApi("api/make").then(readJson).then(mapMakes),
      fetchApi("api/model").then(readJson).then(mapModels),
      fetchApi("api/Location").then(readJson).then(mapLocations),
      fetchApi("api/Type").then(readJson).then(mapTypes),
      fetchApi("api/vehicle-source").then(readJson).then(mapSources),
      fetchApi("api/Site").then(readJson).then(mapSites),
      optionalLookup(
        () => fetchApi("api/ExtraCode").then(readJson).then(mapExtras),
        [] as VehicleExtraOption[],
      ),
      optionalLookup(
        () =>
          fetchApi("api/vehicle/authorization/maintenance-types")
            .then(readJson)
            .then(mapMaintenanceTypes),
        [] as VehicleMaintenanceTypeOption[],
      ),
    ]);

  return { makes, models, locations, types, sources, sites, extras, maintenanceTypes };
}

export async function getVehicleEditReferenceData(): Promise<VehicleEditReferenceData> {
  const [models, locations] = await Promise.all([
    fetchApi("api/model").then(readJson).then(mapModels),
    fetchApi("api/Location").then(readJson).then(mapLocations),
  ]);

  let types: VehicleTypeOption[] = [];
  try {
    types = await fetchApi("api/Type").then(readJson).then(mapTypes);
  } catch (error) {
    if (
      error instanceof VehicleCreateApiError &&
      (error.reason === "unauthorized" || error.reason === "forbidden")
    ) {
      throw error;
    }
  }

  return { models, locations, types };
}

export async function searchVehiclesAgainstApi(searchTerm: string) {
  const response = await fetchApi(
    `api/vehicles/search?searchTerm=${encodeURIComponent(searchTerm)}`,
  );
  return mapSearchResults(await readJson(response));
}

export async function createVehicleAgainstApi(request: CreateVehicleRequest) {
  // The inception controller exposes the legacy workflow DTO with normal
  // ASP.NET property names (FleetNumber, ModelCode, ...).  The adapter's
  // snake_case shape is intentionally shared with the rest of the vehicle
  // APIs, so translate it at this boundary instead of relying on an
  // underscore-insensitive JSON binder.
  const apiRequest = {
    fleetNumber: request.fleet_number,
    registrationNumber: request.registration_number,
    chassisNumber: request.chassis_number,
    engineNumber: request.engine_number,
    modelCode: request.model_code,
    colour: request.colour,
    yearManufactured: request.year_manufactured,
    locationCode: request.location_code,
    vehicleStatusCode: request.vehicle_status_code,
    typeCode: request.type_code,
    vsCode: request.vs_code,
    takeOnDate: request.take_on_date,
    takeOnOdo: request.take_on_odo,
    purchaseDate: request.purchase_date,
    purchaseAmount: request.purchase_amount,
    purchaseFrom: request.purchase_from,
    replacedGGNumber: request.replaced_gg_number,
    siteCode: request.site_code,
    invoiceNumber: request.invoice_number,
    gpNumber: request.gp_number,
    comment: request.comment,
    damageStatus: request.damage_status,
    damagesComment: request.damages_comment,
    fleetNotes: request.fleet_notes,
    extraCodes: request.extra_codes,
    maintenanceTypeCode: request.maintenance_type_code,
    maintenanceStartDate: request.maintenance_start_date,
    maintenancePeriodMonths: request.maintenance_period_months,
    maintenanceKilos: request.maintenance_kilos,
    maintenanceValue: request.maintenance_value,
  };

  const response = await fetchApi("api/vehicle/authorization", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(apiRequest),
  });

  await readJson(response);
  return { ok: true as const };
}
