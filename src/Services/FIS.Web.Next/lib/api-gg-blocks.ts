import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;

type JsonRecord = Record<string, unknown>;

export type GgBlockHistoryRecord = {
  blockId: number;
  capturedBy: string;
  dateCreated: string | null;
  startGgNumber: string;
  endGgNumber: string;
};

export type GgBlockHistoryPage = {
  items: GgBlockHistoryRecord[];
  page: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
};

export type GgBlockApiErrorReason = "unauthorized" | "unavailable" | "invalid-response" | "conflict";

export class GgBlockApiError extends Error {
  constructor(
    public readonly reason: GgBlockApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "GgBlockApiError";
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
    return value.trim();
  }

  if (typeof value === "number" || typeof value === "bigint") {
    return String(value);
  }

  return "";
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

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new GgBlockApiError("unauthorized", "No FIS access cookie is available.");
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
      throw new GgBlockApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (response.status === 409) {
      let message = "The requested GG block range overlaps an existing range.";
      try {
        const payload = (await response.json()) as unknown;
        if (isRecord(payload) && asString(getValue(payload, "message", "error"))) {
          message = asString(getValue(payload, "message", "error"));
        }
      } catch {
        // Keep the stable conflict message when the API has no JSON body.
      }

      throw new GgBlockApiError("conflict", message);
    }

    if (!response.ok) {
      throw new GgBlockApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        response.status >= 500 ? "The GG block service is unavailable." : `FIS API returned HTTP ${response.status}.`,
      );
    }

    return response;
  } catch (error) {
    if (error instanceof GgBlockApiError) {
      throw error;
    }

    throw new GgBlockApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

function mapHistoryRecord(value: unknown): GgBlockHistoryRecord | null {
  if (!isRecord(value)) {
    return null;
  }

  const capturedBy = asString(getValue(value, "capturedBy", "CapturedBy"));
  const blockId = asNumber(getValue(value, "blockId", "BlockId"));
  const startGgNumber = asString(getValue(value, "startGgNumber", "StartGgNumber"));
  const endGgNumber = asString(getValue(value, "endGgNumber", "EndGgNumber"));
  if (blockId === null || !capturedBy || !startGgNumber || !endGgNumber) {
    return null;
  }

  const rawDate = getValue(value, "dateCreated", "DateCreated");
  return {
    blockId: Math.trunc(blockId),
    capturedBy,
    dateCreated: typeof rawDate === "string" && rawDate.trim() ? rawDate : null,
    startGgNumber,
    endGgNumber,
  };
}

export async function getGgBlockHistory(page: number, pageSize: number): Promise<GgBlockHistoryPage> {
  const response = await requestApi(`api/vehicle-inception/gg-blocks?page=${page}&pageSize=${pageSize}`);
  let payload: unknown;
  try {
    payload = await response.json();
  } catch {
    throw new GgBlockApiError("invalid-response", "The FIS API returned invalid JSON.");
  }

  if (!isRecord(payload) || !Array.isArray(payload.items)) {
    throw new GgBlockApiError("invalid-response", "The FIS API returned an invalid GG block history response.");
  }

  const mappedItems = payload.items
    .map(mapHistoryRecord)
    .filter((item): item is GgBlockHistoryRecord => item !== null);
  const parsedPage = asNumber(getValue(payload, "page"));
  const parsedPageSize = asNumber(getValue(payload, "pageSize"));
  const totalRecords = asNumber(getValue(payload, "totalRecords"));
  const totalPages = asNumber(getValue(payload, "totalPages"));
  if (parsedPage === null || parsedPageSize === null || totalRecords === null || totalPages === null) {
    throw new GgBlockApiError("invalid-response", "The FIS API returned incomplete GG block history metadata.");
  }

  return {
    items: mappedItems,
    page: Math.max(1, Math.trunc(parsedPage)),
    pageSize: Math.max(1, Math.trunc(parsedPageSize)),
    totalRecords: Math.max(0, Math.trunc(totalRecords)),
    totalPages: Math.max(1, Math.trunc(totalPages)),
  };
}

export async function createGgBlock(startGgNumber: string, endGgNumber: string) {
  await requestApi("api/vehicle-inception/gg-blocks", {
    method: "POST",
    body: JSON.stringify({ startGgNumber, endGgNumber }),
  });
}
