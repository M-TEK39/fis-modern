import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
const AUTHORIZATION_BASE_PATH = "api/vehicle/authorization";
type JsonRecord = Record<string, unknown>;

export type VehicleAuthorization = {
  tempVmfCode: number;
  chassisNumber: string;
  engineNumber: string | null;
  modelCode: number | null;
  modelDescription: string | null;
  colour: string | null;
  purchaseAmount: number | null;
  purchaseDate: string | null;
  purchaseFrom: string | null;
  takeOnDate: string | null;
  takeOnOdo: number | null;
  replacedGgNumber: string | null;
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

export type VehicleAuthorizationQueues = {
  awaiting: VehicleAuthorization[];
  rejected: VehicleAuthorization[];
  authorized: VehicleAuthorization[];
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

function getCollection(payload: unknown) {
  if (Array.isArray(payload)) {
    return payload;
  }

  if (isRecord(payload)) {
    const data = getValue(payload, "data", "items", "results");
    return Array.isArray(data) ? data : [];
  }

  return [];
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
    chassisNumber: asString(getValue(value, "chassisNumber", "chassis_number")) ?? "",
    engineNumber: asString(getValue(value, "engineNumber", "engine_number")),
    modelCode: asNumber(getValue(value, "modelCode", "model_code")),
    modelDescription: asString(getValue(value, "modelDescription", "model_description")),
    colour: asString(getValue(value, "colour")),
    purchaseAmount: asNumber(getValue(value, "purchaseAmount", "purchase_amount")),
    purchaseDate: asString(getValue(value, "purchaseDate", "purchase_date")),
    purchaseFrom: asString(getValue(value, "purchaseFrom", "purchase_from")),
    takeOnDate: asString(getValue(value, "takeOnDate", "take_on_date")),
    takeOnOdo: asNumber(getValue(value, "takeOnOdo", "take_on_odo")),
    replacedGgNumber: asString(getValue(value, "replacedGGNumber", "replacedGgNumber", "replaced_gg_number")),
    fleetNotes: asString(getValue(value, "fleetNotes", "Fleet_Notes", "fleet_notes")),
    damageStatus: asString(getValue(value, "damageStatus", "damage_status")),
    damagesComment: asString(getValue(value, "damagesComment", "damages_comment")),
    authorityStatus: asString(getValue(value, "authorityStatus", "authority_status", "Authority_Status")) ?? "",
    authorizedByUserCode: asNumber(getValue(value, "authorizedByUserCode", "authorized_by_user_code")),
    authorizedByUserName: asString(getValue(value, "authorizedByUserName", "authorized_by_user_name")),
    authorizationDate: asString(getValue(value, "authorizationDate", "authorization_date")),
    rejectionReason: asString(getValue(value, "rejectionReason", "rejection_reason")),
    authorizationComment: asString(getValue(value, "authorizationComment", "authorization_comment")),
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
      throw new VehicleAuthorizationApiError("invalid-response", "The FIS API returned invalid JSON.");
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

async function getAuthorizationQueue(path: string) {
  const payload = await requestApi(`${AUTHORIZATION_BASE_PATH}/${path}`);
  const vehicles = getCollection(payload)
    .map(toVehicleAuthorization)
    .filter((vehicle): vehicle is VehicleAuthorization => vehicle !== null);

  return vehicles;
}

function dateValue(value: string | null) {
  if (!value) {
    return 0;
  }

  const timestamp = Date.parse(value);
  return Number.isNaN(timestamp) ? 0 : timestamp;
}

export async function getVehicleAuthorizationQueues(): Promise<VehicleAuthorizationQueues> {
  const [awaiting, rejected, authorized] = await Promise.all([
    getAuthorizationQueue("pending"),
    getAuthorizationQueue("rejected"),
    getAuthorizationQueue("authorized"),
  ]);

  return {
    awaiting: awaiting.toSorted((left, right) => left.chassisNumber.localeCompare(right.chassisNumber)),
    rejected: rejected.toSorted((left, right) => dateValue(right.authorizationDate) - dateValue(left.authorizationDate)),
    authorized: authorized.toSorted((left, right) => dateValue(right.authorizationDate) - dateValue(left.authorizationDate)),
  };
}

async function postAuthorizationAction(path: string, body: JsonRecord): Promise<VehicleAuthorizationMutation> {
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
