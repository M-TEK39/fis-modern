"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  BookingApiError,
  createBooking,
  linkCallCentreBooking,
  updateBooking,
  type BookingRecord,
  type BookingRequest,
} from "@/lib/api/fleet-operations/api-bookings";
import { getSession } from "@/lib/auth/session";

const BOOKING_PATH = "/call-centre/bookings";
const CALL_CENTRE_ROLE = "Call Centre";

function getText(formData: FormData, ...keys: string[]) {
  for (const key of keys) {
    const value = formData.get(key);
    if (typeof value === "string") return value.trim();
  }

  return "";
}

function redirectWithError(message: string, bookingId = "", sourceGmt = ""): never {
  const params = new URLSearchParams({ error: message });
  if (bookingId) params.set("bookingId", bookingId);
  if (sourceGmt) params.set("gmt", sourceGmt);
  redirect(`${BOOKING_PATH}?${params.toString()}`);
}

async function authorizeCallCentre(bookingId: string, sourceGmt: string) {
  const session = await getSession();
  if (session.status === "unavailable") {
    redirectWithError(
      "The sign-in service is temporarily unavailable. Please try again.",
      bookingId,
      sourceGmt,
    );
  }

  if (session.status !== "authenticated") {
    redirectWithError(
      "Your session has expired. Sign in again before continuing.",
      bookingId,
      sourceGmt,
    );
  }

  if (
    !session.roles.some(
      (role) => role.localeCompare(CALL_CENTRE_ROLE, undefined, { sensitivity: "accent" }) === 0,
    )
  ) {
    redirectWithError("You do not have permission to manage bookings.", bookingId, sourceGmt);
  }
}

function getOptionalNumber(formData: FormData, ...keys: string[]) {
  const value = getText(formData, ...keys);
  if (!value) return null;

  const parsed = Number(value);
  if (!Number.isInteger(parsed)) throw new Error("A numeric booking value is invalid.");
  return parsed;
}

function getRequiredNumber(formData: FormData, label: string, ...keys: string[]) {
  const value = getOptionalNumber(formData, ...keys);
  if (value === null) throw new Error(`${label} is required.`);
  return value;
}

function getBookingId(formData: FormData) {
  const raw = getText(formData, "booking_id", "bookingId");
  if (!raw) return null;

  const parsed = Number(raw);
  if (!Number.isInteger(parsed) || parsed <= 0 || parsed > 32_767) {
    throw new Error("Enter a valid booking ID.");
  }

  return parsed;
}

function getDateTime(formData: FormData, label: string, ...keys: string[]) {
  const value = getText(formData, ...keys);
  if (!value) throw new Error(`${label} is required.`);
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value) || Number.isNaN(Date.parse(value))) {
    throw new Error(`${label} must use a valid date and time.`);
  }

  return `${value}:00`;
}

function getOptionalDateTime(formData: FormData, label: string, ...keys: string[]) {
  const value = getText(formData, ...keys);
  if (!value) return null;
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value) || Number.isNaN(Date.parse(value))) {
    throw new Error(`${label} must use a valid date and time.`);
  }

  return `${value}:00`;
}

function getRequest(formData: FormData): BookingRequest {
  const request: BookingRequest = {
    site_code: getText(formData, "site_code") || null,
    name: getText(formData, "name") || null,
    start_date: getDateTime(formData, "Start date", "start_date"),
    end_date: getOptionalDateTime(formData, "End date", "end_date"),
    class_code: getRequiredNumber(formData, "Class code", "class_code"),
    user_id: getRequiredNumber(formData, "User ID", "user_id"),
    booking_date: getDateTime(formData, "Booking date", "booking_date"),
    telephone: getText(formData, "telephone") || null,
    collected: getOptionalNumber(formData, "collected"),
    location_code: getRequiredNumber(formData, "Location code", "location_code"),
    vmf_code: getOptionalNumber(formData, "vmf_code"),
    booking_status: getText(formData, "booking_status") || null,
    notes: getText(formData, "notes") || null,
  };

  if (request.class_code < 0 || request.class_code > 32_767)
    throw new Error("Class code is outside the legacy range.");
  if (request.user_id < 0 || request.user_id > 32_767)
    throw new Error("User ID is outside the legacy range.");
  if (request.collected !== null && (request.collected < 0 || request.collected > 32_767)) {
    throw new Error("Collected is outside the legacy range.");
  }
  if (request.location_code < 0 || request.location_code > 2_147_483_647)
    throw new Error("Location code is invalid.");
  if (request.vmf_code !== null && (request.vmf_code < 0 || request.vmf_code > 2_147_483_647)) {
    throw new Error("VMF code is invalid.");
  }

  return request;
}

function apiErrorMessage(error: unknown) {
  if (error instanceof BookingApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again before continuing.";
    if (error.reason === "not-found") return "The booking was not found.";
    if (error.reason === "unavailable")
      return "The booking service is temporarily unavailable. Please try again.";
  }

  return "The booking could not be saved. Please try again.";
}

export async function saveBookingAction(formData: FormData) {
  let bookingId = getText(formData, "booking_id", "bookingId");
  const sourceGmt = getText(formData, "sourceGmt");
  let saved: BookingRecord;
  let linkWarning = false;

  try {
    await authorizeCallCentre(bookingId, sourceGmt);
    const parsedBookingId = getBookingId(formData);
    const request = getRequest(formData);
    saved =
      parsedBookingId === null
        ? await createBooking(request)
        : await updateBooking(parsedBookingId, request);
    bookingId = String(saved.bookingId);

    if (parsedBookingId === null && sourceGmt) {
      const parsedGmt = Number(sourceGmt);
      if (Number.isInteger(parsedGmt) && parsedGmt > 0 && parsedGmt <= 32_767) {
        try {
          await linkCallCentreBooking(parsedGmt, saved.bookingId);
        } catch (error) {
          linkWarning = true;
          console.error("Booking was saved but the source GMT link could not be updated.", error);
        }
      }
    }
  } catch (error) {
    if (error instanceof Error && !(error instanceof BookingApiError)) {
      redirectWithError(error.message, bookingId, sourceGmt);
    }

    redirectWithError(apiErrorMessage(error), bookingId, sourceGmt);
  }

  revalidatePath(BOOKING_PATH);
  const params = new URLSearchParams({ bookingId, saved: "1" });
  if (sourceGmt) params.set("gmt", sourceGmt);
  if (linkWarning) params.set("linkWarning", "1");
  redirect(`${BOOKING_PATH}?${params.toString()}`);
}
