import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type ModelRecord = {
  modelCode: number;
  modelDescription: string;
  makeCode: number;
  makeDescription: string | null;
  unitOfMeasureCode: number;
  fuelTypeCode: number;
  licenceCode: number;
  maintenanceTriggerCode: number | null;
  classCode: number;
  typeCode: number | null;
  engineType: string | null;
  engineCapacity: number | null;
  ratedPower: number | null;
  fuelTankCapacity: number | null;
  targetConsumption: number | null;
  targetTyreLife: number | null;
  serviceInterval: number | null;
  vemmCode: string | null;
  licenceFeeCode: number | null;
  gvm: number | null;
  transmission: string | null;
  wesbankKilosPerLitre: number | null;
  dateCreated: string | null;
  dateUpdated: string | null;
  createdByUserCode: number | null;
  modifiedByUserCode: number | null;
  isDeleted: boolean;
};

export type ModelOption = { code: number; description: string };

export type ModelReferenceData = {
  makes: ModelOption[];
  types: ModelOption[];
  fuelTypes: ModelOption[];
  classes: ModelOption[];
  units: ModelOption[];
  licenceFees: ModelOption[];
  driverLicences: ModelOption[];
  maintenanceTriggers: ModelOption[];
};

export type ModelDeleteCheck = {
  vehicleCount: number;
  canDelete: boolean;
};

export type ModelWriteInput = Omit<ModelRecord, "modelCode" | "makeDescription" | "dateCreated" | "dateUpdated" | "createdByUserCode" | "modifiedByUserCode" | "isDeleted">;

export type ModelApiErrorReason = "unauthorized" | "unavailable" | "invalid-response";

export class ModelApiError extends Error {
  constructor(
    public readonly reason: ModelApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "ModelApiError";
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

function getCollection(payload: unknown) {
  if (Array.isArray(payload)) return payload;
  if (isRecord(payload)) {
    const collection = getValue(payload, "data", "items", "results");
    return Array.isArray(collection) ? collection : [];
  }
  return [];
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) throw new ModelApiError("unauthorized", "No FIS access cookie is available.");

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
      throw new ModelApiError("unauthorized", "The FIS access cookie was rejected.", response.status);
    }
    if (!response.ok) {
      let message = `FIS API returned HTTP ${response.status}.`;
      try {
        const payload = await response.clone().json();
        if (isRecord(payload)) message = asString(getValue(payload, "message", "Message", "error")) ?? message;
      } catch {
        // Keep the status-based message when the API body is not JSON.
      }
      throw new ModelApiError(response.status >= 500 ? "unavailable" : "invalid-response", message, response.status);
    }
    return response;
  } catch (error) {
    if (error instanceof ModelApiError) throw error;
    throw new ModelApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new ModelApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapModel(value: unknown): ModelRecord | null {
  if (!isRecord(value)) return null;
  const modelCode = asNumber(getValue(value, "model_code", "modelCode"));
  const modelDescription = asString(getValue(value, "model_description", "modelDescription", "model_name", "modelName"));
  const makeCode = asNumber(getValue(value, "make_code", "makeCode"));
  if (modelCode === null || modelDescription === null || makeCode === null) return null;

  return {
    modelCode,
    modelDescription,
    makeCode,
    makeDescription: asString(getValue(value, "make_description", "makeDescription", "make_name", "makeName")),
    unitOfMeasureCode: asNumber(getValue(value, "unit_of_measure_code", "unitOfMeasureCode")) ?? 0,
    fuelTypeCode: asNumber(getValue(value, "fuel_type_code", "fuelTypeCode")) ?? 0,
    licenceCode: asNumber(getValue(value, "licence_code", "licenceCode")) ?? 0,
    maintenanceTriggerCode: asNumber(getValue(value, "maint_trigger_code", "maintenanceTriggerCode", "maintTriggerCode")),
    classCode: asNumber(getValue(value, "class_code", "classCode")) ?? 0,
    typeCode: asNumber(getValue(value, "type_code", "typeCode")),
    engineType: asString(getValue(value, "engine_type", "engineType")),
    engineCapacity: asNumber(getValue(value, "engine_capacity", "engineCapacity")),
    ratedPower: asNumber(getValue(value, "rated_power", "ratedPower")),
    fuelTankCapacity: asNumber(getValue(value, "fuel_tank_capacity", "fuelTankCapacity")),
    targetConsumption: asNumber(getValue(value, "target_consumption", "targetConsumption")),
    targetTyreLife: asNumber(getValue(value, "target_tyre_life", "targetTyreLife")),
    serviceInterval: asNumber(getValue(value, "service_interval", "serviceInterval")),
    vemmCode: asString(getValue(value, "vemm_code", "vemmCode")),
    licenceFeeCode: asNumber(getValue(value, "licence_fee_code", "licenceFeeCode")),
    gvm: asNumber(getValue(value, "gvm")),
    transmission: asString(getValue(value, "transmission")),
    wesbankKilosPerLitre: asNumber(getValue(value, "wesbank_kilos_per_litre", "wesbankKilosPerLitre")),
    dateCreated: asString(getValue(value, "date_created", "dateCreated")),
    dateUpdated: asString(getValue(value, "date_updated", "dateUpdated")),
    createdByUserCode: asNumber(getValue(value, "created_by_user_code", "createdByUserCode")),
    modifiedByUserCode: asNumber(getValue(value, "modified_by_user_code", "modifiedByUserCode")),
    isDeleted: asBoolean(getValue(value, "is_deleted", "isDeleted")),
  };
}

function mapOption(value: unknown, codeKeys: string[], descriptionKeys: string[]): ModelOption | null {
  if (!isRecord(value)) return null;
  const code = asNumber(getValue(value, ...codeKeys));
  const description = asString(getValue(value, ...descriptionKeys));
  return code !== null && description !== null ? { code, description } : null;
}

async function getOptions(path: string, codeKeys: string[], descriptionKeys: string[]) {
  const payload = await readJson(await requestApi(path));
  return getCollection(payload)
    .map((item) => mapOption(item, codeKeys, descriptionKeys))
    .filter((item): item is ModelOption => item !== null);
}

async function getOptionalOptions(path: string, codeKeys: string[], descriptionKeys: string[]) {
  try {
    return await getOptions(path, codeKeys, descriptionKeys);
  } catch (error) {
    if (error instanceof ModelApiError && error.reason === "unauthorized") throw error;
    return [];
  }
}

export async function getModels() {
  const payload = await readJson(await requestApi("api/model"));
  if (!Array.isArray(payload)) throw new ModelApiError("invalid-response", "The model response was not a list.");
  return payload.map(mapModel).filter((model): model is ModelRecord => model !== null);
}

export async function getModel(modelCode: number) {
  return mapModel(await readJson(await requestApi(`api/model/${encodeURIComponent(modelCode)}`)));
}

export async function getModelDeleteCheck(modelCode: number): Promise<ModelDeleteCheck> {
  const payload = await readJson(await requestApi(`api/model/${encodeURIComponent(modelCode)}/delete-check`));
  if (!isRecord(payload)) throw new ModelApiError("invalid-response", "The model dependency response was invalid.");
  return {
    vehicleCount: asNumber(getValue(payload, "vehicleCount", "VehicleCount")) ?? 0,
    canDelete: asBoolean(getValue(payload, "canDelete", "CanDelete")),
  };
}

export async function getModelReferenceData(): Promise<ModelReferenceData> {
  const [makes, types, fuelTypes, classes, units, licenceFees, driverLicences, maintenanceTriggers] = await Promise.all([
    getOptions("api/make", ["make_code", "makeCode"], ["make_description", "makeDescription"]),
    getOptionalOptions("api/type", ["type_code", "typeCode"], ["type_description", "typeDescription"]),
    getOptions("api/fueltype", ["fuel_type_code", "fuelTypeCode"], ["fuel_description", "fuelDescription"]),
    getOptions("api/class", ["class_code", "classCode"], ["description", "Description", "class_description", "classDescription"]),
    getOptions("api/UnitOfMeasure", ["unit_of_measure_code", "unitOfMeasureCode"], ["unit_description", "unitDescription"]),
    getOptions("api/licensefee", ["licence_fee_code", "LicenceFeeCode", "licenceFeeCode"], ["licence_description", "Description", "description"]),
    getOptions("api/DriverLicence", ["licence_code", "LicenceCode", "licenceCode"], ["description", "Description"]),
    getOptionalOptions("api/MaintenanceTrigger", ["maint_trigger_code", "Id", "maintTriggerCode"], ["description", "Description"]),
  ]);
  return { makes, types, fuelTypes, classes, units, licenceFees, driverLicences, maintenanceTriggers };
}

function toRequest(input: ModelWriteInput) {
  return {
    make_code: input.makeCode,
    unit_of_measure_code: input.unitOfMeasureCode,
    fuel_type_code: input.fuelTypeCode,
    licence_code: input.licenceCode,
    maint_trigger_code: input.maintenanceTriggerCode,
    class_code: input.classCode,
    type_code: input.typeCode,
    model_description: input.modelDescription,
    engine_type: input.engineType,
    engine_capacity: input.engineCapacity,
    rated_power: input.ratedPower,
    fuel_tank_capacity: input.fuelTankCapacity,
    target_consumption: input.targetConsumption,
    target_tyre_life: input.targetTyreLife,
    service_interval: input.serviceInterval,
    vemm_code: input.vemmCode,
    licence_fee_code: input.licenceFeeCode,
    gvm: input.gvm,
    transmission: input.transmission,
    wesbank_kilos_per_litre: input.wesbankKilosPerLitre,
  };
}

export async function createModel(input: ModelWriteInput) {
  return mapModel(await readJson(await requestApi("api/model", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(toRequest(input)),
  })));
}

export async function updateModel(modelCode: number, input: ModelWriteInput) {
  return mapModel(await readJson(await requestApi(`api/model/${encodeURIComponent(modelCode)}`, {
    method: "PUT",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ model_code: modelCode, ...toRequest(input) }),
  })));
}

export async function deleteModel(modelCode: number) {
  await requestApi(`api/model/${encodeURIComponent(modelCode)}`, { method: "DELETE" });
}
