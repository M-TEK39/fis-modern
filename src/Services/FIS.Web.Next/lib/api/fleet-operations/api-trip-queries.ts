import "server-only";

import { FinanceApiError, getFinanceJson } from "@/lib/api/finance/api-finance";

export type TripQueryRow = {
  key: string;
  vehicle: string;
  department: string;
  tripCount: number;
  kilometres: number;
  firstTrip: string | null;
  lastTrip: string | null;
};

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function getValue(record: Record<string, unknown>, ...keys: string[]) {
  for (const key of keys) if (key in record) return record[key];
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
  return typeof value === "string" && value.trim() ? value.trim() : null;
}

function rows(value: unknown) {
  return Array.isArray(value) ? value.filter(isRecord) : [];
}

function mapSummaryLine(value: Record<string, unknown>, index: number): TripQueryRow {
  const tripId = asNumber(getValue(value, "tripId", "TripId"));
  const vmfCode = asNumber(getValue(value, "vmfCode", "VmfCode"));
  const tripCount = asNumber(getValue(value, "tripCount", "TripCount")) ?? 0;
  return {
    key:
      tripId && tripId > 0
        ? String(tripId)
        : vmfCode && vmfCode > 0
          ? `VMF ${vmfCode}`
          : `Summary ${index + 1}`,
    vehicle:
      asString(
        getValue(
          value,
          "vehicleRegistration",
          "VehicleRegistration",
          "registrationNumber",
          "RegistrationNumber",
        ),
      ) ?? (vmfCode ? `VMF ${vmfCode}` : "Unknown"),
    department: asString(getValue(value, "department", "Department")) ?? "-",
    tripCount,
    kilometres:
      asNumber(getValue(value, "totalKilometers", "TotalKilometers", "distance", "Distance")) ?? 0,
    firstTrip: asString(getValue(value, "firstTrip", "FirstTrip", "date", "Date")),
    lastTrip: asString(getValue(value, "lastTrip", "LastTrip", "date", "Date")),
  };
}

function mapLegacySummary(value: Record<string, unknown>, index: number): TripQueryRow {
  const tripId = asNumber(getValue(value, "tripId", "TripId"));
  const vmfCode = asNumber(getValue(value, "vmfCode", "VmfCode"));
  const date = asString(getValue(value, "tripDate", "TripDate", "date", "Date"));
  return {
    key: tripId && tripId > 0 ? String(tripId) : `Summary ${index + 1}`,
    vehicle:
      asString(getValue(value, "vehicleRegistration", "VehicleRegistration")) ??
      (vmfCode ? `VMF ${vmfCode}` : "Unknown"),
    department: "-",
    tripCount: 1,
    kilometres: asNumber(getValue(value, "distance", "Distance")) ?? 0,
    firstTrip: date,
    lastTrip: date,
  };
}

export async function getTripQueryRows() {
  const startDate = new Date(Date.now() - 90 * 24 * 60 * 60 * 1000).toISOString();
  const endDate = new Date().toISOString();
  const query = new URLSearchParams({ startDate, endDate });
  const payload = await getFinanceJson(`api/report/trip/summary?${query.toString()}`);
  if (Array.isArray(payload)) return payload.filter(isRecord).map(mapLegacySummary);
  if (!isRecord(payload))
    throw new FinanceApiError("invalid-response", "The FIS API returned an invalid trip summary.");

  const tripLines = rows(getValue(payload, "tripLines", "TripLines"));
  if (tripLines.length > 0) return tripLines.map(mapSummaryLine);
  const summaries = rows(getValue(payload, "tripSummaries", "TripSummaries"));
  return summaries.map(mapLegacySummary);
}
