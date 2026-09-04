import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;

type JsonRecord = Record<string, unknown>;

export type GarageSearchType = "GG" | "GP";

export type GarageAccidentRow = {
  accidentCode: number;
  vehicleNumber: string | null;
  hireType: string | null;
  accidentDate: string | null;
  reference: string | null;
};

export type GarageAccidentPage = {
  rows: GarageAccidentRow[];
  page: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
  searchTerm: string;
  searchType: GarageSearchType;
};

export type AccidentVehicleOption = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
};

export type CreateAccidentRequest = {
  vmf_code: number;
  description: string;
  driver_name: string | null;
  driver_employ_number: string | null;
  hq_reference: string | null;
  gg_reference: string | null;
  sa_reference: string | null;
  occurence_date: string;
  occurence_time: string | null;
  reported_date: string;
  claim_amount: number;
  excess_amount: number;
};

export type AccidentEditRecord = {
  accidentCode: number;
  vmfCode: number;
  postingMonthCode: number | null;
  description: string | null;
  driverName: string | null;
  driverEmployNumber: string | null;
  hqReference: string | null;
  ggReference: string | null;
  saReference: string | null;
  occurenceDate: string | null;
  occurenceTime: string | null;
  reportedDate: string | null;
  claimAmount: number | null;
  excessAmount: number | null;
  dateCreated: string;
  dateUpdated: string | null;
  createdByUserCode: number | null;
  modifiedByUserCode: number | null;
  isDeleted: boolean;
  vehicleFleetNumber: string | null;
  vehicleRegistrationNumber: string | null;
};

export type AccidentUpdateRequest = {
  accident_code: number;
  vmf_code: number;
  posting_month_code: number | null;
  description: string | null;
  driver_name: string | null;
  driver_employ_number: string | null;
  hq_reference: string | null;
  gg_reference: string | null;
  sa_reference: string | null;
  occurence_date: string | null;
  occurence_time: string | null;
  reported_date: string | null;
  claim_amount: number | null;
  excess_amount: number | null;
  date_created: string;
  date_updated: string | null;
  created_by_user_code: number | null;
  modified_by_user_code: number | null;
  is_deleted: boolean;
};

export type AccidentApiErrorReason = "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class AccidentApiError extends Error {
  constructor(
    public readonly reason: AccidentApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "AccidentApiError";
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

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new AccidentApiError("unauthorized", "No FIS access cookie is available.");
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
      throw new AccidentApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (response.status === 404) {
      throw new AccidentApiError("not-found", "The accident record was not found.");
    }

    if (!response.ok) {
      throw new AccidentApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        `FIS API returned HTTP ${response.status}.`,
      );
    }

    if (response.status === 204) {
      return null;
    }

    try {
      return (await response.json()) as unknown;
    } catch {
      throw new AccidentApiError("invalid-response", "The FIS API returned invalid JSON.");
    }
  } catch (error) {
    if (error instanceof AccidentApiError) {
      throw error;
    }

    throw new AccidentApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

type VehicleLookup = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  typeCode: number | null;
};

function mapVehicle(value: unknown): VehicleLookup | null {
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
    typeCode: asNumber(getValue(value, "type_code", "typeCode")),
  };
}

export async function getAccidentVehicleOptions(searchType: GarageSearchType, searchTerm = "") {
  const normalizedSearchTerm = searchTerm.trim();
  const path = normalizedSearchTerm
    ? `api/vehicles/search?searchTerm=${encodeURIComponent(normalizedSearchTerm)}`
    : "api/vehicles";
  const vehicles = getCollection(await requestApi(path))
    .map(mapVehicle)
    .filter((vehicle): vehicle is VehicleLookup => vehicle !== null);

  const normalizedLowerTerm = normalizedSearchTerm.toLowerCase();
  return vehicles
    .filter((vehicle) => {
      if (!normalizedLowerTerm) {
        return true;
      }

      const value = searchType === "GG" ? vehicle.fleetNumber : vehicle.registrationNumber;
      return (value?.trim().toLowerCase() ?? "") === normalizedLowerTerm;
    })
    .map((vehicle) => ({
      vmfCode: vehicle.vmfCode,
      fleetNumber: vehicle.fleetNumber,
      registrationNumber: vehicle.registrationNumber,
    }) satisfies AccidentVehicleOption)
    .toSorted((left, right) => {
      const leftLabel = left.fleetNumber ?? left.registrationNumber ?? String(left.vmfCode);
      const rightLabel = right.fleetNumber ?? right.registrationNumber ?? String(right.vmfCode);
      return leftLabel.localeCompare(rightLabel);
    });
}

function mapAccidentEditRecord(value: unknown): AccidentEditRecord {
  if (!isRecord(value)) {
    throw new AccidentApiError("invalid-response", "The FIS API returned an invalid accident record.");
  }

  const accidentCode = asNumber(getValue(value, "accident_code", "accidentCode"));
  const vmfCode = asNumber(getValue(value, "vmf_code", "vmfCode"));
  const dateCreated = asString(getValue(value, "date_created", "dateCreated"));
  if (accidentCode === null || vmfCode === null || !dateCreated) {
    throw new AccidentApiError("invalid-response", "The FIS API returned an incomplete accident record.");
  }

  const vehicle = getValue(value, "vehicle", "Vehicle");
  const vehicleRecord = isRecord(vehicle) ? vehicle : null;

  return {
    accidentCode,
    vmfCode,
    postingMonthCode: asNumber(getValue(value, "posting_month_code", "postingMonthCode")),
    description: asString(getValue(value, "description")),
    driverName: asString(getValue(value, "driver_name", "driverName")),
    driverEmployNumber: asString(getValue(value, "driver_employ_number", "driverEmployNumber")),
    hqReference: asString(getValue(value, "hq_reference", "hqReference")),
    ggReference: asString(getValue(value, "gg_reference", "ggReference")),
    saReference: asString(getValue(value, "sa_reference", "saReference")),
    occurenceDate: asString(getValue(value, "occurence_date", "occurrence_date", "occurenceDate")),
    occurenceTime: asString(getValue(value, "occurence_time", "occurenceTime")),
    reportedDate: asString(getValue(value, "reported_date", "reportedDate")),
    claimAmount: asNumber(getValue(value, "claim_amount", "claimAmount")),
    excessAmount: asNumber(getValue(value, "excess_amount", "excessAmount")),
    dateCreated,
    dateUpdated: asString(getValue(value, "date_updated", "dateUpdated")),
    createdByUserCode: asNumber(getValue(value, "created_by_user_code", "createdByUserCode")),
    modifiedByUserCode: asNumber(getValue(value, "modified_by_user_code", "modifiedByUserCode")),
    isDeleted: getValue(value, "is_deleted", "isDeleted") === true,
    vehicleFleetNumber: vehicleRecord
      ? asString(getValue(vehicleRecord, "fleet_number", "fleetNumber"))
      : null,
    vehicleRegistrationNumber: vehicleRecord
      ? asString(getValue(vehicleRecord, "registration_number", "registrationNumber"))
      : null,
  };
}

export async function getAccidentForEdit(accidentCode: number) {
  return mapAccidentEditRecord(await requestApi(`api/accidents/${encodeURIComponent(accidentCode)}`));
}

export async function updateAccidentAgainstApi(request: AccidentUpdateRequest) {
  await requestApi(`api/accidents/${encodeURIComponent(request.accident_code)}`, {
    method: "PUT",
    body: JSON.stringify(request),
  });

  return { ok: true as const };
}

export async function deleteAccidentAgainstApi(accidentCode: number) {
  await requestApi(`api/accidents/${encodeURIComponent(accidentCode)}`, {
    method: "DELETE",
  });

  return { ok: true as const };
}

type TypeLookup = {
  code: number;
  description: string;
};

function mapType(value: unknown): TypeLookup | null {
  if (!isRecord(value)) {
    return null;
  }

  const code = asNumber(getValue(value, "type_code", "typeCode"));
  const description = asString(getValue(value, "type_description", "typeDescription"));
  return code !== null && description ? { code, description } : null;
}

type AccidentLookup = {
  accidentCode: number;
  vmfCode: number | null;
  accidentDate: string | null;
  reference: string | null;
};

function mapAccident(value: unknown): AccidentLookup | null {
  if (!isRecord(value)) {
    return null;
  }

  const accidentCode = asNumber(getValue(value, "accident_code", "accidentCode"));
  if (accidentCode === null) {
    return null;
  }

  return {
    accidentCode,
    vmfCode: asNumber(getValue(value, "vmf_code", "vmfCode")),
    accidentDate: asString(getValue(value, "occurence_date", "occurrence_date", "occurenceDate", "accident_date")),
    reference: asString(getValue(value, "gg_reference", "ggReference")),
  };
}

export async function getGarageAccidentPage(
  page: number,
  searchType: GarageSearchType,
  searchTerm: string,
  pageSize = 12,
): Promise<GarageAccidentPage> {
  const normalizedSearchTerm = searchTerm.trim();
  const vehiclePath = normalizedSearchTerm
    ? `api/vehicles/search?searchTerm=${encodeURIComponent(normalizedSearchTerm)}`
    : "api/vehicles";

  const [accidentPayload, vehiclePayload, typePayload] = await Promise.all([
    requestApi("api/accidents"),
    requestApi(vehiclePath),
    requestApi("api/type"),
  ]);

  const vehicles = getCollection(vehiclePayload)
    .map(mapVehicle)
    .filter((vehicle): vehicle is VehicleLookup => vehicle !== null);
  const vehicleByCode = new Map(vehicles.map((vehicle) => [vehicle.vmfCode, vehicle]));
  const typesByCode = new Map(
    getCollection(typePayload)
      .map(mapType)
      .filter((type): type is TypeLookup => type !== null)
      .map((type) => [type.code, type.description]),
  );

  const matchingVehicleCodes = normalizedSearchTerm
    ? new Set(
        vehicles
          .filter((vehicle) => {
            const value = searchType === "GG" ? vehicle.fleetNumber : vehicle.registrationNumber;
            return (value?.trim().toLocaleLowerCase() ?? "") === normalizedSearchTerm.toLocaleLowerCase();
          })
          .map((vehicle) => vehicle.vmfCode),
      )
    : null;

  const allRows = getCollection(accidentPayload)
    .map(mapAccident)
    .filter((accident): accident is AccidentLookup => accident !== null)
    .filter((accident) => matchingVehicleCodes === null || (accident.vmfCode !== null && matchingVehicleCodes.has(accident.vmfCode)))
    .map((accident) => {
      const vehicle = accident.vmfCode === null ? undefined : vehicleByCode.get(accident.vmfCode);
      const vehicleNumber = searchType === "GG" ? vehicle?.fleetNumber : vehicle?.registrationNumber;

      return {
        accidentCode: accident.accidentCode,
        vehicleNumber: vehicleNumber ?? null,
        hireType: vehicle?.typeCode === null || vehicle?.typeCode === undefined ? null : typesByCode.get(vehicle.typeCode) ?? null,
        accidentDate: accident.accidentDate,
        reference: accident.reference,
      } satisfies GarageAccidentRow;
    })
    .toSorted((left, right) => (right.accidentDate ?? "").localeCompare(left.accidentDate ?? ""));

  const totalRecords = allRows.length;
  const totalPages = Math.max(1, Math.ceil(totalRecords / pageSize));
  const safePage = Math.min(Math.max(page, 1), totalPages);

  return {
    rows: allRows.slice((safePage - 1) * pageSize, safePage * pageSize),
    page: safePage,
    pageSize,
    totalRecords,
    totalPages,
    searchTerm: normalizedSearchTerm,
    searchType,
  };
}

export async function createAccidentAgainstApi(request: CreateAccidentRequest) {
  await requestApi("api/accidents", {
    method: "POST",
    body: JSON.stringify(request),
  });

  return { ok: true as const };
}
