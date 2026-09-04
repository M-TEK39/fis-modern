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

export type CallCentreIncidentRecord = {
  code: number;
  vmfCode: number | null;
  incidentDescription: string | null;
  incidentTown: string | null;
  transportOfficerSite: number | null;
  transportOfficerName: string | null;
  transportOfficerTel: string | null;
};

export type TowTruckOption = {
  code: number;
  area: string | null;
  name: string | null;
  telephone: string | null;
  fax: string | null;
};

export type LossTypeOption = {
  code: number;
  description: string;
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

export type CreateAccidentRequest = CreateCallCentreRequest & {
  IncidentDate: string;
  IncidentTime: string | null;
  DriverName: string | null;
  DriverTel: string | null;
  DriverPersalno: string | null;
  AccidentDescription: string | null;
  DamageDescription: string | null;
  ThirdPartyRegistration: string | null;
  ThirdPartyOwner: string | null;
  ThirdPartyTelephone: string | null;
  Death: string;
  Injured: string;
  OccurencePlace: string;
  TowNeed: string;
  AccidentNotes: string | null;
  AccidentDriverName: string | null;
  AccidentDriverTel: string | null;
  AccidentDriverEmployNumber: string | null;
};

export type CreateAccidentTowingRequest = {
  VmfCode: number;
  CallRefer: number;
  RequestDate: string;
  RequestTime: string;
  Location: string | null;
  VehicleProblem: string | null;
  SiteCode: number | null;
  TowTruckCode: number | null;
  ContactPersonName: string | null;
  ContactPersonTel: string | null;
  ContactPersonCell: string | null;
  Remarks: string | null;
};

export type CreateHiJackRequest = CreateCallCentreRequest & {
  IncidentDate: string;
  IncidentTime: string | null;
  IncidentTown: string | null;
  IncidentStreet: string | null;
  DriverName: string | null;
  DriverTel: string | null;
  DriverPersalno: string | null;
  IncidentDesc: string | null;
};

export type CreateLossRequest = CreateCallCentreRequest & {
  IncidentDate: string;
  IncidentTown: string | null;
  IncidentStreet: string | null;
  DriverName: string | null;
  DriverTel: string | null;
  DriverPersalno: string | null;
  IncidentDesc: string | null;
  LossTypeCode: number;
  TowNeed: string;
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

function mapLossType(value: unknown): LossTypeOption | null {
  if (!isRecord(value)) {
    return null;
  }

  const code = asNumber(getValue(value, "LossTypeCode", "loss_type_code", "lossTypeCode"));
  const description = asString(getValue(value, "Description", "loss_description", "description"));
  if (code === null || !description) {
    return null;
  }

  return { code, description };
}

function mapCallCentreIncident(value: unknown): CallCentreIncidentRecord | null {
  if (!isRecord(value)) {
    return null;
  }

  const code = asNumber(getValue(value, "Call_centre_code", "call_centre_code", "callCentreCode"));
  if (code === null) {
    return null;
  }

  return {
    code,
    vmfCode: asNumber(getValue(value, "vmf_code", "VmfCode", "vmfCode")),
    incidentDescription: asString(getValue(value, "Incident_Desc", "incidentDesc", "incidentDescription")),
    incidentTown: asString(getValue(value, "Incident_town", "incidentTown")),
    transportOfficerSite: asNumber(getValue(value, "TrOfficer_Site", "transportOfficerSite")),
    transportOfficerName: asString(getValue(value, "TrOfficer_name", "transportOfficerName")),
    transportOfficerTel: asString(getValue(value, "TrOfficer_tel", "transportOfficerTel")),
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

export async function getCallCentreIncident(callCentreCode: number) {
  const response = await requestApi(`api/CallCentre/${encodeURIComponent(callCentreCode)}`);
  const incident = mapCallCentreIncident(await readJson(response));
  if (!incident) {
    throw new CallCentreApiError("invalid-response", "The FIS API returned an invalid call centre incident.");
  }

  return incident;
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

export async function getCallCentreLossTypes() {
  const response = await requestApi("api/LossType");
  return getCollection(await readJson(response))
    .map(mapLossType)
    .filter((lossType): lossType is LossTypeOption => lossType !== null)
    .sort((left, right) => left.description.localeCompare(right.description));
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

export async function createAccidentIncident(request: CreateAccidentRequest) {
  const response = await requestApi("api/CallCentre/accident", {
    method: "POST",
    body: JSON.stringify(request),
  });
  const payload = await readJson(response);
  if (!isRecord(payload)) {
    throw new CallCentreApiError("invalid-response", "The FIS API returned an invalid accident record.");
  }

  const callCentreCode = asNumber(getValue(payload, "CallCentreCode", "callCentreCode", "Call_centre_code"));
  const accidentCode = asNumber(getValue(payload, "AccidentCode", "accidentCode", "accident_code"));
  if (callCentreCode === null || accidentCode === null) {
    throw new CallCentreApiError("invalid-response", "The FIS API returned incomplete accident references.");
  }

  return { callCentreCode, accidentCode };
}

export async function createHiJackIncident(request: CreateHiJackRequest) {
  const response = await requestApi("api/CallCentre/hijack", {
    method: "POST",
    body: JSON.stringify(request),
  });
  const payload = await readJson(response);
  if (!isRecord(payload)) {
    throw new CallCentreApiError("invalid-response", "The FIS API returned an invalid Hi-Jack record.");
  }

  const callCentreCode = asNumber(getValue(payload, "CallCentreCode", "callCentreCode", "Call_centre_code"));
  if (callCentreCode === null) {
    throw new CallCentreApiError("invalid-response", "The FIS API returned an incomplete Hi-Jack reference.");
  }

  return { callCentreCode };
}

export async function createLossIncident(request: CreateLossRequest) {
  const response = await requestApi("api/CallCentre/loss", {
    method: "POST",
    body: JSON.stringify(request),
  });
  const payload = await readJson(response);
  if (!isRecord(payload)) {
    throw new CallCentreApiError("invalid-response", "The FIS API returned an invalid Loss/Theft record.");
  }

  const callCentreCode = asNumber(getValue(payload, "CallCentreCode", "callCentreCode", "Call_centre_code"));
  const lossCode = asNumber(getValue(payload, "LossCode", "lossCode", "loss_code"));
  if (callCentreCode === null || lossCode === null) {
    throw new CallCentreApiError("invalid-response", "The FIS API returned incomplete Loss/Theft references.");
  }

  return { callCentreCode, lossCode };
}

export async function createAccidentTowing(request: CreateAccidentTowingRequest) {
  const response = await requestApi("api/Towing", {
    method: "POST",
    body: JSON.stringify({
      vmf_code: request.VmfCode,
      Call_refer: request.CallRefer,
      Tow_request_date: request.RequestDate,
      Tow_request_time: request.RequestTime,
      Tow_location_start: request.Location,
      Vehicle_problem: request.VehicleProblem,
      Site_code: request.SiteCode,
      Tow_Truck_code: request.TowTruckCode,
      Contact_person_name: request.ContactPersonName,
      Contact_person_tel: request.ContactPersonTel,
      Contact_person_cell: request.ContactPersonCell,
      Remaks: request.Remarks,
    }),
  });
  const payload = await readJson(response);
  if (!isRecord(payload)) {
    throw new CallCentreApiError("invalid-response", "The FIS API returned an invalid towing record.");
  }

  const towingCode = asNumber(getValue(payload, "Towing_code", "towingCode", "TowingCode"));
  if (towingCode === null) {
    throw new CallCentreApiError("invalid-response", "The FIS API returned an incomplete towing reference.");
  }

  return towingCode;
}

export async function createLossTowing(request: CreateAccidentTowingRequest) {
  const response = await requestApi("api/Towing", {
    method: "POST",
    body: JSON.stringify({
      vmf_code: request.VmfCode,
      Call_refer: request.CallRefer,
      Tow_request_date: request.RequestDate,
      Tow_request_time: request.RequestTime,
      Tow_location_start: request.Location,
      Vehicle_problem: request.VehicleProblem,
      Site_code: request.SiteCode,
      Tow_Truck_code: request.TowTruckCode,
      Contact_person_name: request.ContactPersonName,
      Contact_person_tel: request.ContactPersonTel,
      Contact_person_cell: request.ContactPersonCell,
      Remaks: request.Remarks,
    }),
  });
  const payload = await readJson(response);
  if (!isRecord(payload)) {
    throw new CallCentreApiError("invalid-response", "The FIS API returned an invalid Loss/Theft towing record.");
  }

  const towingCode = asNumber(getValue(payload, "Towing_code", "towingCode", "TowingCode"));
  if (towingCode === null) {
    throw new CallCentreApiError("invalid-response", "The FIS API returned an incomplete Loss/Theft towing reference.");
  }

  return towingCode;
}
