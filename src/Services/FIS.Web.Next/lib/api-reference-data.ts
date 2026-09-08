import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type VehicleTypeRecord = { typeCode: number; description: string };
export type FuelTypeRecord = {
  fuelTypeCode: number;
  description: string;
  ratePerLitre: number | null;
};
export type UnitOfMeasureRecord = {
  unitCode: number;
  description: string;
  abbreviation: string | null;
  category: string | null;
};
export type LicenseTypeRecord = {
  licenceCode: number;
  description: string;
  category: string | null;
};

export type ReferenceDataApiErrorReason = "unauthorized" | "unavailable" | "invalid-response";

export class ReferenceDataApiError extends Error {
  constructor(
    public readonly reason: ReferenceDataApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "ReferenceDataApiError";
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
  for (const key of keys) if (key in record) return record[key];
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

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader)
    throw new ReferenceDataApiError("unauthorized", "No FIS access cookie is available.");

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
      throw new ReferenceDataApiError(
        "unauthorized",
        "The FIS access cookie was rejected.",
        response.status,
      );
    }
    if (!response.ok) {
      let message = `FIS API returned HTTP ${response.status}.`;
      try {
        const payload = await response.clone().json();
        if (isRecord(payload))
          message = asString(getValue(payload, "message", "Message", "error")) ?? message;
      } catch {
        // Keep the status-based message when the response is not JSON.
      }
      throw new ReferenceDataApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
        response.status,
      );
    }
    return response;
  } catch (error) {
    if (error instanceof ReferenceDataApiError) throw error;
    throw new ReferenceDataApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new ReferenceDataApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function readList(value: unknown) {
  if (!Array.isArray(value))
    throw new ReferenceDataApiError(
      "invalid-response",
      "The reference-data response was not a list.",
    );
  return value.filter(isRecord);
}

function mapVehicleType(value: unknown): VehicleTypeRecord | null {
  if (!isRecord(value)) return null;
  const typeCode = asNumber(getValue(value, "type_code", "typeCode"));
  const description = asString(
    getValue(value, "type_description", "typeDescription", "description"),
  );
  return typeCode === null || description === null ? null : { typeCode, description };
}

function mapFuelType(value: unknown): FuelTypeRecord | null {
  if (!isRecord(value)) return null;
  const fuelTypeCode = asNumber(getValue(value, "fuel_type_code", "fuelTypeCode"));
  const description = asString(
    getValue(value, "fuel_description", "fuelDescription", "description"),
  );
  return fuelTypeCode === null || description === null
    ? null
    : {
        fuelTypeCode,
        description,
        ratePerLitre: asNumber(getValue(value, "rate_per_litre", "ratePerLitre")),
      };
}

function mapUnit(value: unknown): UnitOfMeasureRecord | null {
  if (!isRecord(value)) return null;
  const unitCode = asNumber(
    getValue(value, "unit_of_measure_code", "unitOfMeasureCode", "unitCode"),
  );
  const description = asString(
    getValue(value, "unit_description", "unitDescription", "description"),
  );
  return unitCode === null || description === null
    ? null
    : {
        unitCode,
        description,
        abbreviation: asString(
          getValue(value, "unit_abbreviation", "unitAbbreviation", "abbreviation"),
        ),
        category: asString(getValue(value, "unit_category", "unitCategory", "category")),
      };
}

function mapLicense(value: unknown): LicenseTypeRecord | null {
  if (!isRecord(value)) return null;
  const licenceCode = asNumber(
    getValue(value, "licence_code", "license_code", "licenceCode", "licenseCode"),
  );
  const description = asString(
    getValue(
      value,
      "licence_description",
      "license_description",
      "licenceDescription",
      "licenseDescription",
      "description",
    ),
  );
  return licenceCode === null || description === null
    ? null
    : {
        licenceCode,
        description,
        category: asString(
          getValue(
            value,
            "licence_category",
            "license_category",
            "licenceCategory",
            "licenseCategory",
            "category",
          ),
        ),
      };
}

async function sendJson(path: string, method: "POST" | "PUT", body: JsonRecord) {
  return readJson(
    await requestApi(path, {
      method,
      headers: { "content-type": "application/json" },
      body: JSON.stringify(body),
    }),
  );
}

export async function getVehicleTypes() {
  return readList(await readJson(await requestApi("api/type")))
    .map(mapVehicleType)
    .filter((value): value is VehicleTypeRecord => value !== null);
}

export async function createVehicleType(description: string) {
  return mapVehicleType(await sendJson("api/type", "POST", { type_description: description }));
}

export async function updateVehicleType(typeCode: number, description: string) {
  return mapVehicleType(
    await sendJson(`api/type/${encodeURIComponent(typeCode)}`, "PUT", {
      type_code: typeCode,
      type_description: description,
    }),
  );
}

export async function deleteVehicleType(typeCode: number) {
  await requestApi(`api/type/${encodeURIComponent(typeCode)}`, { method: "DELETE" });
}

export async function getFuelTypes() {
  return readList(await readJson(await requestApi("api/fueltype")))
    .map(mapFuelType)
    .filter((value): value is FuelTypeRecord => value !== null);
}

export async function createFuelType(description: string, ratePerLitre: number | null) {
  return mapFuelType(
    await sendJson("api/fueltype", "POST", {
      fuel_description: description,
      rate_per_litre: ratePerLitre,
    }),
  );
}

export async function updateFuelType(
  fuelTypeCode: number,
  description: string,
  ratePerLitre: number | null,
) {
  return mapFuelType(
    await sendJson(`api/fueltype/${encodeURIComponent(fuelTypeCode)}`, "PUT", {
      fuel_description: description,
      rate_per_litre: ratePerLitre,
    }),
  );
}

export async function deleteFuelType(fuelTypeCode: number) {
  await requestApi(`api/fueltype/${encodeURIComponent(fuelTypeCode)}`, { method: "DELETE" });
}

export async function getUnitsOfMeasure() {
  return readList(await readJson(await requestApi("api/UnitOfMeasure")))
    .map(mapUnit)
    .filter((value): value is UnitOfMeasureRecord => value !== null);
}

export async function createUnitOfMeasure(
  description: string,
  abbreviation: string | null,
  category: string | null,
) {
  return mapUnit(
    await sendJson("api/UnitOfMeasure", "POST", {
      unit_description: description,
      unit_abbreviation: abbreviation,
      unit_category: category,
    }),
  );
}

export async function updateUnitOfMeasure(
  unitCode: number,
  description: string,
  abbreviation: string | null,
  category: string | null,
) {
  return mapUnit(
    await sendJson(`api/UnitOfMeasure/${encodeURIComponent(unitCode)}`, "PUT", {
      unit_of_measure_code: unitCode,
      unit_description: description,
      unit_abbreviation: abbreviation,
      unit_category: category,
    }),
  );
}

export async function deleteUnitOfMeasure(unitCode: number) {
  await requestApi(`api/UnitOfMeasure/${encodeURIComponent(unitCode)}`, { method: "DELETE" });
}

export async function getLicenseTypes() {
  return readList(await readJson(await requestApi("api/License")))
    .map(mapLicense)
    .filter((value): value is LicenseTypeRecord => value !== null);
}

export async function createLicenseType(description: string, category: string | null) {
  return mapLicense(
    await sendJson("api/License", "POST", {
      licence_description: description,
      licence_category: category,
    }),
  );
}

export async function updateLicenseType(
  licenceCode: number,
  description: string,
  category: string | null,
) {
  return mapLicense(
    await sendJson(`api/License/${encodeURIComponent(licenceCode)}`, "PUT", {
      licence_code: licenceCode,
      licence_description: description,
      licence_category: category,
    }),
  );
}

export async function deleteLicenseType(licenceCode: number) {
  await requestApi(`api/License/${encodeURIComponent(licenceCode)}`, { method: "DELETE" });
}
