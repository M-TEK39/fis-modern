import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
const AUTHORIZATION_BASE_PATH = "api/vehicle/authorization";
export const VEHICLE_AUTHORIZATION_PAGE_SIZE = 24;
type JsonRecord = Record<string, unknown>;

export type VehicleAuthorization = {
  tempVmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  chassisNumber: string;
  engineNumber: string | null;
  modelCode: number | null;
  modelDescription: string | null;
  colour: string | null;
  yearManufactured: number | null;
  locationCode: number | null;
  vehicleStatusCode: number | null;
  vehicleStatusDate: string | null;
  typeCode: number | null;
  vsCode: number | null;
  comment: string | null;
  purchaseAmount: number | null;
  purchaseDate: string | null;
  purchaseFrom: string | null;
  takeOnDate: string | null;
  takeOnOdo: number | null;
  replacedGgNumber: string | null;
  siteCode: number | null;
  invoiceNumber: string | null;
  gpNumber: string | null;
  fleetNotes: string | null;
  damageStatus: string | null;
  damagesComment: string | null;
  authorityStatus: string;
  authorizedByUserCode: number | null;
  authorizedByUserName: string | null;
  authorizationDate: string | null;
  rejectionReason: string | null;
  authorizationComment: string | null;
  vmfCode: number | null;
  dateCreated: string | null;
  createdByUserCode: number | null;
};

export type VehicleAuthorizationQueuePage = {
  data: VehicleAuthorization[];
  page: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
};

export type VehicleAuthorizationQueuePageRequests = {
  pendingPage?: number;
  rejectedPage?: number;
  authorizedPage?: number;
};

export type VehicleAuthorizationQueues = {
  awaiting: VehicleAuthorizationQueuePage;
  rejected: VehicleAuthorizationQueuePage;
  authorized: VehicleAuthorizationQueuePage;
};

export type VehicleAuthorizationMutation = {
  message: string;
};

export class VehicleAuthorizationApiError extends Error {
  constructor(
    public readonly reason: "unauthorized" | "unavailable" | "invalid-response",
    message: string,
  ) {
    super(message);
    this.name = "VehicleAuthorizationApiError";
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

function toVehicleAuthorization(value: unknown): VehicleAuthorization | null {
  if (!isRecord(value)) {
    return null;
  }

  const tempVmfCode = asNumber(getValue(value, "tempVmfCode", "temp_vmf_code"));
  if (tempVmfCode === null) {
    return null;
  }

  return {
    tempVmfCode,
    fleetNumber: asString(getValue(value, "fleetNumber", "fleet_number")),
    registrationNumber: asString(getValue(value, "registrationNumber", "registration_number")),
    chassisNumber: asString(getValue(value, "chassisNumber", "chassis_number")) ?? "",
    engineNumber: asString(getValue(value, "engineNumber", "engine_number")),
    modelCode: asNumber(getValue(value, "modelCode", "model_code")),
    modelDescription: asString(getValue(value, "modelDescription", "model_description")),
    colour: asString(getValue(value, "colour")),
    yearManufactured: asNumber(getValue(value, "yearManufactured", "year_manufactured")),
    locationCode: asNumber(getValue(value, "locationCode", "location_code")),
    vehicleStatusCode: asNumber(getValue(value, "vehicleStatusCode", "vehicle_status_code")),
    vehicleStatusDate: asString(getValue(value, "vehicleStatusDate", "vehicle_status_date")),
    typeCode: asNumber(getValue(value, "typeCode", "type_code")),
    vsCode: asNumber(getValue(value, "vsCode", "vs_code")),
    comment: asString(getValue(value, "comment")),
    purchaseAmount: asNumber(getValue(value, "purchaseAmount", "purchase_amount")),
    purchaseDate: asString(getValue(value, "purchaseDate", "purchase_date")),
    purchaseFrom: asString(getValue(value, "purchaseFrom", "purchase_from")),
    takeOnDate: asString(getValue(value, "takeOnDate", "take_on_date")),
    takeOnOdo: asNumber(getValue(value, "takeOnOdo", "take_on_odo")),
    replacedGgNumber: asString(
      getValue(value, "replacedGGNumber", "replacedGgNumber", "replaced_gg_number"),
    ),
    siteCode: asNumber(getValue(value, "siteCode", "site_code")),
    invoiceNumber: asString(getValue(value, "invoiceNumber", "invoice_number")),
    gpNumber: asString(getValue(value, "gpNumber", "gp_number")),
    fleetNotes: asString(getValue(value, "fleetNotes", "Fleet_Notes", "fleet_notes")),
    damageStatus: asString(getValue(value, "damageStatus", "damage_status")),
    damagesComment: asString(getValue(value, "damagesComment", "damages_comment")),
    authorityStatus:
      asString(getValue(value, "authorityStatus", "authority_status", "Authority_Status")) ?? "",
    authorizedByUserCode: asNumber(
      getValue(value, "authorizedByUserCode", "authorized_by_user_code"),
    ),
    authorizedByUserName: asString(
      getValue(value, "authorizedByUserName", "authorized_by_user_name"),
    ),
    authorizationDate: asString(getValue(value, "authorizationDate", "authorization_date")),
    rejectionReason: asString(getValue(value, "rejectionReason", "rejection_reason")),
    authorizationComment: asString(
      getValue(value, "authorizationComment", "authorization_comment"),
    ),
    vmfCode: asNumber(getValue(value, "vmfCode", "vmf_code")),
    dateCreated: asString(getValue(value, "dateCreated", "date_created")),
    createdByUserCode: asNumber(getValue(value, "createdByUserCode", "created_by_user_code")),
  };
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new VehicleAuthorizationApiError("unauthorized", "No FIS access cookie is available.");
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
      throw new VehicleAuthorizationApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (!response.ok) {
      let message = `FIS API returned HTTP ${response.status}.`;
      try {
        const payload = (await response.json()) as unknown;
        if (isRecord(payload)) {
          message = asString(getValue(payload, "message", "error")) ?? message;
        } else if (typeof payload === "string" && payload.trim()) {
          message = payload.trim();
        }
      } catch {
        // Keep the status-based message when the API does not return JSON.
      }

      throw new VehicleAuthorizationApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
      );
    }

    try {
      return (await response.json()) as unknown;
    } catch {
      throw new VehicleAuthorizationApiError(
        "invalid-response",
        "The FIS API returned invalid JSON.",
      );
    }
  } catch (error) {
    if (error instanceof VehicleAuthorizationApiError) {
      throw error;
    }

    throw new VehicleAuthorizationApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

function requiredInteger(record: JsonRecord, key: string, minimum: number, label: string) {
  const value = asNumber(record[key]);
  if (value === null || !Number.isSafeInteger(value) || value < minimum) {
    throw new VehicleAuthorizationApiError(
      "invalid-response",
      `The FIS API returned an invalid ${label}.`,
    );
  }

  return value;
}

function sortQueue(path: "pending" | "rejected" | "authorized", vehicles: VehicleAuthorization[]) {
  if (path === "pending") {
    return vehicles.toSorted((left, right) =>
      left.chassisNumber.localeCompare(right.chassisNumber),
    );
  }

  return vehicles.toSorted(
    (left, right) => dateValue(right.authorizationDate) - dateValue(left.authorizationDate),
  );
}

function mapAuthorizationQueuePage(
  payload: unknown,
  requestedPage: number,
): VehicleAuthorizationQueuePage {
  if (!isRecord(payload) || !Array.isArray(payload.data)) {
    throw new VehicleAuthorizationApiError(
      "invalid-response",
      "The FIS API returned an invalid vehicle authorization page.",
    );
  }

  const page = requiredInteger(payload, "page", 1, "authorization page");
  const pageSize = requiredInteger(payload, "pageSize", 1, "authorization page size");
  const totalRecords = requiredInteger(payload, "totalRecords", 0, "authorization record count");
  const totalPages = requiredInteger(payload, "totalPages", 0, "authorization page count");
  const data = payload.data
    .map(toVehicleAuthorization)
    .filter((vehicle): vehicle is VehicleAuthorization => vehicle !== null);

  return {
    data,
    page: page || requestedPage,
    pageSize,
    totalRecords,
    totalPages: Math.max(1, totalPages),
  };
}

function dateValue(value: string | null) {
  if (!value) {
    return 0;
  }

  const timestamp = Date.parse(value);
  return Number.isNaN(timestamp) ? 0 : timestamp;
}

async function getAuthorizationQueue(
  path: "pending" | "rejected" | "authorized",
  page: number,
): Promise<VehicleAuthorizationQueuePage> {
  const requestedPage = Number.isSafeInteger(page) && page > 0 ? page : 1;
  const query = new URLSearchParams({
    page: String(requestedPage),
    pageSize: String(VEHICLE_AUTHORIZATION_PAGE_SIZE),
  });
  const payload = await requestApi(`${AUTHORIZATION_BASE_PATH}/${path}?${query.toString()}`);
  const result = mapAuthorizationQueuePage(payload, requestedPage);

  return {
    ...result,
    data: sortQueue(path, result.data),
  };
}

export async function getVehicleAuthorizationQueues(
  pages: VehicleAuthorizationQueuePageRequests = {},
): Promise<VehicleAuthorizationQueues> {
  const [awaiting, rejected, authorized] = await Promise.all([
    getAuthorizationQueue("pending", pages.pendingPage ?? 1),
    getAuthorizationQueue("rejected", pages.rejectedPage ?? 1),
    getAuthorizationQueue("authorized", pages.authorizedPage ?? 1),
  ]);

  return { awaiting, rejected, authorized };
}

async function postAuthorizationAction(
  path: string,
  body: JsonRecord,
): Promise<VehicleAuthorizationMutation> {
  const payload = await requestApi(`${AUTHORIZATION_BASE_PATH}/${path}`, {
    method: "POST",
    body: JSON.stringify(body),
  });

  const message = isRecord(payload) ? asString(getValue(payload, "message")) : null;
  return { message: message ?? "Vehicle authorization action completed successfully." };
}

export function approveVehicleAuthorization(id: number, comment: string) {
  return postAuthorizationAction(`${id}/approve`, { comment });
}

export function rejectVehicleAuthorization(id: number, rejectionReason: string, comment: string) {
  return postAuthorizationAction(`${id}/reject`, { rejectionReason, comment });
}

export function addVehicleAuthorizationComment(id: number, comment: string) {
  return postAuthorizationAction(`${id}/comment`, { comment });
}
