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

export type TripQueryPage = {
  items: TripQueryRow[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
};

export const DEFAULT_TRIP_QUERY_PAGE_SIZE = 24;

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

function readPageMetadata(payload: Record<string, unknown>) {
  const page = asNumber(getValue(payload, "page", "Page"));
  const pageSize = asNumber(getValue(payload, "pageSize", "PageSize", "page_size"));
  const total = asNumber(getValue(payload, "total", "Total"));
  const totalPages = asNumber(getValue(payload, "totalPages", "TotalPages", "total_pages"));

  if (
    page === null ||
    pageSize === null ||
    total === null ||
    totalPages === null ||
    !Number.isSafeInteger(page) ||
    !Number.isSafeInteger(pageSize) ||
    !Number.isSafeInteger(total) ||
    !Number.isSafeInteger(totalPages) ||
    page < 1 ||
    pageSize < 1 ||
    pageSize > 100 ||
    total < 0 ||
    totalPages < 1
  ) {
    return null;
  }

  return { page, pageSize, total, totalPages };
}

function normalizePage(value: number | undefined) {
  return Number.isSafeInteger(value) && (value ?? 0) > 0 ? (value ?? 1) : 1;
}

function normalizePageSize(value: number | undefined) {
  const pageSize =
    Number.isSafeInteger(value) && (value ?? 0) > 0
      ? (value ?? DEFAULT_TRIP_QUERY_PAGE_SIZE)
      : DEFAULT_TRIP_QUERY_PAGE_SIZE;
  return Math.min(100, pageSize);
}

export async function getTripQueryPage(
  options: {
    page?: number;
    pageSize?: number;
    vmfCode?: number;
    search?: string;
    filter?: string;
  } = {},
): Promise<TripQueryPage> {
  const endDate = new Date();
  const startDate = new Date(endDate.getTime() - 90 * 24 * 60 * 60 * 1000);
  const query = new URLSearchParams({
    startDate: startDate.toISOString(),
    endDate: endDate.toISOString(),
    page: String(normalizePage(options.page)),
    pageSize: String(normalizePageSize(options.pageSize)),
  });
  if (options.vmfCode !== undefined) query.set("vmfCode", String(options.vmfCode));
  const search = options.search?.trim();
  if (search) query.set("search", search);
  const filter = options.filter?.trim();
  if (filter) query.set("filter", filter);

  const payload = await getFinanceJson(`api/report/trip/summary/page?${query.toString()}`);
  if (!isRecord(payload))
    throw new FinanceApiError("invalid-response", "The FIS API returned an invalid trip summary.");

  const items = getValue(payload, "items", "Items");
  if (!Array.isArray(items))
    throw new FinanceApiError(
      "invalid-response",
      "The FIS API returned an invalid trip summary page.",
    );

  const metadata = readPageMetadata(payload);
  if (!metadata)
    throw new FinanceApiError(
      "invalid-response",
      "The FIS API returned incomplete trip summary pagination metadata.",
    );

  return {
    items: items.filter(isRecord).map(mapSummaryLine),
    ...metadata,
  };
}
