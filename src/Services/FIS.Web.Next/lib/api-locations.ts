import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type LocationRecord = {
  locationId: number;
  locationName: string;
  description: string | null;
  contactPerson: string | null;
  phoneNumber: string | null;
  email: string | null;
  addressLine1: string | null;
  addressLine2: string | null;
  city: string | null;
  postalCode: string | null;
  province: string | null;
  country: string | null;
  latitude: number | null;
  longitude: number | null;
  isActive: boolean;
  createdDate: string | null;
  modifiedDate: string | null;
  dateCreated: string | null;
  dateUpdated: string | null;
};

export type LocationWriteInput = {
  locationName: string;
  address: string | null;
  latitude: number | null;
  longitude: number | null;
};

export type LocationApiErrorReason =
  "unauthorized" | "unavailable" | "invalid-response" | "not-found" | "conflict";

export class LocationApiError extends Error {
  constructor(
    public readonly reason: LocationApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "LocationApiError";
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

function asRequiredString(value: unknown) {
  return asString(value) ?? "";
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

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader)
    throw new LocationApiError("unauthorized", "No FIS access cookie is available.");

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
      throw new LocationApiError(
        "unauthorized",
        "The FIS access cookie was rejected.",
        response.status,
      );
    }
    if (response.status === 404) {
      throw new LocationApiError(
        "not-found",
        "The requested location was not found.",
        response.status,
      );
    }
    if (response.status === 409) {
      throw new LocationApiError(
        "conflict",
        "The location change conflicts with an existing record.",
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
        // Keep the status-based message when the API body is not JSON.
      }
      throw new LocationApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
        response.status,
      );
    }
    return response;
  } catch (error) {
    if (error instanceof LocationApiError) throw error;
    throw new LocationApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new LocationApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapLocation(value: unknown): LocationRecord | null {
  if (!isRecord(value)) return null;
  const locationId = asNumber(
    getValue(value, "locationId", "LocationId", "location_id", "locationCode", "location_code"),
  );
  const locationName = asString(
    getValue(value, "locationName", "LocationName", "location_name", "description"),
  );
  if (locationId === null || !locationName) return null;

  return {
    locationId,
    locationName,
    description: asString(getValue(value, "description", "Description")),
    contactPerson: asString(getValue(value, "contactPerson", "ContactPerson", "contact_person")),
    phoneNumber: asString(getValue(value, "phoneNumber", "PhoneNumber", "phone_number")),
    email: asString(getValue(value, "email", "Email")),
    addressLine1: asString(getValue(value, "addressLine1", "AddressLine1", "address_line1")),
    addressLine2: asString(getValue(value, "addressLine2", "AddressLine2", "address_line2")),
    city: asString(getValue(value, "city", "City")),
    postalCode: asString(getValue(value, "postalCode", "PostalCode", "postal_code")),
    province: asString(getValue(value, "province", "Province")),
    country: asString(getValue(value, "country", "Country")),
    latitude: asNumber(getValue(value, "latitude", "Latitude")),
    longitude: asNumber(getValue(value, "longitude", "Longitude")),
    isActive: asBoolean(getValue(value, "isActive", "IsActive", "is_active")),
    createdDate: asString(getValue(value, "createdDate", "CreatedDate", "created_date")),
    modifiedDate: asString(getValue(value, "modifiedDate", "ModifiedDate", "modified_date")),
    dateCreated: asString(getValue(value, "dateCreated", "DateCreated", "date_created")),
    dateUpdated: asString(getValue(value, "dateUpdated", "DateUpdated", "date_updated")),
  };
}

function toRequest(locationId: number, input: LocationWriteInput) {
  return {
    locationId,
    locationName: input.locationName,
    addressLine1: input.address,
    latitude: input.latitude,
    longitude: input.longitude,
    isActive: true,
    country: "South Africa",
  };
}

export async function getLocations() {
  const payload = await readJson(await requestApi("api/Location"));
  return getCollection(payload)
    .map(mapLocation)
    .filter((location): location is LocationRecord => location !== null);
}

export async function createLocation(input: LocationWriteInput) {
  const payload = await readJson(
    await requestApi("api/Location", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(toRequest(0, input)),
    }),
  );
  return mapLocation(payload);
}

export async function updateLocation(locationId: number, input: LocationWriteInput) {
  const payload = await readJson(
    await requestApi(`api/Location/${encodeURIComponent(locationId)}`, {
      method: "PUT",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(toRequest(locationId, input)),
    }),
  );
  return mapLocation(payload);
}

export async function deleteLocation(locationId: number) {
  await requestApi(`api/Location/${encodeURIComponent(locationId)}`, { method: "DELETE" });
}

export function mapLocationCollection(payload: unknown) {
  return getCollection(payload)
    .map(mapLocation)
    .filter((location): location is LocationRecord => location !== null);
}

export function locationNameForDisplay(location: LocationRecord) {
  return asRequiredString(location.locationName) || `Location ${location.locationId}`;
}
