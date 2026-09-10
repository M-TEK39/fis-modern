import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type TrackingRecord = {
  trackCode: number;
  vmfCode: number | null;
  trackNumber: string | null;
  previousGg: string | null;
  followGg: string | null;
  installDate: string | null;
  removeDate: string | null;
  status: string | null;
  type: string | null;
  note: string | null;
  isDeleted: boolean;
  fleetNumber: string | null;
  registrationNumber: string | null;
};

export type TrackingWriteInput = {
  vmf_code: number | null;
  track_num: string | null;
  gg_previous: string | null;
  gg_follow: string | null;
  install_date: string | null;
  remove_date: string | null;
  track_status: string | null;
  track_type: string | null;
  track_note: string | null;
};

export type TrackingApiErrorReason =
  "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class TrackingApiError extends Error {
  constructor(
    public readonly reason: TrackingApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "TrackingApiError";
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
  for (const key of keys) if (key in record) return record[key];
  return undefined;
}

function asString(value: unknown) {
  if (typeof value === "string") return value.trim() || null;
  if (typeof value === "number" || typeof value === "bigint") return String(value);
  return null;
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
  return ["true", "1", "yes", "y"].includes(String(value).trim().toLowerCase());
}

function getCollection(payload: unknown) {
  if (Array.isArray(payload)) return payload;
  if (!isRecord(payload)) return [];
  const data = getValue(payload, "data", "Data", "items", "Items", "results", "Results");
  return Array.isArray(data) ? data : [];
}

function mapTracking(value: unknown): TrackingRecord | null {
  if (!isRecord(value)) return null;
  const trackCode = asNumber(getValue(value, "track_code", "trackCode"));
  if (trackCode === null) return null;
  const vehicle = getValue(value, "vehicle", "Vehicle");
  const vehicleRecord = isRecord(vehicle) ? vehicle : {};
  return {
    trackCode,
    vmfCode: asNumber(getValue(value, "vmf_code", "vmfCode")),
    trackNumber: asString(getValue(value, "track_num", "trackNum")),
    previousGg: asString(getValue(value, "gg_previous", "ggPrevious")),
    followGg: asString(getValue(value, "gg_follow", "ggFollow")),
    installDate: asString(getValue(value, "install_date", "installDate")),
    removeDate: asString(getValue(value, "remove_date", "removeDate")),
    status: asString(getValue(value, "track_status", "trackStatus")),
    type: asString(getValue(value, "track_type", "trackType")),
    note: asString(getValue(value, "track_note", "trackNote")),
    isDeleted: asBoolean(getValue(value, "is_deleted", "isDeleted")),
    fleetNumber: asString(getValue(vehicleRecord, "fleet_number", "fleetNumber")),
    registrationNumber: asString(
      getValue(vehicleRecord, "registration_number", "registrationNumber"),
    ),
  };
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader)
    throw new TrackingApiError("unauthorized", "No FIS access cookie is available.");

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers: { accept: "application/json", cookie: cookieHeader, ...init.headers },
      signal: controller.signal,
    });
    if (response.status === 401 || response.status === 403)
      throw new TrackingApiError(
        "unauthorized",
        "The FIS access cookie was rejected.",
        response.status,
      );
    if (response.status === 404)
      throw new TrackingApiError(
        "not-found",
        "The tracking record was not found.",
        response.status,
      );
    if (!response.ok)
      throw new TrackingApiError(
        "unavailable",
        `FIS API returned HTTP ${response.status}.`,
        response.status,
      );
    return response;
  } catch (error) {
    if (error instanceof TrackingApiError) throw error;
    throw new TrackingApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new TrackingApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

async function readTrackingList(path: string, init?: RequestInit) {
  const payload = await readJson(await requestApi(path, init));
  return getCollection(payload)
    .map(mapTracking)
    .filter((item): item is TrackingRecord => item !== null);
}

async function postReport(path: string, body: unknown) {
  return readTrackingList(path, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(body),
  });
}

export async function getTrackings() {
  return readTrackingList("api/tracking");
}

export async function getTracking(trackCode: number) {
  const payload = await readJson(await requestApi(`api/tracking/${encodeURIComponent(trackCode)}`));
  return mapTracking(payload);
}

export async function createTracking(input: TrackingWriteInput) {
  const payload = await readJson(
    await requestApi("api/tracking", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(input),
    }),
  );
  return mapTracking(payload);
}

export async function updateTracking(trackCode: number, input: TrackingWriteInput) {
  const payload = await readJson(
    await requestApi(`api/tracking/${encodeURIComponent(trackCode)}`, {
      method: "PUT",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ track_code: trackCode, ...input }),
    }),
  );
  return mapTracking(payload);
}

const reportRange = (startDate: string, endDate: string) => ({ startDate, endDate });

export function getTrackingOneVehicleReport(vmfCode: number, startDate: string, endDate: string) {
  return postReport("api/tracking/reports/one-vehicle", {
    vmfCode,
    ...reportRange(startDate, endDate),
  });
}

export function getTrackingOneDeviceReport(deviceId: string) {
  return postReport("api/tracking/reports/one-device", { deviceId });
}

export function getTrackingAllVehiclesReport(
  trackerType: string,
  startDate: string,
  endDate: string,
) {
  return postReport("api/tracking/reports/all-vehicles", {
    trackerType,
    ...reportRange(startDate, endDate),
  });
}

export function getTrackingAllDevicesReport(startDate: string, endDate: string) {
  return postReport("api/tracking/reports/all-devices", reportRange(startDate, endDate));
}

export function getTrackingInstallPeriodReport(startDate: string, endDate: string) {
  return postReport("api/tracking/reports/install-period", reportRange(startDate, endDate));
}

export function getTrackingSitePeriodReport(
  siteCode: number,
  allSites: boolean,
  startDate: string,
  endDate: string,
) {
  return postReport("api/tracking/reports/site-period", {
    siteCode,
    allSites,
    ...reportRange(startDate, endDate),
  });
}

export function getTrackingDeptPeriodReport(
  departmentCode: number,
  allDepartments: boolean,
  startDate: string,
  endDate: string,
) {
  return postReport("api/tracking/reports/dept-period", {
    departmentCode,
    allDepartments,
    ...reportRange(startDate, endDate),
  });
}
