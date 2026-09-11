import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type TowingSearchType = "GG" | "GP";

export type TowingRecord = {
  towingCode: number;
  vmfCode: number;
  callReference: number | null;
  requestDate: string | null;
  requestTime: string | null;
  locationStart: string | null;
  vehicleProblem: string | null;
  keys: string | null;
  siteCode: number | null;
  contactPersonName: string | null;
  contactPersonTel: string | null;
  contactPersonCell: string | null;
  personAtVehicleName: string | null;
  personAtVehicleCell: string | null;
  remarks: string | null;
  towTruckCode: number | null;
};

export type TowingPage = {
  items: TowingRecord[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
};

export type TowTruckRecord = {
  towCode: number;
  area: string | null;
  name: string | null;
  telephone: string | null;
  fax: string | null;
};

export type TowTruckPage = {
  items: TowTruckRecord[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
};

export type TowingSite = {
  siteCode: number;
  departmentNumber: string | null;
  description: string | null;
};

export type TowingRequest = {
  vmf_code: number;
  Call_refer: number | null;
  Tow_request_date: string | null;
  Tow_request_time: string | null;
  Tow_location_start: string | null;
  Vehicle_problem: string | null;
  Keys: string | null;
  Site_code: number | null;
  Contact_person_name: string | null;
  Contact_person_tel: string | null;
  Contact_person_cell: string | null;
  Person_at_vehicle_name: string | null;
  Person_at_vehicle_cell: string | null;
  Remaks: string | null;
  Tow_Truck_code: number | null;
};

export type TowTruckRequest = {
  TowArea: string | null;
  TowName: string | null;
  TowTel: string | null;
  TowFax: string | null;
};

export type TowingApiErrorReason =
  "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class TowingApiError extends Error {
  constructor(
    public readonly reason: TowingApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "TowingApiError";
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

function getCollection(value: unknown) {
  if (Array.isArray(value)) return value;
  if (isRecord(value)) {
    const collection = getValue(value, "data", "items", "results");
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
    throw new TowingApiError("unauthorized", "No FIS access cookie is available.");
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
      throw new TowingApiError("unauthorized", "The FIS access cookie was rejected.");
    }
    if (response.status === 404) {
      throw new TowingApiError("not-found", "The requested towing record was not found.");
    }
    if (!response.ok) {
      throw new TowingApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        `FIS API returned HTTP ${response.status}.`,
      );
    }

    return response;
  } catch (error) {
    if (error instanceof TowingApiError) throw error;
    throw new TowingApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new TowingApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapTowing(value: unknown): TowingRecord | null {
  if (!isRecord(value)) return null;
  const towingCode = asNumber(getValue(value, "Towing_code", "towingCode", "towing_code"));
  const vmfCode = asNumber(getValue(value, "vmf_code", "vmfCode"));
  if (towingCode === null || vmfCode === null) return null;

  return {
    towingCode,
    vmfCode,
    callReference: asNumber(getValue(value, "Call_refer", "callReference", "call_refer")),
    requestDate: asString(getValue(value, "Tow_request_date", "requestDate", "tow_request_date")),
    requestTime: asString(getValue(value, "Tow_request_time", "requestTime", "tow_request_time")),
    locationStart: asString(
      getValue(value, "Tow_location_start", "locationStart", "tow_location_start"),
    ),
    vehicleProblem: asString(
      getValue(value, "Vehicle_problem", "vehicleProblem", "vehicle_problem"),
    ),
    keys: asString(getValue(value, "Keys", "keys")),
    siteCode: asNumber(getValue(value, "Site_code", "siteCode", "site_code")),
    contactPersonName: asString(
      getValue(value, "Contact_person_name", "contactPersonName", "contact_person_name"),
    ),
    contactPersonTel: asString(
      getValue(value, "Contact_person_tel", "contactPersonTel", "contact_person_tel"),
    ),
    contactPersonCell: asString(
      getValue(value, "Contact_person_cell", "contactPersonCell", "contact_person_cell"),
    ),
    personAtVehicleName: asString(
      getValue(value, "Person_at_vehicle_name", "personAtVehicleName", "person_at_vehicle_name"),
    ),
    personAtVehicleCell: asString(
      getValue(value, "Person_at_vehicle_cell", "personAtVehicleCell", "person_at_vehicle_cell"),
    ),
    remarks: asString(getValue(value, "Remaks", "remarks", "remaks")),
    towTruckCode: asNumber(getValue(value, "Tow_Truck_code", "towTruckCode", "tow_truck_code")),
  };
}

function mapTowTruck(value: unknown): TowTruckRecord | null {
  if (!isRecord(value)) return null;
  const towCode = asNumber(getValue(value, "TowCode", "towCode", "Tow_code"));
  if (towCode === null) return null;
  return {
    towCode,
    area: asString(getValue(value, "TowArea", "towArea", "Tow_area")),
    name: asString(getValue(value, "TowName", "towName", "Tow_name")),
    telephone: asString(getValue(value, "TowTel", "towTel", "Tow_tel")),
    fax: asString(getValue(value, "TowFax", "towFax", "Tow_fax")),
  };
}

function mapSite(value: unknown): TowingSite | null {
  if (!isRecord(value)) return null;
  const siteCode = asNumber(getValue(value, "SiteCode", "siteCode", "site_code"));
  if (siteCode === null) return null;
  return {
    siteCode,
    departmentNumber: asString(
      getValue(value, "DepartmentNumber", "Department_number", "department_number"),
    ),
    description: asString(getValue(value, "Description", "description", "site_description")),
  };
}

function mapTowingCollection(value: unknown) {
  return mapPresent(getCollection(value), mapTowing);
}

export async function getTowings() {
  return mapTowingCollection(await readJson(await requestApi("api/towing")));
}

export async function getTowingPage(
  options: { vmfCodes?: readonly number[]; page?: number; pageSize?: number } = {},
): Promise<TowingPage> {
  const page = Math.max(1, Math.trunc(options.page ?? 1));
  const pageSize = Math.min(100, Math.max(1, Math.trunc(options.pageSize ?? 24)));
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  for (const vmfCode of options.vmfCodes ?? []) {
    if (Number.isSafeInteger(vmfCode) && vmfCode > 0) {
      params.append("vmfCode", String(vmfCode));
    }
  }

  const payload = await readJson(await requestApi(`api/towing/page?${params.toString()}`));
  if (!isRecord(payload)) {
    throw new TowingApiError("invalid-response", "The FIS API returned an invalid towing page.");
  }

  const items = getCollection(payload)
    .map(mapTowing)
    .filter((item): item is TowingRecord => item !== null);
  const resolvedPage = pageNumber(getValue(payload, "page", "Page"), page);
  const resolvedPageSize = pageNumber(getValue(payload, "pageSize", "PageSize"), pageSize);
  const total = Math.max(0, asNumber(getValue(payload, "total", "Total")) ?? items.length);
  return {
    items,
    page: resolvedPage,
    pageSize: resolvedPageSize,
    total,
    totalPages: pageNumber(
      getValue(payload, "totalPages", "TotalPages"),
      Math.max(1, Math.ceil(total / resolvedPageSize)),
    ),
  };
}

export async function getTowing(towingCode: number) {
  const record = mapTowing(
    await readJson(await requestApi(`api/towing/${encodeURIComponent(towingCode)}`)),
  );
  if (!record)
    throw new TowingApiError("invalid-response", "The FIS API returned an invalid towing record.");
  return record;
}

export async function searchTowingVehicles(searchType: TowingSearchType, searchTerm: string) {
  const response = await requestApi(`api/VehicleLookup?keyword=${encodeURIComponent(searchTerm)}`);
  const values = getCollection(await readJson(response));
  const vehicles = mapPresent(values, (value) => {
    if (!isRecord(value)) return null;
    const vmfCode = asNumber(getValue(value, "VmfCode", "vmfCode", "vmf_code"));
    if (vmfCode === null) return null;
    return {
      vmfCode,
      fleetNumber: asString(getValue(value, "FleetNumber", "fleetNumber", "fleet_number")),
      registrationNumber: asString(
        getValue(value, "RegistrationNumber", "registrationNumber", "registration_number"),
      ),
    };
  });
  return vehicles.filter((vehicle) => {
    const value = searchType === "GG" ? vehicle.fleetNumber : vehicle.registrationNumber;
    return value?.toLocaleLowerCase().includes(searchTerm.trim().toLocaleLowerCase()) === true;
  });
}

export async function getTowingSites() {
  return getCollection(await readJson(await requestApi("api/site")))
    .map(mapSite)
    .filter((site): site is TowingSite => site !== null)
    .sort((left, right) => (left.description ?? "").localeCompare(right.description ?? ""));
}

export async function getTowTrucks() {
  return getCollection(await readJson(await requestApi("api/towing/tow-trucks")))
    .map(mapTowTruck)
    .filter((truck): truck is TowTruckRecord => truck !== null)
    .sort((left, right) => (left.name ?? "").localeCompare(right.name ?? ""));
}

function pageNumber(value: unknown, fallback: number) {
  const parsed = asNumber(value);
  return parsed !== null && Number.isSafeInteger(parsed) && parsed > 0 ? parsed : fallback;
}

export async function getTowTruckPage(
  search: string,
  page = 1,
  pageSize = 24,
): Promise<TowTruckPage> {
  const params = new URLSearchParams({ search, page: String(page), pageSize: String(pageSize) });
  const payload = await readJson(await requestApi(`api/towing/tow-trucks/page?${params}`));
  if (!isRecord(payload))
    throw new TowingApiError("invalid-response", "The FIS API returned an invalid tow truck page.");

  const items = getCollection(payload)
    .map(mapTowTruck)
    .filter((truck): truck is TowTruckRecord => truck !== null);
  const resolvedPage = pageNumber(getValue(payload, "page", "Page"), 1);
  const resolvedPageSize = pageNumber(getValue(payload, "pageSize", "PageSize"), pageSize);
  const total = Math.max(0, asNumber(getValue(payload, "total", "Total")) ?? items.length);
  return {
    items,
    page: resolvedPage,
    pageSize: resolvedPageSize,
    total,
    totalPages: pageNumber(
      getValue(payload, "totalPages", "TotalPages"),
      Math.max(1, Math.ceil(total / resolvedPageSize)),
    ),
  };
}

export async function createTowingAgainstApi(request: TowingRequest) {
  return mapTowing(
    await readJson(
      await requestApi("api/towing", { method: "POST", body: JSON.stringify(request) }),
    ),
  );
}

export async function updateTowingAgainstApi(towingCode: number, request: TowingRequest) {
  return mapTowing(
    await readJson(
      await requestApi(`api/towing/${encodeURIComponent(towingCode)}`, {
        method: "PUT",
        body: JSON.stringify({ Towing_code: towingCode, ...request }),
      }),
    ),
  );
}

export async function deleteTowingAgainstApi(towingCode: number) {
  await requestApi(`api/towing/${encodeURIComponent(towingCode)}`, { method: "DELETE" });
}

export async function createTowTruckAgainstApi(request: TowTruckRequest) {
  return mapTowTruck(
    await readJson(
      await requestApi("api/towing/tow-trucks", { method: "POST", body: JSON.stringify(request) }),
    ),
  );
}

export async function updateTowTruckAgainstApi(towCode: number, request: TowTruckRequest) {
  return mapTowTruck(
    await readJson(
      await requestApi(`api/towing/tow-trucks/${encodeURIComponent(towCode)}`, {
        method: "PUT",
        body: JSON.stringify(request),
      }),
    ),
  );
}

export async function deleteTowTruckAgainstApi(towCode: number) {
  await requestApi(`api/towing/tow-trucks/${encodeURIComponent(towCode)}`, { method: "DELETE" });
}

export async function getTowingRequestReport(startDate: string, endDate: string) {
  return mapTowingCollection(
    (await readJson(
      await requestApi("api/towing/reports/request", {
        method: "POST",
        body: JSON.stringify({ StartDate: startDate, EndDate: endDate }),
      }),
    )) as JsonRecord,
  ).filter((item) => item !== null);
}

export async function getAllTowtruckReport() {
  const payload = await readJson(await requestApi("api/towing/reports/towtruck/all"));
  return mapTowingCollection(payload);
}

export async function getFirmDateTowingReport(
  firmName: string,
  startDate: string,
  endDate: string,
) {
  return mapTowingCollection(
    await readJson(
      await requestApi("api/towing/reports/firm-date", {
        method: "POST",
        body: JSON.stringify({ FirmName: firmName, StartDate: startDate, EndDate: endDate }),
      }),
    ),
  );
}
