import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

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
  callTime: string | null;
  callDate: string | null;
  incidentType: string | null;
  incidentDescription: string | null;
  captureName: string | null;
  userAccessCode: number | null;
  callerName: string | null;
  driverName: string | null;
  driverPersalNumber: string | null;
  driverLicenceNumber: string | null;
  ggNumber: string | null;
  driverBaseStation: string | null;
  driverSite: number | null;
  driverTel: string | null;
  driverCell: string | null;
  driverFax: string | null;
  driverEmail: string | null;
  incidentDate: string | null;
  incidentTime: string | null;
  callerTel: string | null;
  callerEmail: string | null;
  transportOfficerFax: string | null;
  transportOfficerEmail: string | null;
  incidentTown: string | null;
  incidentStreet: string | null;
  transportOfficerName: string | null;
  transportOfficerTel: string | null;
  transportOfficerSite: number | null;
  counter: number | null;
  callerFax: string | null;
  croNotification: string | null;
  croRemarks: string | null;
  incidentRemarks: string | null;
  notifyListCode: number | null;
  callClosed: string | null;
  dateCreated: string | null;
  dateUpdated: string | null;
};

export type CallCentreChildRecord = Record<string, unknown>;

export type CallCentreEditDetails = {
  accidentTableAvailable: boolean;
  accident: CallCentreChildRecord | null;
  lossTableAvailable: boolean;
  loss: CallCentreChildRecord | null;
  towingTableAvailable: boolean;
  towing: CallCentreChildRecord | null;
};

export type AccidentEditUpdate = {
  OccurenceDate: string | null;
  OccurenceTime: string | null;
  Description: string | null;
  DriverName: string | null;
  DriverEmployNumber: string | null;
  DriverTelno: string | null;
  DriverSiteCode: number | null;
  TransportOfficerName: string | null;
  TransportOfficerTel: string | null;
  Death: string | null;
  Injured: string | null;
  ThirdPartyRegistration: string | null;
  ThirdPartyOwner: string | null;
  ThirdPartyTelephone: string | null;
  DamageDescription: string | null;
  Notes: string | null;
  OccurencePlace: string | null;
  TowNeed: string | null;
};

export type LossEditUpdate = {
  LossDate: string | null;
  LossTypeCode: number | null;
  SiteCode: number | null;
  DepartmentContact: string | null;
  PlaceOfLoss: string | null;
  DriverName: string | null;
  Remarks: string | null;
  TowNeed: string | null;
};

export type TowingEditUpdate = {
  Location: string | null;
  VehicleProblem: string | null;
  SiteCode: number | null;
  TowTruckCode: number | null;
  ContactPersonName: string | null;
  ContactPersonTel: string | null;
  Remarks: string | null;
};

export type CallCentreEditUpdate = {
  Accident?: AccidentEditUpdate;
  Loss?: LossEditUpdate;
  Towing?: TowingEditUpdate;
};

export type UpdateCallCentreEditDetailsRequest = {
  CallCentre: UpdateCallCentreRequest;
  ChildUpdates: CallCentreEditUpdate;
};

export type CallCentreDataAccessEntry = {
  counterCode: number | null;
  counter: number | null;
  dataCaptureId: number | null;
  dataCaptureDate: string | null;
  dataCaptureTime: string | null;
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

export type UpdateCallCentreRequest = {
  VmfCode: number | null;
  CallTime: string | null;
  CallDate: string | null;
  IncidentType: string | null;
  IncidentDesc: string | null;
  CaptureName: string | null;
  UserAccessCode: number | null;
  CallerName: string | null;
  DriverName: string | null;
  DriverPersalno: string | null;
  DriverLicno: string | null;
  GGNumber: string | null;
  DriverBaseStation: string | null;
  DriverSite: number | null;
  DriverTel: string | null;
  DriverCell: string | null;
  DriverFax: string | null;
  DriverEmail: string | null;
  IncidentDate: string | null;
  IncidentTime: string | null;
  CallerTel: string | null;
  TransportOfficerName: string | null;
  TransportOfficerTel: string | null;
  TransportOfficerSite: number | null;
  IncidentTown: string | null;
  IncidentStreet: string | null;
  Counter: number | null;
  CallerFax: string | null;
  TransportOfficerFax: string | null;
  CallerEmail: string | null;
  TransportOfficerEmail: string | null;
  InformCro: string | null;
  CroRemarks: string | null;
  IncidentRemarks: string | null;
  NotifyListCode: number | null;
  CallClosed: string | null;
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
    asString(getValue(value, "DisplayText", "display_text", "displayText")) ||
    [fleetNumber, registrationNumber].filter(Boolean).join(" - ") ||
    String(vmfCode);

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
    departmentNumber: asString(
      getValue(value, "DepartmentNumber", "Department_number", "department_number"),
    ),
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
    callTime: asString(getValue(value, "Call_time", "callTime")),
    callDate: asString(getValue(value, "Call_date", "callDate")),
    incidentType: asString(getValue(value, "Incident_type", "incidentType")),
    incidentDescription: asString(
      getValue(value, "Incident_Desc", "incidentDesc", "incidentDescription"),
    ),
    captureName: asString(getValue(value, "Capture_name", "captureName")),
    userAccessCode: asNumber(getValue(value, "User_access_code", "userAccessCode")),
    callerName: asString(getValue(value, "Caller_name", "callerName")),
    driverName: asString(getValue(value, "Driver_name", "driverName")),
    driverPersalNumber: asString(getValue(value, "Driver_persalno", "driverPersalno")),
    driverLicenceNumber: asString(getValue(value, "Driver_Licno", "driverLicno")),
    ggNumber: asString(getValue(value, "GG_number", "ggNumber")),
    driverBaseStation: asString(getValue(value, "Driver_base_station", "driverBaseStation")),
    driverSite: asNumber(getValue(value, "Driver_Site", "driverSite")),
    driverTel: asString(getValue(value, "Driver_tel", "driverTel")),
    driverCell: asString(getValue(value, "Driver_cell", "driverCell")),
    driverFax: asString(getValue(value, "Driver_fax", "driverFax")),
    driverEmail: asString(getValue(value, "Driver_email", "driverEmail")),
    incidentDate: asString(getValue(value, "Incident_date", "incidentDate")),
    incidentTime: asString(getValue(value, "Incident_time", "incidentTime")),
    callerTel: asString(getValue(value, "Caller_tel", "callerTel")),
    callerEmail: asString(getValue(value, "Caller_email", "callerEmail")),
    transportOfficerFax: asString(getValue(value, "TrOfficer_fax", "transportOfficerFax")),
    transportOfficerEmail: asString(getValue(value, "TrOfficer_email", "transportOfficerEmail")),
    incidentTown: asString(getValue(value, "Incident_town", "incidentTown")),
    incidentStreet: asString(getValue(value, "Incident_street", "incidentStreet")),
    transportOfficerName: asString(getValue(value, "TrOfficer_name", "transportOfficerName")),
    transportOfficerTel: asString(getValue(value, "TrOfficer_tel", "transportOfficerTel")),
    transportOfficerSite: asNumber(getValue(value, "TrOfficer_Site", "transportOfficerSite")),
    counter: asNumber(getValue(value, "Counter", "counter")),
    callerFax: asString(getValue(value, "Caller_fax", "callerFax")),
    croNotification: asString(getValue(value, "Inform_CRO", "informCro")),
    croRemarks: asString(getValue(value, "CRO_Remarks", "croRemarks")),
    incidentRemarks: asString(getValue(value, "Incident_Remarks", "incidentRemarks")),
    notifyListCode: asNumber(getValue(value, "Notify_list_code", "notifyListCode")),
    callClosed: asString(getValue(value, "call_closed", "callClosed")),
    dateCreated: asString(getValue(value, "date_created", "dateCreated")),
    dateUpdated: asString(getValue(value, "date_updated", "dateUpdated")),
  };
}

function mapDataAccessEntry(value: unknown): CallCentreDataAccessEntry | null {
  if (!isRecord(value)) return null;
  return {
    counterCode: asNumber(getValue(value, "CallCentreCounterCode", "callCentreCounterCode")),
    counter: asNumber(getValue(value, "Counter", "counter")),
    dataCaptureId: asNumber(getValue(value, "DataCaptureId", "dataCaptureId")),
    dataCaptureDate: asString(getValue(value, "DataCaptureDate", "dataCaptureDate")),
    dataCaptureTime: asString(getValue(value, "DataCaptureTime", "dataCaptureTime")),
  };
}

export async function searchCallCentreVehicles(searchTerm: string) {
  const response = await requestApi(
    `api/VehicleLookup?keyword=${encodeURIComponent(searchTerm)}&limit=20`,
  );
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
    throw new CallCentreApiError(
      "invalid-response",
      "The FIS API returned an invalid call centre incident.",
    );
  }

  return incident;
}

export async function getCallCentreEditDetails(
  callCentreCode: number,
): Promise<CallCentreEditDetails> {
  const response = await requestApi(
    `api/CallCentre/${encodeURIComponent(callCentreCode)}/edit-details`,
  );
  const payload = await readJson(response);
  if (!isRecord(payload)) {
    throw new CallCentreApiError(
      "invalid-response",
      "The FIS API returned invalid call centre edit details.",
    );
  }

  const mapChild = (value: unknown) => (isRecord(value) ? value : null);
  return {
    accidentTableAvailable: Boolean(
      getValue(payload, "AccidentTableAvailable", "accidentTableAvailable"),
    ),
    accident: mapChild(getValue(payload, "Accident", "accident")),
    lossTableAvailable: Boolean(getValue(payload, "LossTableAvailable", "lossTableAvailable")),
    loss: mapChild(getValue(payload, "Loss", "loss")),
    towingTableAvailable: Boolean(
      getValue(payload, "TowingTableAvailable", "towingTableAvailable"),
    ),
    towing: mapChild(getValue(payload, "Towing", "towing")),
  };
}

export async function getCallCentreIncidents() {
  const response = await requestApi("api/CallCentre");
  return getCollection(await readJson(response))
    .map(mapCallCentreIncident)
    .filter((incident): incident is CallCentreIncidentRecord => incident !== null);
}

export async function updateCallCentreIncident(
  callCentreCode: number,
  request: UpdateCallCentreRequest,
) {
  const response = await requestApi(`api/CallCentre/${encodeURIComponent(callCentreCode)}`, {
    method: "PUT",
    body: JSON.stringify(request),
  });
  const payload = await readJson(response);
  const updated = mapCallCentreIncident(payload);
  if (!updated) {
    throw new CallCentreApiError(
      "invalid-response",
      "The FIS API returned an invalid updated call centre record.",
    );
  }

  return updated;
}

export async function updateCallCentreEditDetails(
  callCentreCode: number,
  request: UpdateCallCentreEditDetailsRequest,
) {
  const response = await requestApi(
    `api/CallCentre/${encodeURIComponent(callCentreCode)}/edit-details`,
    {
      method: "PUT",
      body: JSON.stringify(request),
    },
  );
  const payload = await readJson(response);
  const updated = mapCallCentreIncident(payload);
  if (!updated) {
    throw new CallCentreApiError(
      "invalid-response",
      "The FIS API returned an invalid updated call centre record.",
    );
  }

  return updated;
}

export async function getCallCentreDataAccess(callCentreCode: number) {
  const response = await requestApi(
    `api/CallCentre/reports/data-access/${encodeURIComponent(callCentreCode)}`,
  );
  const payload = await readJson(response);
  if (!isRecord(payload)) {
    throw new CallCentreApiError(
      "invalid-response",
      "The FIS API returned an invalid data access report.",
    );
  }

  const entries = getCollection(getValue(payload, "Entries", "entries"))
    .map(mapDataAccessEntry)
    .filter((entry): entry is CallCentreDataAccessEntry => entry !== null);
  return {
    accessTableAvailable: Boolean(
      getValue(payload, "AccessTableAvailable", "accessTableAvailable"),
    ),
    entries,
  };
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
    throw new CallCentreApiError(
      "invalid-response",
      "The FIS API returned an invalid call centre record.",
    );
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
    throw new CallCentreApiError(
      "invalid-response",
      "The FIS API returned an invalid road assistance record.",
    );
  }

  const callCentreCode = asNumber(
    getValue(payload, "CallCentreCode", "callCentreCode", "Call_centre_code"),
  );
  const towingCode = asNumber(getValue(payload, "TowingCode", "towingCode", "Towing_code"));
  if (callCentreCode === null || towingCode === null) {
    throw new CallCentreApiError(
      "invalid-response",
      "The FIS API returned incomplete road assistance references.",
    );
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
    throw new CallCentreApiError(
      "invalid-response",
      "The FIS API returned an invalid accident record.",
    );
  }

  const callCentreCode = asNumber(
    getValue(payload, "CallCentreCode", "callCentreCode", "Call_centre_code"),
  );
  const accidentCode = asNumber(getValue(payload, "AccidentCode", "accidentCode", "accident_code"));
  if (callCentreCode === null || accidentCode === null) {
    throw new CallCentreApiError(
      "invalid-response",
      "The FIS API returned incomplete accident references.",
    );
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
    throw new CallCentreApiError(
      "invalid-response",
      "The FIS API returned an invalid Hi-Jack record.",
    );
  }

  const callCentreCode = asNumber(
    getValue(payload, "CallCentreCode", "callCentreCode", "Call_centre_code"),
  );
  if (callCentreCode === null) {
    throw new CallCentreApiError(
      "invalid-response",
      "The FIS API returned an incomplete Hi-Jack reference.",
    );
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
    throw new CallCentreApiError(
      "invalid-response",
      "The FIS API returned an invalid Loss/Theft record.",
    );
  }

  const callCentreCode = asNumber(
    getValue(payload, "CallCentreCode", "callCentreCode", "Call_centre_code"),
  );
  const lossCode = asNumber(getValue(payload, "LossCode", "lossCode", "loss_code"));
  if (callCentreCode === null || lossCode === null) {
    throw new CallCentreApiError(
      "invalid-response",
      "The FIS API returned incomplete Loss/Theft references.",
    );
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
    throw new CallCentreApiError(
      "invalid-response",
      "The FIS API returned an invalid towing record.",
    );
  }

  const towingCode = asNumber(getValue(payload, "Towing_code", "towingCode", "TowingCode"));
  if (towingCode === null) {
    throw new CallCentreApiError(
      "invalid-response",
      "The FIS API returned an incomplete towing reference.",
    );
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
    throw new CallCentreApiError(
      "invalid-response",
      "The FIS API returned an invalid Loss/Theft towing record.",
    );
  }

  const towingCode = asNumber(getValue(payload, "Towing_code", "towingCode", "TowingCode"));
  if (towingCode === null) {
    throw new CallCentreApiError(
      "invalid-response",
      "The FIS API returned an incomplete Loss/Theft towing reference.",
    );
  }

  return towingCode;
}
