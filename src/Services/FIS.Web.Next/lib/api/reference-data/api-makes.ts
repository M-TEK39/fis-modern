import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type MakeRecord = {
  makeCode: number;
  makeDescription: string;
  dateCreated: string | null;
  dateUpdated: string | null;
  createdByUserCode: number | null;
  modifiedByUserCode: number | null;
};

export type MakeDeleteCheck = {
  modelCount: number;
  canDelete: boolean;
};

export type MakeApiErrorReason = "unauthorized" | "unavailable" | "invalid-response";

export class MakeApiError extends Error {
  constructor(
    public readonly reason: MakeApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "MakeApiError";
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

function mapMake(value: unknown): MakeRecord | null {
  if (!isRecord(value)) return null;
  const makeCode = asNumber(getValue(value, "makeCode", "MakeCode", "make_code"));
  const makeDescription = asString(
    getValue(value, "makeDescription", "MakeDescription", "make_description"),
  );
  if (makeCode === null || makeDescription === null) return null;

  return {
    makeCode,
    makeDescription,
    dateCreated: asString(getValue(value, "dateCreated", "DateCreated", "date_created")),
    dateUpdated: asString(getValue(value, "dateUpdated", "DateUpdated", "date_updated")),
    createdByUserCode: asNumber(
      getValue(value, "createdByUserCode", "CreatedByUserCode", "created_by_user_code"),
    ),
    modifiedByUserCode: asNumber(
      getValue(value, "modifiedByUserCode", "ModifiedByUserCode", "modified_by_user_code"),
    ),
  };
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) throw new MakeApiError("unauthorized", "No FIS access cookie is available.");

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
      throw new MakeApiError(
        "unauthorized",
        "The FIS access cookie was rejected.",
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
      throw new MakeApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
        response.status,
      );
    }
    return response;
  } catch (error) {
    if (error instanceof MakeApiError) throw error;
    throw new MakeApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new MakeApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

export async function getMakes() {
  const payload = await readJson(await requestApi("api/make"));
  if (!Array.isArray(payload))
    throw new MakeApiError("invalid-response", "The make response was not a list.");
  return payload.map(mapMake).filter((make): make is MakeRecord => make !== null);
}

export async function getMake(makeCode: number) {
  return mapMake(await readJson(await requestApi(`api/make/${makeCode}`)));
}

export async function getMakeDeleteCheck(makeCode: number) {
  const payload = await readJson(await requestApi(`api/make/${makeCode}/delete-check`));
  if (!isRecord(payload))
    throw new MakeApiError("invalid-response", "The make dependency response was invalid.");
  return {
    modelCount: asNumber(getValue(payload, "modelCount", "ModelCount")) ?? 0,
    canDelete:
      getValue(payload, "canDelete", "CanDelete") === true ||
      String(getValue(payload, "canDelete", "CanDelete")).toLowerCase() === "true",
  } satisfies MakeDeleteCheck;
}

export async function createMake(makeDescription: string) {
  return mapMake(
    await readJson(
      await requestApi("api/make", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ make_description: makeDescription }),
      }),
    ),
  );
}

export async function updateMake(makeCode: number, makeDescription: string) {
  return mapMake(
    await readJson(
      await requestApi(`api/make/${makeCode}`, {
        method: "PUT",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ make_code: makeCode, make_description: makeDescription }),
      }),
    ),
  );
}

export async function deleteMake(makeCode: number) {
  await requestApi(`api/make/${makeCode}`, { method: "DELETE" });
}
