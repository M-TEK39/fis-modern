import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type CallCentreVehicleOption = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  displayText: string;
};

export type CallCentreSiteOption = {
  code: number;
  description: string;
  departmentNumber: string | null;
};

export type TowTruckOption = {
  code: number;
  area: string | null;
  name: string | null;
  telephone: string | null;
  fax: string | null;
};

export type CreateCallCentreRequest = {
  VmfCode: number;
  IncidentType: string;
  TransportOfficerName: string | null;
  TransportOfficerTel: string | null;
  TransportOfficerFax: string | null;
  TransportOfficerEmail: string | null;
  TransportOfficerSite: number | null;
  CallerName: string | null;
  CallerTel: string | null;
  CallerFax: string | null;
  CallerEmail: string | null;
  InformCro: string;
  CroRemarks: string | null;
  IncidentRemarks: string | null;
  NotifyListCode: number | null;
  CallClosed: string;
};

export type CreateRoadAssistanceRequest = CreateCallCentreRequest & {
  GGNumber: string | null;
  IncidentDate: string;
  IncidentTime: string;
  IncidentTown: string | null;
  IncidentStreet: string | null;
  DriverName: string | null;
  DriverTel: string | null;
  DriverPersalno: string | null;
  TowingLocationStart: string | null;
  VehicleProblem: string | null;
  TowingRemarks: string | null;
  TowTruckCode: number | null;
};

export class CallCentreApiError extends Error {
  constructor(
    public readonly reason: "unauthorized" | "unavailable" | "invalid-response" | "not-found",
    message: string,
  ) {
    super(message);
    this.name = "CallCentreApiError";
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

  if (typeof value === "number") {
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
    const value = getValue(payload, "data", "items", "results");
    return Array.isArray(value) ? value : [];
  }

  return [];
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new CallCentreApiError("unauthorized", "No FIS access cookie is available.");
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
      throw new CallCentreApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (response.status === 404) {
      throw new CallCentreApiError("not-found", "The requested call centre record was not found.");
    }

    if (!response.ok) {
      throw new CallCentreApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        `FIS API returned HTTP ${response.status}.`,
      );
    }

    return response;
  } catch (error) {
    if (error instanceof CallCentreApiError) {
      throw error;
    }

    throw new CallCentreApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new CallCentreApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapVehicle(value: unknown): CallCentreVehicleOption | null {
  if (!isRecord(value)) {
    return null;
  }

  const vmfCode = asNumber(getValue(value, "VmfCode", "vmf_code", "vmfCode"));
  if (vmfCode === null) {
    return null;
  }

  const fleetNumber = asString(getValue(value, "FleetNumber", "fleet_number", "fleetNumber"));
  const registrationNumber = asString(
    getValue(value, "RegistrationNumber", "registration_number", "registrationNumber"),
  );
  const displayText =
    asString(getValue(value, "DisplayText", "display_text", "displayText"))
    || [fleetNumber, registrationNumber].filter(Boolean).join(" - ")
    || String(vmfCode);

  return { vmfCode, fleetNumber, registrationNumber, displayText };
}

function mapSite(value: unknown): CallCentreSiteOption | null {
  if (!isRecord(value)) {
    return null;
  }

  const code = asNumber(getValue(value, "SiteCode", "site_code", "siteCode"));
  const description = asString(getValue(value, "Description", "description", "site_description"));
  if (code === null || !description) {
    return null;
  }

  return {
    code,
    description,
    departmentNumber: asString(getValue(value, "DepartmentNumber", "Department_number", "department_number")),
  };
}

function mapTowTruck(value: unknown): TowTruckOption | null {
  if (!isRecord(value)) {
    return null;
  }

  const code = asNumber(getValue(value, "TowCode", "towCode", "Tow_code"));
  if (code === null) {
    return null;
  }

  return {
    code,
    area: asString(getValue(value, "TowArea", "towArea", "Tow_area")),
    name: asString(getValue(value, "TowName", "towName", "Tow_name")),
    telephone: asString(getValue(value, "TowTel", "towTel", "Tow_tel")),
    fax: asString(getValue(value, "TowFax", "towFax", "Tow_fax")),
  };
}

export async function searchCallCentreVehicles(searchTerm: string) {
  const response = await requestApi(`api/VehicleLookup?keyword=${encodeURIComponent(searchTerm)}&limit=20`);
  return getCollection(await readJson(response))
    .map(mapVehicle)
    .filter((vehicle): vehicle is CallCentreVehicleOption => vehicle !== null)
    .sort((left, right) => left.displayText.localeCompare(right.displayText));
}

export async function getCallCentreVehicle(vmfCode: number) {
  const response = await requestApi(`api/VehicleLookup/${encodeURIComponent(vmfCode)}`);
  const vehicle = mapVehicle(await readJson(response));
  if (!vehicle) {
    throw new CallCentreApiError("invalid-response", "The FIS API returned an invalid vehicle.");
  }

  return vehicle;
}

export async function getCallCentreSites() {
  const response = await requestApi("api/site");
  return getCollection(await readJson(response))
    .map(mapSite)
    .filter((site): site is CallCentreSiteOption => site !== null)
    .sort((left, right) => left.description.localeCompare(right.description));
}

export async function getCallCentreTowTrucks() {
  const response = await requestApi("api/Towing/tow-trucks");
  return getCollection(await readJson(response))
    .map(mapTowTruck)
    .filter((towTruck): towTruck is TowTruckOption => towTruck !== null)
    .sort((left, right) => (left.name ?? "").localeCompare(right.name ?? ""));
}

export async function createCallCentreIncident(request: CreateCallCentreRequest) {
  const response = await requestApi("api/CallCentre", {
    method: "POST",
    body: JSON.stringify(request),
  });
  const payload = await readJson(response);
  if (!isRecord(payload)) {
    throw new CallCentreApiError("invalid-response", "The FIS API returned an invalid call centre record.");
  }

  return asNumber(getValue(payload, "Call_centre_code", "call_centre_code", "callCentreCode"));
}

export async function createRoadAssistanceIncident(request: CreateRoadAssistanceRequest) {
  const response = await requestApi("api/CallCentre/road-assistance", {
    method: "POST",
    body: JSON.stringify(request),
  });
  const payload = await readJson(response);
  if (!isRecord(payload)) {
    throw new CallCentreApiError("invalid-response", "The FIS API returned an invalid road assistance record.");
  }

  const callCentreCode = asNumber(getValue(payload, "CallCentreCode", "callCentreCode", "Call_centre_code"));
  const towingCode = asNumber(getValue(payload, "TowingCode", "towingCode", "Towing_code"));
  if (callCentreCode === null || towingCode === null) {
    throw new CallCentreApiError("invalid-response", "The FIS API returned incomplete road assistance references.");
  }

  return { callCentreCode, towingCode };
}
