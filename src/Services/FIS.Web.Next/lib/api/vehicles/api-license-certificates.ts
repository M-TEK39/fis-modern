import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
export const DEFAULT_LICENSE_CERTIFICATE_PAGE_SIZE = 24;
type JsonRecord = Record<string, unknown>;

export type CertificateVehicle = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
};

export type LicenseCertificateRecord = {
  source: "modern" | "legacy";
  documentKey: string;
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  image: string | null;
  fileName: string | null;
  mimeType: string;
  fileSizeBytes: number | null;
  periodBegin: string | null;
  periodEnd: string | null;
  dateCreated: string | null;
};

export type LicenseCertificatePage = {
  items: LicenseCertificateRecord[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
};

export type MissingCertificateVehicle = CertificateVehicle & {
  number: number;
  locationCode: number;
};

export type MissingLicenseCertificatePage = {
  location: string;
  vehicles: MissingCertificateVehicle[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
};

export type LicenseCertificateApiErrorReason =
  "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class LicenseCertificateApiError extends Error {
  constructor(
    public readonly reason: LicenseCertificateApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "LicenseCertificateApiError";
  }
}

function getApiBaseUrl() {
  const value = process.env.API_BASE_URL?.trim() || "http://localhost:5010";
  return `${value.replace(/\/$/, "")}/`;
}

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function valueOf(record: JsonRecord, ...keys: string[]) {
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

async function requestApi(path: string, init: RequestInit = {}) {
  const cookie = await getForwardedAuthCookieHeader();
  if (!cookie)
    throw new LicenseCertificateApiError("unauthorized", "No FIS access cookie is available.");
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers: { accept: "application/json", cookie, ...init.headers },
      signal: controller.signal,
    });
    if (response.status === 401 || response.status === 403)
      throw new LicenseCertificateApiError(
        "unauthorized",
        "The FIS access cookie was rejected.",
        response.status,
      );
    if (response.status === 404)
      throw new LicenseCertificateApiError(
        "not-found",
        "The selected licence certificate was not found.",
        response.status,
      );
    if (!response.ok) {
      let message = `FIS API returned HTTP ${response.status}.`;
      try {
        const payload = await response.clone().json();
        if (isRecord(payload))
          message = asString(valueOf(payload, "message", "Message", "error")) ?? message;
      } catch {
        // Keep the status message when the API has no JSON error body.
      }
      throw new LicenseCertificateApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
        response.status,
      );
    }
    return response;
  } catch (error) {
    if (error instanceof LicenseCertificateApiError) throw error;
    throw new LicenseCertificateApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new LicenseCertificateApiError(
      "invalid-response",
      "The FIS API returned invalid certificate JSON.",
    );
  }
}

function mapCertificate(value: unknown): LicenseCertificateRecord | null {
  if (!isRecord(value)) return null;
  const source = asString(valueOf(value, "source", "Source"))?.toLowerCase();
  const documentKey = asString(valueOf(value, "document_key", "documentKey", "id"));
  const vmfCode = asNumber(valueOf(value, "vmf_code", "vmfCode"));
  if ((source !== "modern" && source !== "legacy") || !documentKey || vmfCode === null) return null;
  return {
    source,
    documentKey,
    vmfCode,
    fleetNumber: asString(valueOf(value, "fleet_number", "fleetNumber")),
    registrationNumber: asString(valueOf(value, "registration_number", "registrationNumber")),
    image: asString(valueOf(value, "image")),
    fileName: asString(valueOf(value, "original_file_name", "originalFileName", "fileName")),
    mimeType: asString(valueOf(value, "mime_type", "mimeType")) ?? "application/octet-stream",
    fileSizeBytes: asNumber(valueOf(value, "file_size_bytes", "fileSizeBytes")),
    periodBegin: asString(valueOf(value, "period_begin", "periodBegin")),
    periodEnd: asString(valueOf(value, "period_end", "periodEnd")),
    dateCreated: asString(valueOf(value, "date_created", "dateCreated")),
  };
}

function mapVehicle(value: unknown): CertificateVehicle | null {
  if (!isRecord(value)) return null;
  const vmfCode = asNumber(valueOf(value, "vmf_code", "vmfCode"));
  if (vmfCode === null) return null;
  return {
    vmfCode,
    fleetNumber: asString(valueOf(value, "fleet_number", "fleetNumber")),
    registrationNumber: asString(valueOf(value, "registration_number", "registrationNumber")),
  };
}

function listPayload(payload: unknown, ...keys: string[]) {
  if (Array.isArray(payload)) return payload;
  if (isRecord(payload)) {
    const value = valueOf(payload, ...keys);
    return Array.isArray(value) ? value : [];
  }
  return [];
}

function readPageMetadata(payload: JsonRecord) {
  const page = asNumber(valueOf(payload, "page", "Page"));
  const pageSize = asNumber(valueOf(payload, "pageSize", "PageSize", "page_size"));
  const total = asNumber(valueOf(payload, "total", "Total"));
  const totalPages = asNumber(valueOf(payload, "totalPages", "TotalPages", "total_pages"));

  if (
    page === null ||
    pageSize === null ||
    total === null ||
    totalPages === null ||
    !Number.isInteger(page) ||
    !Number.isInteger(pageSize) ||
    !Number.isInteger(total) ||
    !Number.isInteger(totalPages) ||
    page < 1 ||
    pageSize < 1 ||
    total < 0 ||
    totalPages < 1
  )
    return null;

  return { page, pageSize, total, totalPages };
}

function normalizePage(value: number | undefined) {
  return Number.isInteger(value) && (value ?? 0) > 0 ? (value ?? 1) : 1;
}

function normalizePageSize(value: number | undefined) {
  const pageSize =
    Number.isInteger(value) && (value ?? 0) > 0
      ? (value ?? DEFAULT_LICENSE_CERTIFICATE_PAGE_SIZE)
      : DEFAULT_LICENSE_CERTIFICATE_PAGE_SIZE;
  return Math.min(100, pageSize);
}

function mapMissingCertificateVehicle(value: unknown): MissingCertificateVehicle | null {
  if (!isRecord(value)) return null;
  const vehicle = mapVehicle(value);
  const locationCode = asNumber(valueOf(value, "location_code", "locationCode"));
  const number = asNumber(valueOf(value, "number"));
  return vehicle && locationCode !== null && number !== null
    ? { ...vehicle, locationCode, number }
    : null;
}

function readLicenseCertificatePage(payload: unknown): LicenseCertificatePage {
  if (!isRecord(payload) || !Array.isArray(payload.items))
    throw new LicenseCertificateApiError(
      "invalid-response",
      "The FIS API returned an invalid licence certificate page.",
    );
  const metadata = readPageMetadata(payload);
  if (!metadata)
    throw new LicenseCertificateApiError(
      "invalid-response",
      "The FIS API returned incomplete licence certificate pagination metadata.",
    );
  return {
    items: payload.items
      .map(mapCertificate)
      .filter((item): item is LicenseCertificateRecord => item !== null),
    ...metadata,
  };
}

function readMissingLicenseCertificatePage(payload: unknown): MissingLicenseCertificatePage {
  if (!isRecord(payload) || !Array.isArray(payload.vehicles))
    throw new LicenseCertificateApiError(
      "invalid-response",
      "The FIS API returned an invalid missing-certificate page.",
    );
  const metadata = readPageMetadata(payload);
  if (!metadata)
    throw new LicenseCertificateApiError(
      "invalid-response",
      "The FIS API returned incomplete missing-certificate pagination metadata.",
    );
  return {
    location: asString(valueOf(payload, "location")) ?? "all",
    vehicles: payload.vehicles
      .map(mapMissingCertificateVehicle)
      .filter((vehicle): vehicle is MissingCertificateVehicle => vehicle !== null),
    ...metadata,
  };
}

export async function getLicenseCertificates() {
  const payload = await readJson(await requestApi("api/licence-certificates"));
  return listPayload(payload, "documents", "data", "items")
    .map(mapCertificate)
    .filter((item): item is LicenseCertificateRecord => item !== null);
}

export async function getLicenseCertificatesPage(
  options: { page?: number; pageSize?: number } = {},
): Promise<LicenseCertificatePage> {
  const params = new URLSearchParams({
    page: String(normalizePage(options.page)),
    pageSize: String(normalizePageSize(options.pageSize)),
  });
  return readLicenseCertificatePage(
    await readJson(await requestApi(`api/licence-certificates/page?${params.toString()}`)),
  );
}

export async function getLicenseCertificatesForVehicle(vmfCode: number) {
  const payload = await readJson(
    await requestApi(`api/licence-certificates/vehicle/${encodeURIComponent(vmfCode)}`),
  );
  return listPayload(payload, "documents", "data", "items")
    .map(mapCertificate)
    .filter((item): item is LicenseCertificateRecord => item !== null);
}

export async function searchLicenseCertificateVehicles(searchTerm: string) {
  const payload = await readJson(
    await requestApi(`api/vehicles/search?searchTerm=${encodeURIComponent(searchTerm)}`),
  );
  return listPayload(payload, "data", "items", "results")
    .map(mapVehicle)
    .filter((item): item is CertificateVehicle => item !== null);
}

export async function getVehiclesMissingLicenseCertificates(location?: "jhb" | "pta") {
  const query = location ? `?location=${location}` : "";
  const payload = await readJson(await requestApi(`api/licence-certificates/missing${query}`));
  return {
    location: isRecord(payload) ? (asString(valueOf(payload, "location")) ?? "all") : "all",
    vehicles: listPayload(payload, "vehicles", "data", "items").flatMap((value) => {
      const vehicle = mapMissingCertificateVehicle(value);
      return vehicle ? [vehicle] : [];
    }),
  };
}

export async function getVehiclesMissingLicenseCertificatesPage(
  options: { location?: "jhb" | "pta"; page?: number; pageSize?: number } = {},
): Promise<MissingLicenseCertificatePage> {
  const params = new URLSearchParams();
  if (options.location) params.set("location", options.location);
  params.set("page", String(normalizePage(options.page)));
  params.set("pageSize", String(normalizePageSize(options.pageSize)));
  return readMissingLicenseCertificatePage(
    await readJson(await requestApi(`api/licence-certificates/missing/page?${params.toString()}`)),
  );
}

export async function uploadLicenseCertificate(vmfCode: number, formData: FormData) {
  await requestApi("api/licence-certificates", { method: "POST", body: formData });
}

export async function deleteLicenseCertificate(
  source: string,
  documentKey: string,
  vmfCode: number,
) {
  await requestApi(
    `api/licence-certificates/${encodeURIComponent(source)}/${encodeURIComponent(documentKey)}?vmfCode=${encodeURIComponent(vmfCode)}`,
    { method: "DELETE" },
  );
}

export async function downloadLicenseCertificate(
  source: string,
  documentKey: string,
  vmfCode: number,
) {
  return requestApi(
    `api/licence-certificates/${encodeURIComponent(source)}/${encodeURIComponent(documentKey)}/file?vmfCode=${encodeURIComponent(vmfCode)}`,
  );
}
