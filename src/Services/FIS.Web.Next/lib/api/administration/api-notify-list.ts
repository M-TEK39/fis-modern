import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type NotifyListRecord = {
  code: number;
  description: string | null;
  email: string | null;
};

export type NotifyListRequest = {
  Notify_list_desc: string;
  Notify_email1: string | null;
};

export class NotifyListApiError extends Error {
  constructor(
    public readonly reason: "unauthorized" | "unavailable" | "invalid-response" | "not-found",
    message: string,
  ) {
    super(message);
    this.name = "NotifyListApiError";
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

  return null;
}

function asNumber(value: unknown) {
  if (typeof value === "number" && Number.isInteger(value)) {
    return value;
  }

  if (typeof value === "string" && value.trim()) {
    const parsed = Number(value);
    return Number.isInteger(parsed) ? parsed : null;
  }

  return null;
}

function getCollection(payload: unknown) {
  if (Array.isArray(payload)) {
    return payload;
  }

  if (isRecord(payload)) {
    const value = getValue(payload, "data", "items", "results");
    return Array.isArray(value) ? value : [];
  }

  return [];
}

function mapNotifyList(value: unknown): NotifyListRecord | null {
  if (!isRecord(value)) {
    return null;
  }

  const code = asNumber(getValue(value, "Notify_list_code", "notify_list_code", "code"));
  if (code === null || code <= 0) {
    return null;
  }

  return {
    code,
    description: asString(getValue(value, "Notify_list_desc", "notify_list_desc", "description")),
    email: asString(getValue(value, "Notify_email1", "notify_email1", "email")),
  };
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new NotifyListApiError("unauthorized", "No FIS access cookie is available.");
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
      throw new NotifyListApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (response.status === 404) {
      throw new NotifyListApiError("not-found", "The notification section was not found.");
    }

    if (!response.ok) {
      throw new NotifyListApiError("unavailable", `FIS API returned HTTP ${response.status}.`);
    }

    return response;
  } catch (error) {
    if (error instanceof NotifyListApiError) {
      throw error;
    }

    throw new NotifyListApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new NotifyListApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

export async function getNotifyLists(searchTerm = "") {
  const search = searchTerm.trim();
  const path = search ? `api/notifylist?search=${encodeURIComponent(search)}` : "api/notifylist";
  const payload = await readJson(await requestApi(path));

  return getCollection(payload)
    .map(mapNotifyList)
    .filter((item): item is NotifyListRecord => item !== null)
    .sort(
      (left, right) =>
        (left.description ?? "").localeCompare(right.description ?? "") || left.code - right.code,
    );
}

export async function getNotifyList(code: number) {
  let response: Response;
  try {
    response = await requestApi(`api/notifylist/${encodeURIComponent(code)}`);
  } catch (error) {
    if (error instanceof NotifyListApiError && error.reason === "not-found") {
      return null;
    }

    throw error;
  }

  const payload = await readJson(response);
  const item = mapNotifyList(payload);
  if (!item) {
    throw new NotifyListApiError(
      "invalid-response",
      "The FIS API returned an invalid notification section.",
    );
  }

  return item;
}

async function mutateNotifyList(path: string, init: RequestInit) {
  const payload = await readJson(await requestApi(path, init));
  const item = mapNotifyList(payload);
  if (!item) {
    throw new NotifyListApiError(
      "invalid-response",
      "The FIS API returned an invalid notification section.",
    );
  }

  return item;
}

export function createNotifyList(request: NotifyListRequest) {
  return mutateNotifyList("api/notifylist", {
    method: "POST",
    body: JSON.stringify(request),
  });
}

export function updateNotifyList(code: number, request: NotifyListRequest) {
  return mutateNotifyList(`api/notifylist/${encodeURIComponent(code)}`, {
    method: "PUT",
    body: JSON.stringify(request),
  });
}

export async function deleteNotifyList(code: number) {
  await requestApi(`api/notifylist/${encodeURIComponent(code)}`, { method: "DELETE" });
}
