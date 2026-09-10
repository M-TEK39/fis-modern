import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type BookingRecord = {
  bookingId: number;
  siteCode: string | null;
  name: string | null;
  startDate: string;
  endDate: string | null;
  classCode: number;
  userId: number;
  bookingDate: string;
  telephone: string | null;
  collected: number | null;
  locationCode: number;
  vmfCode: number | null;
  bookingStatus: string | null;
  notes: string | null;
  dateCreated: string | null;
  dateUpdated: string | null;
  createdByUserCode: number | null;
  modifiedByUserCode: number | null;
  isDeleted: boolean;
};

export type BookingRequest = {
  booking_id?: number;
  site_code: string | null;
  name: string | null;
  start_date: string;
  end_date: string | null;
  class_code: number;
  user_id: number;
  booking_date: string;
  telephone: string | null;
  collected: number | null;
  location_code: number;
  vmf_code: number | null;
  booking_status: string | null;
  notes: string | null;
};

export class BookingApiError extends Error {
  constructor(
    public readonly reason: "unauthorized" | "unavailable" | "invalid-response" | "not-found",
    message: string,
  ) {
    super(message);
    this.name = "BookingApiError";
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
  return value === null || value === undefined ? null : String(value);
}

function asNumber(value: unknown) {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  if (typeof value === "string" && value.trim()) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }

  return null;
}

function asBoolean(value: unknown) {
  if (typeof value === "boolean") return value;
  if (typeof value === "number") return value !== 0;
  return typeof value === "string" && ["true", "1", "y"].includes(value.toLowerCase());
}

function mapBooking(value: unknown): BookingRecord | null {
  if (!isRecord(value)) return null;

  const bookingId = asNumber(getValue(value, "booking_id", "bookingId", "BookingId"));
  const startDate = asString(getValue(value, "start_date", "startDate", "StartDate"));
  if (bookingId === null || startDate === null) return null;

  return {
    bookingId,
    siteCode: asString(getValue(value, "site_code", "siteCode", "SiteCode")),
    name: asString(getValue(value, "name", "Name")),
    startDate,
    endDate: asString(getValue(value, "end_date", "endDate", "EndDate")),
    classCode: asNumber(getValue(value, "class_code", "classCode", "ClassCode")) ?? 0,
    userId: asNumber(getValue(value, "user_id", "userId", "UserId")) ?? 0,
    bookingDate:
      asString(getValue(value, "booking_date", "bookingDate", "BookingDate")) ?? startDate,
    telephone: asString(getValue(value, "telephone", "Telephone")),
    collected: asNumber(getValue(value, "collected", "Collected")),
    locationCode: asNumber(getValue(value, "location_code", "locationCode", "LocationCode")) ?? 0,
    vmfCode: asNumber(getValue(value, "vmf_code", "vmfCode", "VmfCode")),
    bookingStatus: asString(getValue(value, "booking_status", "bookingStatus", "BookingStatus")),
    notes: asString(getValue(value, "notes", "Notes")),
    dateCreated: asString(getValue(value, "date_created", "dateCreated", "DateCreated")),
    dateUpdated: asString(getValue(value, "date_updated", "dateUpdated", "DateUpdated")),
    createdByUserCode: asNumber(
      getValue(value, "created_by_user_code", "createdByUserCode", "CreatedByUserCode"),
    ),
    modifiedByUserCode: asNumber(
      getValue(value, "modified_by_user_code", "modifiedByUserCode", "ModifiedByUserCode"),
    ),
    isDeleted: asBoolean(getValue(value, "is_deleted", "isDeleted", "IsDeleted")),
  };
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new BookingApiError("unauthorized", "No FIS access cookie is available.");
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
      throw new BookingApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (response.status === 404) {
      throw new BookingApiError("not-found", "The requested booking was not found.");
    }

    if (!response.ok) {
      throw new BookingApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        `FIS API returned HTTP ${response.status}.`,
      );
    }

    return response;
  } catch (error) {
    if (error instanceof BookingApiError) throw error;
    throw new BookingApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new BookingApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

export async function getBooking(bookingId: number) {
  try {
    const booking = mapBooking(
      await readJson(await requestApi(`api/Booking/${encodeURIComponent(bookingId)}`)),
    );
    if (!booking) {
      throw new BookingApiError(
        "invalid-response",
        "The FIS API returned an invalid booking record.",
      );
    }

    return booking;
  } catch (error) {
    if (error instanceof BookingApiError && error.reason === "not-found") return null;
    throw error;
  }
}

export async function createBooking(request: BookingRequest) {
  const booking = mapBooking(
    await readJson(
      await requestApi("api/Booking", {
        method: "POST",
        body: JSON.stringify(request),
      }),
    ),
  );
  if (!booking) {
    throw new BookingApiError(
      "invalid-response",
      "The FIS API returned an invalid created booking record.",
    );
  }

  return booking;
}

export async function updateBooking(bookingId: number, request: BookingRequest) {
  const booking = mapBooking(
    await readJson(
      await requestApi(`api/Booking/${encodeURIComponent(bookingId)}`, {
        method: "PUT",
        body: JSON.stringify({ ...request, booking_id: bookingId }),
      }),
    ),
  );
  if (!booking) {
    throw new BookingApiError(
      "invalid-response",
      "The FIS API returned an invalid updated booking record.",
    );
  }

  return booking;
}

export async function linkCallCentreBooking(callCentreCode: number, bookingId: number) {
  await requestApi(`api/CallCentre/${encodeURIComponent(callCentreCode)}/booking-link`, {
    method: "PUT",
    body: JSON.stringify({ BookingId: bookingId }),
  });
}
