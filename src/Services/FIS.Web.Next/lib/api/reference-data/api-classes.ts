import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type ClassRecord = {
  classCode: number;
  description: string | null;
  classNumber: string | null;
  bankNumber: string | null;
  monthsLife: number | null;
  depreciationPercent: number | null;
  odometerLife: number | null;
  appreciatePercent: number | null;
  replacementCost: number | null;
  dateCreated: string | null;
  dateUpdated: string | null;
  createdByUserCode: number | null;
  modifiedByUserCode: number | null;
  isDeleted: boolean;
};

export type ClassWriteInput = Omit<
  ClassRecord,
  | "classCode"
  | "dateCreated"
  | "dateUpdated"
  | "createdByUserCode"
  | "modifiedByUserCode"
  | "isDeleted"
>;

export type ClassDeleteCheck = {
  modelCount: number;
  vehicleCount: number;
  canDelete: boolean;
};

export type ClassApiErrorReason = "unauthorized" | "unavailable" | "invalid-response";

export class ClassApiError extends Error {
  constructor(
    public readonly reason: ClassApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "ClassApiError";
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
  if (typeof value === "string") return value.trim() || null;
  if (typeof value === "number" || typeof value === "bigint") return String(value);
  return null;
}

function asBoolean(value: unknown) {
  if (typeof value === "boolean") return value;
  if (typeof value === "string") return value.toLowerCase() === "true" || value === "1";
  if (typeof value === "number") return value !== 0;
  return false;
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) throw new ClassApiError("unauthorized", "No FIS access cookie is available.");

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
      throw new ClassApiError(
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
      throw new ClassApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
        response.status,
      );
    }
    return response;
  } catch (error) {
    if (error instanceof ClassApiError) throw error;
    throw new ClassApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new ClassApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapClass(value: unknown): ClassRecord | null {
  if (!isRecord(value)) return null;
  const classCode = asNumber(getValue(value, "class_code", "classCode"));
  if (classCode === null) return null;

  return {
    classCode,
    description: asString(getValue(value, "description", "Description")),
    classNumber: asString(getValue(value, "class_number", "classNumber")),
    bankNumber: asString(getValue(value, "bank_number", "bankNumber")),
    monthsLife: asNumber(getValue(value, "months_life", "monthsLife")),
    depreciationPercent: asNumber(getValue(value, "depreciation_percent", "depreciationPercent")),
    odometerLife: asNumber(getValue(value, "odometer_life", "odometerLife")),
    appreciatePercent: asNumber(getValue(value, "appreciate_percent", "appreciatePercent")),
    replacementCost: asNumber(getValue(value, "replacement_cost", "replacementCost")),
    dateCreated: asString(getValue(value, "date_created", "dateCreated")),
    dateUpdated: asString(getValue(value, "date_updated", "dateUpdated")),
    createdByUserCode: asNumber(getValue(value, "created_by_user_code", "createdByUserCode")),
    modifiedByUserCode: asNumber(getValue(value, "modified_by_user_code", "modifiedByUserCode")),
    isDeleted: asBoolean(getValue(value, "is_deleted", "isDeleted")),
  };
}

export async function getClasses() {
  const payload = await readJson(await requestApi("api/class"));
  if (!Array.isArray(payload))
    throw new ClassApiError("invalid-response", "The class response was not a list.");
  return payload.map(mapClass).filter((item): item is ClassRecord => item !== null);
}

export async function getClass(classCode: number) {
  return mapClass(await readJson(await requestApi(`api/class/${encodeURIComponent(classCode)}`)));
}

export async function getClassDeleteCheck(classCode: number): Promise<ClassDeleteCheck> {
  const payload = await readJson(
    await requestApi(`api/class/${encodeURIComponent(classCode)}/delete-check`),
  );
  if (!isRecord(payload))
    throw new ClassApiError("invalid-response", "The class dependency response was invalid.");
  return {
    modelCount: asNumber(getValue(payload, "modelCount", "ModelCount")) ?? 0,
    vehicleCount: asNumber(getValue(payload, "vehicleCount", "VehicleCount")) ?? 0,
    canDelete: asBoolean(getValue(payload, "canDelete", "CanDelete")),
  };
}

function toRequest(input: ClassWriteInput) {
  return {
    description: input.description,
    class_number: input.classNumber,
    bank_number: input.bankNumber,
    months_life: input.monthsLife,
    depreciation_percent: input.depreciationPercent,
    odometer_life: input.odometerLife,
    appreciate_percent: input.appreciatePercent,
    replacement_cost: input.replacementCost,
  };
}

export async function createClass(input: ClassWriteInput) {
  return mapClass(
    await readJson(
      await requestApi("api/class", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(toRequest(input)),
      }),
    ),
  );
}

export async function updateClass(classCode: number, input: ClassWriteInput) {
  return mapClass(
    await readJson(
      await requestApi(`api/class/${encodeURIComponent(classCode)}`, {
        method: "PUT",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ class_code: classCode, ...toRequest(input) }),
      }),
    ),
  );
}

export async function deleteClass(classCode: number) {
  await requestApi(`api/class/${encodeURIComponent(classCode)}`, { method: "DELETE" });
}
