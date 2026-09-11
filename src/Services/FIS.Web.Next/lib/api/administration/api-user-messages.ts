import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;

type JsonRecord = Record<string, unknown>;

export type UserMessage = {
  id: number;
  message: string;
  isRead: boolean;
  createdAt: string | null;
};

export type UserMessageInbox = {
  items: UserMessage[];
  unreadCount: number;
};

export type UserMessageApiErrorReason =
  "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class UserMessageApiError extends Error {
  constructor(
    public readonly reason: UserMessageApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "UserMessageApiError";
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
  if (typeof value === "string") return value;
  if (typeof value === "number" || typeof value === "bigint") return String(value);
  return "";
}

function asBoolean(value: unknown) {
  if (typeof value === "boolean") return value;
  if (typeof value === "number") return value !== 0;
  if (typeof value === "string") return ["true", "1", "y", "yes"].includes(value.toLowerCase());
  return false;
}

function asNullableString(value: unknown) {
  const result = asString(value).trim();
  return result || null;
}

function mapUserMessage(value: unknown): UserMessage | null {
  if (!isRecord(value)) return null;

  const id = asNumber(getValue(value, "id", "Id"));
  if (id === null || !Number.isSafeInteger(id) || id <= 0) return null;

  return {
    id,
    message: asString(getValue(value, "message", "Message")),
    isRead: asBoolean(getValue(value, "isRead", "IsRead")),
    createdAt: asNullableString(getValue(value, "createdAt", "CreatedAt")),
  };
}

function mapInbox(value: unknown): UserMessageInbox | null {
  if (!isRecord(value)) return null;

  const rawItems = getValue(value, "items", "Items");
  if (!Array.isArray(rawItems)) return null;

  const unreadCount = asNumber(getValue(value, "unreadCount", "UnreadCount"));
  if (unreadCount === null || unreadCount < 0 || !Number.isSafeInteger(unreadCount)) return null;

  return {
    items: rawItems.map(mapUserMessage).filter((item): item is UserMessage => item !== null),
    unreadCount,
  };
}

async function errorMessage(response: Response, fallback: string) {
  try {
    const payload = (await response.json()) as unknown;
    if (isRecord(payload)) {
      return asString(getValue(payload, "detail", "title", "message", "error")) || fallback;
    }
    return typeof payload === "string" && payload.trim() ? payload.trim() : fallback;
  } catch {
    return fallback;
  }
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new UserMessageApiError("unauthorized", "No FIS access cookie is available.");
  }

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);

  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers: {
        accept: "application/json",
        ...init.headers,
        cookie: cookieHeader,
      },
      signal: controller.signal,
    });

    if (response.status === 401 || response.status === 403) {
      throw new UserMessageApiError("unauthorized", "The FIS access cookie was rejected.");
    }
    if (response.status === 404) {
      throw new UserMessageApiError("not-found", "This notification is no longer available.");
    }
    if (!response.ok) {
      throw new UserMessageApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        await errorMessage(response, "The notification inbox is temporarily unavailable."),
      );
    }

    return response;
  } catch (error) {
    if (error instanceof UserMessageApiError) throw error;
    throw new UserMessageApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new UserMessageApiError(
      "invalid-response",
      "The FIS API returned invalid notification data.",
    );
  }
}

export async function getUserMessageInbox(limit = 24) {
  const boundedLimit = Math.max(1, Math.min(50, Math.floor(limit)));
  const payload = await readJson(await requestApi(`api/user-messages?limit=${boundedLimit}`));
  const inbox = mapInbox(payload);
  if (!inbox) {
    throw new UserMessageApiError(
      "invalid-response",
      "The FIS API returned an invalid notification inbox.",
    );
  }

  return inbox;
}

export async function markUserMessageRead(id: number) {
  const payload = await readJson(
    await requestApi(`api/user-messages/${id}/read`, { method: "PUT" }),
  );
  const message = mapUserMessage(payload);
  if (!message) {
    throw new UserMessageApiError(
      "invalid-response",
      "The FIS API returned an invalid updated notification.",
    );
  }

  return message;
}
