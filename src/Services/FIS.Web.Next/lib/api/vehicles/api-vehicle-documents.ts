import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;

type JsonRecord = Record<string, unknown>;

export const VEHICLE_DOCUMENT_CATEGORIES = [
  "Accident",
  "Contract",
  "Fine",
  "Insurance",
  "Licence",
  "Logbook",
  "Maintenance",
  "Other",
  "Registration",
  "RoadWorthy",
] as const;

export type VehicleDocumentCategory = (typeof VEHICLE_DOCUMENT_CATEGORIES)[number];

export type VehicleDocumentRecord = {
  documentId: number;
  category: string;
  description: string | null;
  fileName: string;
  mimeType: string;
  fileSizeBytes: number | null;
  referenceType: string | null;
  referenceId: number | null;
  dateCreated: string | null;
  isImage: boolean;
  isPdf: boolean;
};

export type VehicleDocumentApiErrorReason =
  "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class VehicleDocumentApiError extends Error {
  constructor(
    public readonly reason: VehicleDocumentApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "VehicleDocumentApiError";
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
    throw new VehicleDocumentApiError("unauthorized", "No FIS access cookie is available.");
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
        ...init.headers,
      },
      signal: controller.signal,
    });

    if (response.status === 401 || response.status === 403) {
      throw new VehicleDocumentApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (response.status === 404) {
      throw new VehicleDocumentApiError(
        "not-found",
        "The requested vehicle document was not found.",
      );
    }

    if (!response.ok) {
      let message = `FIS API returned HTTP ${response.status}.`;
      try {
        const payload = (await response.json()) as unknown;
        if (isRecord(payload)) {
          message = asString(getValue(payload, "message", "error")) || message;
        }
      } catch {
        // Keep the status-based message when the API has no JSON error body.
      }

      throw new VehicleDocumentApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
      );
    }

    return response;
  } catch (error) {
    if (error instanceof VehicleDocumentApiError) {
      throw error;
    }

    throw new VehicleDocumentApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new VehicleDocumentApiError(
      "invalid-response",
      "The FIS API returned invalid document JSON.",
    );
  }
}

function getCollection(payload: unknown) {
  if (Array.isArray(payload)) {
    return payload;
  }

  if (isRecord(payload)) {
    const documents = getValue(payload, "documents", "data", "items", "results");
    return Array.isArray(documents) ? documents : [];
  }

  return [];
}

function mapDocument(value: unknown): VehicleDocumentRecord | null {
  if (!isRecord(value)) {
    return null;
  }

  const documentId = asNumber(getValue(value, "document_id", "documentId", "id"));
  const fileName = asString(getValue(value, "original_file_name", "originalFileName", "fileName"));
  if (documentId === null || documentId <= 0 || !fileName) {
    return null;
  }

  const mimeType =
    asString(getValue(value, "mime_type", "mimeType", "contentType")) ?? "application/octet-stream";
  const fileSizeBytes =
    asNumber(getValue(value, "file_size_bytes", "fileSizeBytes")) ??
    (asNumber(getValue(value, "file_size_kb", "fileSizeKb")) ?? 0) * 1024;

  return {
    documentId,
    category:
      asString(getValue(value, "document_category", "documentCategory", "category")) ?? "Other",
    description: asString(
      getValue(value, "document_description", "documentDescription", "description"),
    ),
    fileName,
    mimeType,
    fileSizeBytes: fileSizeBytes > 0 ? fileSizeBytes : null,
    referenceType: asString(getValue(value, "reference_type", "referenceType")),
    referenceId: asNumber(getValue(value, "reference_id", "referenceId")),
    dateCreated: asString(getValue(value, "date_created", "dateCreated")),
    isImage: mimeType.toLowerCase().startsWith("image/"),
    isPdf: mimeType.toLowerCase() === "application/pdf",
  };
}

export async function getVehicleDocuments(vmfCode: number, category?: string) {
  const query = category ? `?category=${encodeURIComponent(category)}` : "";
  const response = await requestApi(
    `api/vehicles/${encodeURIComponent(vmfCode)}/documents${query}`,
  );
  const documents = getCollection(await readJson(response))
    .map(mapDocument)
    .filter((document): document is VehicleDocumentRecord => document !== null);

  return documents;
}

export async function uploadVehicleDocument(vmfCode: number, formData: FormData) {
  const response = await requestApi(`api/vehicles/${encodeURIComponent(vmfCode)}/documents`, {
    method: "POST",
    body: formData,
  });

  await readJson(response);
  return { ok: true as const };
}

export async function deleteVehicleDocument(vmfCode: number, documentId: number) {
  await requestApi(
    `api/vehicles/${encodeURIComponent(vmfCode)}/documents/${encodeURIComponent(documentId)}`,
    {
      method: "DELETE",
    },
  );

  return { ok: true as const };
}

export async function downloadVehicleDocument(vmfCode: number, documentId: number) {
  return requestApi(
    `api/vehicles/${encodeURIComponent(vmfCode)}/documents/${encodeURIComponent(documentId)}/download`,
  );
}
