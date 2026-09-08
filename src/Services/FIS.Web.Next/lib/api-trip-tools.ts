import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type TripsWithoutRoutesRow = {
  tripAuthorityCode: number | null;
  contractCode: number | null;
  issueDate: string | null;
  tripReason: string | null;
  tripRequestNumber: string | null;
  approverName: string | null;
};

export type TripToolsApiErrorReason = "unauthorized" | "unavailable" | "invalid-response" | "rejected";

export class TripToolsApiError extends Error {
  constructor(
    public readonly reason: TripToolsApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "TripToolsApiError";
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

function getCollection(value: unknown) {
  if (Array.isArray(value)) return value;
  if (isRecord(value)) {
    const nested = getValue(value, "items", "data", "results");
    return Array.isArray(nested) ? nested : [];
  }

  return [];
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) throw new TripToolsApiError("unauthorized", "No FIS access cookie is available.");

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
      throw new TripToolsApiError("unauthorized", "The FIS access cookie was rejected.", response.status);
    }

    if (!response.ok) {
      let message = `FIS API returned HTTP ${response.status}.`;
      try {
        const payload = await response.clone().json();
        if (isRecord(payload)) message = asString(getValue(payload, "message", "Message", "error")) ?? message;
      } catch {
        // Keep the status-based message when the error body is not JSON.
      }

      throw new TripToolsApiError(response.status >= 500 ? "unavailable" : "rejected", message, response.status);
    }

    return response;
  } catch (error) {
    if (error instanceof TripToolsApiError) throw error;
    throw new TripToolsApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new TripToolsApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapRow(value: unknown): TripsWithoutRoutesRow | null {
  if (!isRecord(value)) return null;
  return {
    tripAuthorityCode: asNumber(getValue(value, "tripAuthorityCode", "TripAuthorityCode", "trip_authority_code")),
    contractCode: asNumber(getValue(value, "contractCode", "ContractCode", "contract_code")),
    issueDate: asString(getValue(value, "issueDate", "IssueDate", "issue_date")),
    tripReason: asString(getValue(value, "tripReason", "TripReason", "trip_reason")),
    tripRequestNumber: asString(getValue(value, "tripRequestNumber", "TripRequestNumber", "trip_request_number")),
    approverName: asString(getValue(value, "approverName", "ApproverName", "approver_name")),
  };
}

export async function getTripsWithoutRoutes() {
  const response = await requestApi("api/troubleshoot/trips-without-routes");
  return getCollection(await readJson(response)).map(mapRow).filter((row): row is TripsWithoutRoutesRow => row !== null);
}

export async function removeTripsWithoutRoutes() {
  const response = await requestApi("api/troubleshoot/remove-trips-no-routes", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({}),
  });
  const payload = await readJson(response);
  return isRecord(payload) ? asNumber(getValue(payload, "removed", "Removed")) ?? 0 : 0;
}
