import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type TripAuthorityVehicle = {
  vmfCode: number;
  contractCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  licenceDueDate: string | null;
  make: string | null;
  model: string | null;
  contractType: string | null;
  siteCode: number | null;
};

export type TripAuthorityRecord = {
  tripId: number;
  contractCode: number;
  vmfCode: number | null;
  siteCode: number | null;
  approverName: string | null;
  approverRank: string | null;
  approverTelephone: string | null;
  tripReason: string | null;
  tripRequestNumber: string | null;
  tripTypeCode: number | null;
  tripIncidentTypeCode: number | null;
  userAccessCode: number | null;
  lockedForTransfer: boolean;
  tripIsMonthly: boolean;
  endOdometer: number | null;
  issueDate: string | null;
  expiryDate: string | null;
};

export type TripAuthorityDriver = {
  tripDriverCode: number;
  name: string | null;
  identityNumber: string | null;
  isPrimary: boolean;
  siteCode: number | null;
  licenceTypeCode: number | null;
  passportNumber: string | null;
  persalNumber: string | null;
  contractNumber: string | null;
  licenceNumber: string | null;
  licenceIssueDate: string | null;
  licenceLastVerifiedDate: string | null;
  hasPdp: boolean;
  pdpExpiryDate: string | null;
  licenceExpiryDate: string | null;
  isActive: boolean;
};

export type TripAuthorityPassenger = {
  tripPassengerCode: number;
  name: string | null;
};

export type TripAuthorityRoute = {
  routeCode: number;
  startDate: string | null;
  endDate: string | null;
  startOdometer: number | null;
  endOdometer: number | null;
  responsibilityCode: string | null;
  objectiveCode: string | null;
  startLocation: string | null;
  endLocation: string | null;
  estimatedDistance: number | null;
  distance: number | null;
  projectNumber: string | null;
  fundCode: string | null;
  editedByUserCode: number | null;
};

export type TripAuthorityDetails = {
  trip: TripAuthorityRecord;
  drivers: TripAuthorityDriver[];
  passengers: TripAuthorityPassenger[];
  routes: TripAuthorityRoute[];
};

export type CloseTripAuthorityRequest = {
  endOdometer: number | null;
  routes: Array<{ routeCode: number; endOdometer: number }>;
};

export type CreateTripAuthorityRequest = {
  contractCode: number;
  approverName: string;
  approverRank: string;
  approverTelephone: string | null;
  expiryDate: string | null;
  tripReason: string;
  tripRequestNumber: string | null;
  tripTypeCode: number;
  tripIncidentTypeCode: number;
  userAccessCode: number | null;
  tripIsMonthly: boolean;
  drivers: Array<{
    name: string;
    identityNumber: string | null;
    isPrimary: boolean;
    siteCode: number | null;
    licenceTypeCode: number | null;
    passportNumber: string | null;
    persalNumber: string | null;
    contractNumber: string | null;
    licenceNumber: string | null;
    licenceIssueDate: string | null;
    licenceLastVerifiedDate: string | null;
    hasPdp: boolean;
    pdpExpiryDate: string | null;
    licenceExpiryDate: string | null;
    isActive: boolean;
  }>;
  passengers: Array<{ name: string }>;
  routes: Array<{
    startDate: string;
    endDate: string;
    startLocation: string | null;
    endLocation: string | null;
    estimatedDistance: number | null;
    responsibilityCode: string;
    objectiveCode: string;
    projectNumber: string;
    fundCode: string;
  }>;
};

export class TripAuthorityApiError extends Error {
  constructor(
    public readonly reason:
      "unauthorized" | "unavailable" | "invalid-response" | "not-found" | "rejected",
    message: string,
  ) {
    super(message);
    this.name = "TripAuthorityApiError";
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
  if (typeof value === "number") return value !== 0;
  return ["true", "1", "yes", "y"].includes(String(value).trim().toLowerCase());
}

function getCollection(payload: unknown) {
  if (Array.isArray(payload)) return payload;
  if (isRecord(payload)) {
    const collection = getValue(payload, "data", "items", "results");
    return Array.isArray(collection) ? collection : [];
  }

  return [];
}

function statusDescriptionForCode(statusCode: number) {
  const descriptions: Record<number, string> = {
    1: "In Service",
    2: "Withdrawn",
    3: "Board of Survey",
    4: "Stolen",
    5: "Sold",
    6: "Transferred",
    7: "Subsidized",
    8: "From Focus",
    9: "Privatised",
    10: "Recovered",
    11: "Missing",
    12: "Destroyed",
  };

  return descriptions[statusCode] ?? null;
}

function mapVehicle(value: unknown): TripAuthorityVehicle | null {
  if (!isRecord(value)) return null;

  const vmfCode = asNumber(getValue(value, "vmf_code", "vmfCode"));
  const contractCode = asNumber(getValue(value, "contractCode", "contract_code"));
  if (vmfCode === null || contractCode === null) return null;

  const statusCode = asNumber(getValue(value, "vehicle_status_code", "vehicleStatusCode")) ?? 0;
  return {
    vmfCode,
    contractCode,
    fleetNumber: asString(getValue(value, "fleetNumber", "fleet_number")),
    registrationNumber: asString(getValue(value, "registrationNumber", "registration_number")),
    licenceDueDate: asString(getValue(value, "licenceDueDate", "licence_due_date")),
    make: asString(getValue(value, "makeDescription", "make_description", "make")),
    model: asString(getValue(value, "modelDescription", "model_description", "model")),
    contractType:
      asString(getValue(value, "contractType", "contract_type")) ??
      statusDescriptionForCode(statusCode),
    siteCode: asNumber(getValue(value, "siteCode", "site_code")),
  };
}

function mapTrip(value: unknown): TripAuthorityRecord | null {
  if (!isRecord(value)) return null;

  const tripId = asNumber(getValue(value, "tripAuthorityCode", "trip_authority_code", "tripId"));
  const contractCode = asNumber(getValue(value, "contractCode", "contract_code"));
  if (tripId === null || contractCode === null) return null;

  return {
    tripId,
    contractCode,
    vmfCode: asNumber(getValue(value, "vmfCode", "vmf_code")),
    siteCode: asNumber(getValue(value, "siteCode", "site_code")),
    approverName: asString(getValue(value, "approverName", "approver_name")),
    approverRank: asString(getValue(value, "approverRank", "approver_rank")),
    approverTelephone: asString(getValue(value, "approverTelephone", "approver_tel")),
    tripReason: asString(getValue(value, "tripReason", "trip_reason")),
    tripRequestNumber: asString(getValue(value, "tripRequestNumber", "trip_request_number")),
    tripTypeCode: asNumber(getValue(value, "tripTypeCode", "trip_type_code")),
    tripIncidentTypeCode: asNumber(
      getValue(value, "tripIncidentTypeCode", "trip_incident_type_code"),
    ),
    userAccessCode: asNumber(getValue(value, "userAccessCode", "user_access_code")),
    lockedForTransfer: asBoolean(getValue(value, "lockedForTransfer", "locked_for_transfer")),
    tripIsMonthly: asBoolean(
      getValue(value, "tripIsMonthly", "Trip_Is_Monthly", "trip_is_monthly"),
    ),
    endOdometer: asNumber(getValue(value, "endOdometer", "end_odo_meter")),
    issueDate: asString(getValue(value, "issueDate", "issue_date")),
    expiryDate: asString(getValue(value, "expiryDate", "expiry_date")),
  };
}

function mapDriver(value: unknown): TripAuthorityDriver | null {
  if (!isRecord(value)) return null;
  const tripDriverCode = asNumber(getValue(value, "tripDriverCode", "trip_driver_code"));
  if (tripDriverCode === null) return null;
  return {
    tripDriverCode,
    name: asString(getValue(value, "name", "trip_driver_name")),
    identityNumber: asString(getValue(value, "identityNumber", "trip_driver_id")),
    isPrimary: asBoolean(getValue(value, "isPrimary", "trip_driver_primary")),
    siteCode: asNumber(getValue(value, "siteCode", "site_code")),
    licenceTypeCode: asNumber(getValue(value, "licenceTypeCode", "driver_licence_type_id")),
    passportNumber: asString(getValue(value, "passportNumber", "driver_passportnumber")),
    persalNumber: asString(getValue(value, "persalNumber", "driver_persalnumber")),
    contractNumber: asString(getValue(value, "contractNumber", "driver_contractnumber")),
    licenceNumber: asString(getValue(value, "licenceNumber", "driver_licence_number")),
    licenceIssueDate: asString(getValue(value, "licenceIssueDate", "driver_licence_issuedate")),
    licenceLastVerifiedDate: asString(
      getValue(value, "licenceLastVerifiedDate", "driver_licence_lastVerifiedDate"),
    ),
    hasPdp: asBoolean(getValue(value, "hasPdp", "driver_hasPDP")),
    pdpExpiryDate: asString(getValue(value, "pdpExpiryDate", "driver_PDP_ExpiryDate")),
    licenceExpiryDate: asString(getValue(value, "licenceExpiryDate", "driver_licence_ExpiryDate")),
    isActive: asBoolean(getValue(value, "isActive", "driver_active")),
  };
}

function mapPassenger(value: unknown): TripAuthorityPassenger | null {
  if (!isRecord(value)) return null;
  const tripPassengerCode = asNumber(getValue(value, "tripPassengerCode", "trip_passenger_code"));
  return tripPassengerCode === null
    ? null
    : { tripPassengerCode, name: asString(getValue(value, "name", "trip_passenger_name")) };
}

function mapRoute(value: unknown): TripAuthorityRoute | null {
  if (!isRecord(value)) return null;
  const routeCode = asNumber(getValue(value, "routeCode", "route_code"));
  if (routeCode === null) return null;
  return {
    routeCode,
    startDate: asString(getValue(value, "startDate", "start_date")),
    endDate: asString(getValue(value, "endDate", "end_date")),
    startOdometer: asNumber(getValue(value, "startOdometer", "start_odo_meter")),
    endOdometer: asNumber(getValue(value, "endOdometer", "end_odo_meter")),
    responsibilityCode: asString(getValue(value, "responsibilityCode", "bas_responsibility_code")),
    objectiveCode: asString(getValue(value, "objectiveCode", "bas_object_code")),
    startLocation: asString(getValue(value, "startLocation", "start_route_location_name")),
    endLocation: asString(getValue(value, "endLocation", "end_route_location_name")),
    estimatedDistance: asNumber(getValue(value, "estimatedDistance", "estimated_distance")),
    distance: asNumber(getValue(value, "distance")),
    projectNumber: asString(
      getValue(value, "projectNumber", "project_number", "bas_project_number"),
    ),
    fundCode: asString(getValue(value, "fundCode", "fund_code", "bas_fund_code")),
    editedByUserCode: asNumber(getValue(value, "editedByUserCode", "modified_by_user_code")),
  };
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new TripAuthorityApiError("unauthorized", "No FIS access cookie is available.");
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
      throw new TripAuthorityApiError("unauthorized", "The FIS access cookie was rejected.");
    }
    if (response.status === 404) {
      throw new TripAuthorityApiError("not-found", "The requested trip authority was not found.");
    }
    if (response.status === 400 || response.status === 409) {
      throw new TripAuthorityApiError("rejected", await response.text());
    }

    if (!response.ok) {
      throw new TripAuthorityApiError("unavailable", `FIS API returned HTTP ${response.status}.`);
    }

    try {
      return (await response.json()) as unknown;
    } catch {
      throw new TripAuthorityApiError("invalid-response", "The FIS API returned invalid JSON.");
    }
  } catch (error) {
    if (error instanceof TripAuthorityApiError) throw error;
    throw new TripAuthorityApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

export async function getTripAuthorityVehicles() {
  const payload = await requestApi("api/Trip/vehicles");
  return getCollection(payload)
    .map(mapVehicle)
    .filter((vehicle): vehicle is TripAuthorityVehicle => vehicle !== null);
}

export async function getTripAuthorities() {
  const payload = await requestApi("api/Trip");
  return getCollection(payload)
    .map(mapTrip)
    .filter((trip): trip is TripAuthorityRecord => trip !== null);
}

export async function getTripAuthorityDetails(tripId: number) {
  const payload = await requestApi(`api/Trip/${tripId}/details`);
  if (!isRecord(payload)) {
    throw new TripAuthorityApiError(
      "invalid-response",
      "The FIS API returned an invalid trip authority detail response.",
    );
  }

  const trip = mapTrip(getValue(payload, "trip"));
  if (trip === null) {
    throw new TripAuthorityApiError(
      "invalid-response",
      "The FIS API returned an invalid trip authority.",
    );
  }

  const mapDetailCollection = <T>(key: string, mapper: (value: unknown) => T | null) => {
    const collection = getValue(payload, key);
    return Array.isArray(collection)
      ? collection.map(mapper).filter((item): item is T => item !== null)
      : [];
  };

  return {
    trip,
    drivers: mapDetailCollection("drivers", mapDriver),
    passengers: mapDetailCollection("passengers", mapPassenger),
    routes: mapDetailCollection("routes", mapRoute),
  } satisfies TripAuthorityDetails;
}

export async function closeTripAuthority(tripId: number, request: CloseTripAuthorityRequest) {
  const payload = await requestApi(`api/Trip/${encodeURIComponent(tripId)}/close`, {
    method: "POST",
    body: JSON.stringify({
      EndOdometer: request.endOdometer,
      Routes: request.routes.map((route) => ({
        RouteCode: route.routeCode,
        EndOdometer: route.endOdometer,
      })),
    }),
  });

  const trip = mapTrip(payload);
  if (!trip) {
    throw new TripAuthorityApiError(
      "invalid-response",
      "The FIS API returned an invalid closed trip authority.",
    );
  }

  return trip;
}

export async function createTripAuthority(request: CreateTripAuthorityRequest) {
  const payload = await requestApi("api/Trip/with-details", {
    method: "POST",
    body: JSON.stringify({
      ContractCode: request.contractCode,
      ApproverName: request.approverName,
      ApproverRank: request.approverRank,
      ApproverTelephone: request.approverTelephone,
      ExpiryDate: request.expiryDate,
      TripReason: request.tripReason,
      TripRequestNumber: request.tripRequestNumber,
      TripTypeCode: request.tripTypeCode,
      TripIncidentTypeCode: request.tripIncidentTypeCode,
      UserAccessCode: request.userAccessCode,
      TripIsMonthly: request.tripIsMonthly,
      Drivers: request.drivers.map((driver) => ({
        Name: driver.name,
        IdentityNumber: driver.identityNumber,
        IsPrimary: driver.isPrimary,
        SiteCode: driver.siteCode,
        LicenceTypeCode: driver.licenceTypeCode,
        PassportNumber: driver.passportNumber,
        PersalNumber: driver.persalNumber,
        ContractNumber: driver.contractNumber,
        LicenceNumber: driver.licenceNumber,
        LicenceIssueDate: driver.licenceIssueDate,
        LicenceLastVerifiedDate: driver.licenceLastVerifiedDate,
        HasPdp: driver.hasPdp,
        PdpExpiryDate: driver.pdpExpiryDate,
        LicenceExpiryDate: driver.licenceExpiryDate,
        IsActive: driver.isActive,
      })),
      Passengers: request.passengers.map((passenger) => ({ Name: passenger.name })),
      Routes: request.routes.map((route) => ({
        StartDate: route.startDate,
        EndDate: route.endDate,
        StartLocation: route.startLocation,
        EndLocation: route.endLocation,
        EstimatedDistance: route.estimatedDistance,
        ResponsibilityCode: route.responsibilityCode,
        ObjectiveCode: route.objectiveCode,
        ProjectNumber: route.projectNumber,
        FundCode: route.fundCode,
      })),
    }),
  });

  const trip = mapTrip(payload);
  if (!trip) {
    throw new TripAuthorityApiError(
      "invalid-response",
      "The FIS API returned an invalid created trip authority.",
    );
  }

  return trip;
}
