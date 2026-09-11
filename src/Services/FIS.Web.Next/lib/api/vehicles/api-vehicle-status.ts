import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";
import {
  VEHICLE_STATUS_OPTIONS,
  VEHICLE_STATUS_REPORT_PAGE_SIZE,
  type VehicleStatusOption,
  type VehicleStatusReport,
  type VehicleStatusReportFilters,
  type VehicleStatusReportRemark,
  type VehicleStatusReportRow,
  type VehicleStatusSite,
  type VehicleStatusType,
} from "@/app/(fleet-operations)/vehicles/status/status-types";

export { VEHICLE_STATUS_OPTIONS, VEHICLE_STATUS_REPORT_PAGE_SIZE };
export type {
  VehicleStatusOption,
  VehicleStatusReport,
  VehicleStatusReportFilters,
  VehicleStatusReportRemark,
  VehicleStatusReportRow,
  VehicleStatusSite,
  VehicleStatusType,
} from "@/app/(fleet-operations)/vehicles/status/status-types";

const API_TIMEOUT_MS = 8_000;

type JsonRecord = Record<string, unknown>;

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

export type VehicleStatusSearchPage = {
  items: VehicleStatusVehicle[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
};

export const VEHICLE_STATUS_SEARCH_PAGE_SIZE = 24;

export type VehicleStatusChangeResult = {
  vmfCode: number;
  previousStatusCode: number | null;
  newStatusCode: number | null;
  newStatusDescription: string | null;
  effectiveDate: string | null;
  locationCode: number | null;
  actionsPerformed: string[];
};

export type VehicleStatusApiErrorReason =
  "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class VehicleStatusApiError extends Error {
  constructor(
    public readonly reason: VehicleStatusApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "VehicleStatusApiError";
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

function requiredInteger(record: JsonRecord, key: string, minimum: number, label: string) {
  const value = asNumber(record[key]);
  if (value === null || !Number.isSafeInteger(value) || value < minimum) {
    throw new VehicleStatusApiError(
      "invalid-response",
      `The FIS API returned an invalid ${label}.`,
    );
  }

  return value;
}

function requiredBoolean(record: JsonRecord, key: string, label: string) {
  const value = record[key];
  if (typeof value !== "boolean") {
    throw new VehicleStatusApiError(
      "invalid-response",
      `The FIS API returned an invalid ${label}.`,
    );
  }

  return value;
}

function requiredNullableString(record: JsonRecord, key: string, label: string) {
  const value = record[key];
  if (value === null) {
    return null;
  }

  if (typeof value !== "string") {
    throw new VehicleStatusApiError(
      "invalid-response",
      `The FIS API returned an invalid ${label}.`,
    );
  }

  return value.trim() || null;
}

function requiredCollection(record: JsonRecord, key: string, label: string) {
  const value = record[key];
  if (!Array.isArray(value)) {
    throw new VehicleStatusApiError("invalid-response", `The FIS API returned invalid ${label}.`);
  }

  return value;
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

      throw new VehicleStatusApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
      );
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
    fleetNumber:
      asString(getValue(value, "fleet_number", "fleetNumber", "gg_number", "ggNumber")) || null,
    registrationNumber:
      asString(getValue(value, "registration_number", "registrationNumber")) || null,
    modelName:
      asString(
        getValue(value, "model_name", "modelName", "model_description", "modelDescription"),
      ) || null,
    typeName:
      asString(getValue(value, "type_name", "typeName", "type_description", "typeDescription")) ||
      null,
    statusCode: asNumber(getValue(value, "vehicle_status_code", "vehicleStatusCode")) ?? 0,
    statusDescription:
      asString(
        getValue(
          value,
          "status_description",
          "statusDescription",
          "vehicle_status_description",
          "status",
          "Status",
        ),
      ) || null,
    statusDate: asString(getValue(value, "vehicle_status_date", "vehicleStatusDate")) || null,
    locationCode: asNumber(getValue(value, "location_code", "locationCode")),
    locationDescription:
      asString(getValue(value, "location_description", "locationDescription")) || null,
    siteCode: asNumber(getValue(value, "site_code", "siteCode", "location_code", "locationCode")),
    yearManufactured: asNumber(getValue(value, "year_manufactured", "yearManufactured")),
    colour: asString(getValue(value, "colour")) || null,
    chassisNumber: asString(getValue(value, "chassis_number", "chassisNumber")) || null,
    engineNumber:
      asString(getValue(value, "engine_number_1", "engineNumber1", "engine_number")) || null,
    hiredFrom:
      asString(getValue(value, "purchased_from", "purchasedFrom", "hired_from", "hiredFrom")) ||
      null,
    currentOdo: asNumber(getValue(value, "current_odo", "currentOdo")),
  };
}

function mapReportRemark(value: unknown): VehicleStatusReportRemark | null {
  if (!isRecord(value)) {
    return null;
  }

  const remarkId = asNumber(getValue(value, "remark_id", "remarkId"));
  if (remarkId === null) {
    return null;
  }

  return {
    remarkId,
    category: asString(getValue(value, "remark_category", "remarkCategory")) || null,
    text: asString(getValue(value, "remark_text", "remarkText")) || null,
  };
}

function mapReportRow(value: unknown): VehicleStatusReportRow | null {
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
    registrationNumber:
      asString(getValue(value, "registration_number", "registrationNumber")) || null,
    invoiceNumber: asString(getValue(value, "invoice_number", "invoiceNumber")) || null,
    makeDescription: asString(getValue(value, "make_description", "makeDescription")) || null,
    modelDescription: asString(getValue(value, "model_description", "modelDescription")) || null,
    statusCode: asNumber(getValue(value, "vehicle_status_code", "vehicleStatusCode")) ?? 0,
    statusText:
      asString(
        getValue(value, "status_text", "statusText", "status_description", "statusDescription"),
      ) || null,
    typeCode: asNumber(getValue(value, "type_code", "typeCode")),
    typeDescription: asString(getValue(value, "type_description", "typeDescription")) || null,
    locationCode: asNumber(getValue(value, "location_code", "locationCode")),
    siteName:
      asString(
        getValue(value, "site_name", "siteName", "location_description", "locationDescription"),
      ) || null,
    remark: mapReportRemark(getValue(value, "active_remark", "activeRemark")),
  };
}

function mapReportLookup(value: unknown): VehicleStatusOption | null {
  if (!isRecord(value)) {
    return null;
  }

  const code = asNumber(getValue(value, "code", "Code"));
  const description = asString(getValue(value, "description", "Description"));
  return code !== null && description ? { code, description } : null;
}

function mapReportLookups(value: unknown) {
  return getCollection(value)
    .map(mapReportLookup)
    .filter((lookup): lookup is VehicleStatusOption => lookup !== null)
    .toSorted((left, right) => left.description.localeCompare(right.description));
}

export async function searchVehiclesForStatus(searchTerm: string) {
  const response = await requestApi(
    `api/vehicles/search?searchTerm=${encodeURIComponent(searchTerm)}`,
  );
  return getCollection(await readJson(response))
    .map(mapVehicle)
    .filter((vehicle): vehicle is VehicleStatusVehicle => vehicle !== null);
}

export async function searchVehiclesForStatusPage(
  searchTerm: string,
  searchMode: "GG" | "GP",
  page = 1,
): Promise<VehicleStatusSearchPage> {
  const normalizedSearchTerm = searchTerm.trim();
  const requestedPage = Number.isSafeInteger(page) && page > 0 ? page : 1;
  if (!normalizedSearchTerm) {
    return {
      items: [],
      page: 1,
      pageSize: VEHICLE_STATUS_SEARCH_PAGE_SIZE,
      total: 0,
      totalPages: 1,
    };
  }

  const params = new URLSearchParams({
    keyword: normalizedSearchTerm,
    mode: searchMode,
    page: String(requestedPage),
    pageSize: String(VEHICLE_STATUS_SEARCH_PAGE_SIZE),
  });
  const payload = await readJson(await requestApi(`api/VehicleLookup/page?${params.toString()}`));
  if (!isRecord(payload) || !Array.isArray(payload.items)) {
    throw new VehicleStatusApiError(
      "invalid-response",
      "The FIS API returned an invalid vehicle search page.",
    );
  }

  const pageValue = asNumber(getValue(payload, "page", "Page"));
  const pageSize = asNumber(getValue(payload, "pageSize", "PageSize"));
  const total = asNumber(getValue(payload, "total", "Total"));
  const totalPages = asNumber(getValue(payload, "totalPages", "TotalPages"));
  if (
    pageValue === null ||
    pageValue < 1 ||
    pageSize === null ||
    pageSize < 1 ||
    total === null ||
    total < 0 ||
    totalPages === null ||
    totalPages < 1
  ) {
    throw new VehicleStatusApiError(
      "invalid-response",
      "The FIS API returned incomplete vehicle search pagination metadata.",
    );
  }

  return {
    items: payload.items
      .map(mapVehicle)
      .filter((vehicle): vehicle is VehicleStatusVehicle => vehicle !== null),
    page: pageValue,
    pageSize,
    total,
    totalPages,
  };
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
      const description = asString(
        getValue(value, "description", "site_description", "siteDescription"),
      );
      return code !== null && description
        ? ({ code, description } satisfies VehicleStatusSite)
        : null;
    })
    .filter((site): site is VehicleStatusSite => site !== null)
    .toSorted((left, right) => left.description.localeCompare(right.description));
}

export async function getVehicleTypesForStatus(): Promise<VehicleStatusType[]> {
  const response = await requestApi("api/type");
  return getCollection(await readJson(response))
    .map((value) => {
      if (!isRecord(value)) {
        return null;
      }

      const code = asNumber(getValue(value, "type_code", "typeCode"));
      const description = asString(getValue(value, "type_description", "typeDescription"));
      return code !== null && description
        ? ({ code, description } satisfies VehicleStatusType)
        : null;
    })
    .filter((type): type is VehicleStatusType => type !== null)
    .toSorted((left, right) => left.description.localeCompare(right.description));
}

export async function getVehicleStatusReport(
  filters: VehicleStatusReportFilters = {},
  page = 1,
): Promise<VehicleStatusReport> {
  const query = new URLSearchParams();
  const requestedPage = Number.isSafeInteger(page) && page > 0 ? page : 1;
  query.set("page", String(requestedPage));
  query.set("pageSize", String(VEHICLE_STATUS_REPORT_PAGE_SIZE));
  if (filters.search?.trim()) {
    query.set("search", filters.search.trim());
  }
  if (filters.locationCode !== undefined) {
    query.set("location_code", String(filters.locationCode));
  }
  if (filters.typeCode !== undefined) {
    query.set("type_code", String(filters.typeCode));
  }
  if (filters.makeCode !== undefined) {
    query.set("make_code", String(filters.makeCode));
  }
  if (filters.vehicleStatusCode !== undefined) {
    query.set("vehicle_status_code", String(filters.vehicleStatusCode));
  }

  const path = `api/report/new-in-service${query.size > 0 ? `?${query.toString()}` : ""}`;
  const payload = await readJson(await requestApi(path));
  if (!isRecord(payload)) {
    throw new VehicleStatusApiError(
      "invalid-response",
      "The FIS API returned an invalid vehicle status report.",
    );
  }

  const totalCount = requiredInteger(payload, "total_count", 0, "total vehicle count");
  const responsePage = requiredInteger(payload, "page", 1, "report page");
  const pageSize = requiredInteger(payload, "page_size", 1, "report page size");
  const totalPages = requiredInteger(payload, "total_pages", 1, "report page count");
  const vehicles = requiredCollection(payload, "vehicles", "vehicle results");
  const availableFilters = getValue(payload, "available_filters");
  if (!isRecord(availableFilters)) {
    throw new VehicleStatusApiError(
      "invalid-response",
      "The FIS API returned invalid vehicle status filters.",
    );
  }

  const rows = vehicles
    .map(mapReportRow)
    .filter((row): row is VehicleStatusReportRow => row !== null);

  const sites = mapReportLookups(
    requiredCollection(availableFilters, "sites", "site filters"),
  ) as VehicleStatusSite[];
  const types = mapReportLookups(
    requiredCollection(availableFilters, "types", "type filters"),
  ) as VehicleStatusType[];
  const makes = mapReportLookups(requiredCollection(availableFilters, "makes", "make filters"));

  return {
    totalCount,
    page: responsePage,
    pageSize,
    totalPages,
    remarksAvailable: requiredBoolean(payload, "remarks_available", "remarks availability"),
    assumptionNote: requiredNullableString(payload, "assumption_note", "assumption note"),
    sites,
    types,
    makes,
    rows,
  };
}

async function completeRemarkRequest(path: string, body: object) {
  await readJson(
    await requestApi(path, {
      method: "POST",
      body: JSON.stringify(body),
    }),
  );
}

export async function addVehicleRemarkAgainstApi(vmfCode: number, category: string, text: string) {
  await completeRemarkRequest(`api/vehicles/${encodeURIComponent(vmfCode)}/remarks`, {
    remark_category: category,
    remark_text: text,
  });
}

export async function resolveVehicleRemarkAgainstApi(
  vmfCode: number,
  remarkId: number,
  resolutionNotes: string,
) {
  await completeRemarkRequest(
    `api/vehicles/${encodeURIComponent(vmfCode)}/remarks/${encodeURIComponent(remarkId)}/resolve`,
    {
      resolution_notes: resolutionNotes,
    },
  );
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
    throw new VehicleStatusApiError(
      "invalid-response",
      "The FIS API returned an invalid status response.",
    );
  }

  return {
    vmfCode: asNumber(getValue(payload, "vmf_code", "vmfCode")) ?? vmfCode,
    previousStatusCode: asNumber(getValue(payload, "previous_status_code", "previousStatusCode")),
    newStatusCode: asNumber(getValue(payload, "new_status_code", "newStatusCode")),
    newStatusDescription:
      asString(getValue(payload, "new_status_description", "newStatusDescription")) || null,
    effectiveDate: asString(getValue(payload, "effective_date", "effectiveDate")) || null,
    locationCode: asNumber(getValue(payload, "location_code", "locationCode")),
    actionsPerformed: mapPresent(
      getCollection(getValue(payload, "actions_performed", "actionsPerformed")),
      asString,
    ),
  } satisfies VehicleStatusChangeResult;
}
