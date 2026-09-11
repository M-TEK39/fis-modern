import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
export const DEFAULT_ASSET_VERIFICATION_PAGE_SIZE = 24;
type JsonRecord = Record<string, unknown>;

export type AssetVerificationRecord = {
  assetVerificationCode: number;
  province: string | null;
  departmentName: string | null;
  siteCode: number | null;
  siteName: string | null;
  responsibleManager: string | null;
  telNo: string | null;
  faxNo: string | null;
  vehicleRegNo: string | null;
  vmfCode: number | null;
  verificationDate: string | null;
  verifiedBy: number | null;
  verificationStatus: string | null;
  notes: string | null;
  vehicleMake: string | null;
  vehicleModel: string | null;
  vehicleColour: string | null;
  mobitrackFitted: string | null;
  petrolCard: string | null;
  lamination: string | null;
  tyreBands: string | null;
  barcode: string | null;
  logbook: string | null;
  gearlock: string | null;
  radio: string | null;
  carKeys: string | null;
  licenceExpiryDate: string | null;
  barcodeNumber: string | null;
  vehicleEngineNumber: string | null;
  vehicleChassisNumber: string | null;
  currentKm: number | null;
  dateLastVerified: string | null;
  comments: string | null;
  isDeleted: boolean;
};

export type AssetVerificationPage = {
  items: AssetVerificationRecord[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
};

export type AssetVerificationInput = Omit<
  AssetVerificationRecord,
  "assetVerificationCode" | "verificationStatus" | "isDeleted"
> & {
  assetVerificationCode?: number;
  verificationStatus?: string | null;
};

export type AssetVerificationApiErrorReason =
  "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class AssetVerificationApiError extends Error {
  constructor(
    public readonly reason: AssetVerificationApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "AssetVerificationApiError";
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

function getCollection(value: unknown) {
  if (Array.isArray(value)) return value;
  if (isRecord(value)) {
    const collection = getValue(value, "data", "items", "results");
    return Array.isArray(collection) ? collection : [];
  }
  return [];
}

function readPageMetadata(payload: JsonRecord) {
  const page = asNumber(getValue(payload, "page", "Page"));
  const pageSize = asNumber(getValue(payload, "pageSize", "PageSize", "page_size"));
  const total = asNumber(getValue(payload, "total", "Total"));
  const totalPages = asNumber(getValue(payload, "totalPages", "TotalPages", "total_pages"));

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
  ) {
    return null;
  }

  return { page, pageSize, total, totalPages };
}

function normalizePage(value: number | undefined) {
  return Number.isInteger(value) && (value ?? 0) > 0 ? (value ?? 1) : 1;
}

function normalizePageSize(value: number | undefined) {
  const pageSize =
    Number.isInteger(value) && (value ?? 0) > 0
      ? (value ?? DEFAULT_ASSET_VERIFICATION_PAGE_SIZE)
      : DEFAULT_ASSET_VERIFICATION_PAGE_SIZE;
  return Math.min(100, pageSize);
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader)
    throw new AssetVerificationApiError("unauthorized", "No FIS access cookie is available.");

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
    if (response.status === 401 || response.status === 403)
      throw new AssetVerificationApiError(
        "unauthorized",
        "The FIS access cookie was rejected.",
        response.status,
      );
    if (response.status === 404)
      throw new AssetVerificationApiError(
        "not-found",
        "The asset verification record was not found.",
        response.status,
      );
    if (!response.ok) {
      let message = `FIS API returned HTTP ${response.status}.`;
      try {
        const payload = await response.clone().json();
        if (isRecord(payload))
          message = asString(getValue(payload, "message", "Message", "error")) ?? message;
      } catch {
        // Keep the stable status message when the API has no JSON body.
      }
      throw new AssetVerificationApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
        response.status,
      );
    }
    return response;
  } catch (error) {
    if (error instanceof AssetVerificationApiError) throw error;
    throw new AssetVerificationApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new AssetVerificationApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapRecord(value: unknown): AssetVerificationRecord | null {
  if (!isRecord(value)) return null;
  const code = asNumber(getValue(value, "asset_verification_code", "assetVerificationCode"));
  if (code === null) return null;
  return {
    assetVerificationCode: code,
    province: asString(getValue(value, "province")),
    departmentName: asString(getValue(value, "department_name", "departmentName")),
    siteCode: asNumber(getValue(value, "site_code", "siteCode")),
    siteName: asString(getValue(value, "site_name", "siteName")),
    responsibleManager: asString(getValue(value, "responsible_manager", "responsibleManager")),
    telNo: asString(getValue(value, "tel_no", "telNo")),
    faxNo: asString(getValue(value, "fax_no", "faxNo")),
    vehicleRegNo: asString(getValue(value, "vehicle_reg_no", "vehicleRegNo")),
    vmfCode: asNumber(getValue(value, "vmf_code", "vmfCode")),
    verificationDate: asString(getValue(value, "verification_date", "verificationDate")),
    verifiedBy: asNumber(getValue(value, "verified_by", "verifiedBy")),
    verificationStatus: asString(getValue(value, "verification_status", "verificationStatus")),
    notes: asString(getValue(value, "notes")),
    vehicleMake: asString(getValue(value, "vehicle_make", "vehicleMake")),
    vehicleModel: asString(getValue(value, "vehicle_model", "vehicleModel")),
    vehicleColour: asString(getValue(value, "vehicle_colour", "vehicleColour")),
    mobitrackFitted: asString(getValue(value, "mobitrack_fitted", "mobitrackFitted")),
    petrolCard: asString(getValue(value, "petrol_card", "petrolCard")),
    lamination: asString(getValue(value, "lamination")),
    tyreBands: asString(getValue(value, "tyre_bands", "tyreBands")),
    barcode: asString(getValue(value, "barcode")),
    logbook: asString(getValue(value, "logbook")),
    gearlock: asString(getValue(value, "gearlock")),
    radio: asString(getValue(value, "radio")),
    carKeys: asString(getValue(value, "car_keys", "carKeys")),
    licenceExpiryDate: asString(getValue(value, "licence_expiry_date", "licenceExpiryDate")),
    barcodeNumber: asString(getValue(value, "barcode_number", "barcodeNumber")),
    vehicleEngineNumber: asString(getValue(value, "vehicle_engine_num", "vehicleEngineNumber")),
    vehicleChassisNumber: asString(getValue(value, "vehicle_chassis_num", "vehicleChassisNumber")),
    currentKm: asNumber(getValue(value, "current_km", "currentKm")),
    dateLastVerified: asString(getValue(value, "date_last_verified", "dateLastVerified")),
    comments: asString(getValue(value, "comments")),
    isDeleted: asBoolean(getValue(value, "is_deleted", "isDeleted")),
  };
}

function toApiInput(input: AssetVerificationInput) {
  return {
    asset_verification_code: input.assetVerificationCode,
    province: input.province,
    department_name: input.departmentName,
    site_code: input.siteCode,
    site_name: input.siteName,
    responsible_manager: input.responsibleManager,
    tel_no: input.telNo,
    fax_no: input.faxNo,
    vehicle_reg_no: input.vehicleRegNo,
    vmf_code: input.vmfCode,
    verification_date: input.verificationDate,
    verified_by: input.verifiedBy,
    verification_status: input.verificationStatus,
    notes: input.notes,
    vehicle_make: input.vehicleMake,
    vehicle_model: input.vehicleModel,
    vehicle_colour: input.vehicleColour,
    mobitrack_fitted: input.mobitrackFitted,
    petrol_card: input.petrolCard,
    lamination: input.lamination,
    tyre_bands: input.tyreBands,
    barcode: input.barcode,
    logbook: input.logbook,
    gearlock: input.gearlock,
    radio: input.radio,
    car_keys: input.carKeys,
    licence_expiry_date: input.licenceExpiryDate,
    barcode_number: input.barcodeNumber,
    vehicle_engine_num: input.vehicleEngineNumber,
    vehicle_chassis_num: input.vehicleChassisNumber,
    current_km: input.currentKm,
    date_last_verified: input.dateLastVerified,
    comments: input.comments,
  };
}

export async function getAssetVerifications() {
  const payload = await readJson(await requestApi("api/assetverification"));
  return getCollection(payload)
    .map(mapRecord)
    .filter((value): value is AssetVerificationRecord => value !== null);
}

export async function getAssetVerificationsForVehicle(vmfCode: number) {
  const payload = await readJson(
    await requestApi(`api/assetverification/vehicle/${encodeURIComponent(vmfCode)}`),
  );
  return getCollection(payload)
    .map(mapRecord)
    .filter((value): value is AssetVerificationRecord => value !== null);
}

function readAssetVerificationPage(payload: unknown): AssetVerificationPage {
  if (!isRecord(payload) || !Array.isArray(payload.items)) {
    throw new AssetVerificationApiError(
      "invalid-response",
      "The FIS API returned an invalid asset verification page.",
    );
  }

  const metadata = readPageMetadata(payload);
  if (!metadata) {
    throw new AssetVerificationApiError(
      "invalid-response",
      "The FIS API returned incomplete asset verification pagination metadata.",
    );
  }

  return {
    items: payload.items
      .map(mapRecord)
      .filter((value): value is AssetVerificationRecord => value !== null),
    ...metadata,
  };
}

export async function getAssetVerificationsPage(
  options: { page?: number; pageSize?: number } = {},
): Promise<AssetVerificationPage> {
  const params = new URLSearchParams({
    page: String(normalizePage(options.page)),
    pageSize: String(normalizePageSize(options.pageSize)),
  });
  const payload = await readJson(
    await requestApi(`api/assetverification/page?${params.toString()}`),
  );
  return readAssetVerificationPage(payload);
}

export async function createAssetVerification(input: AssetVerificationInput) {
  const payload = await readJson(
    await requestApi("api/assetverification", {
      method: "POST",
      body: JSON.stringify(toApiInput(input)),
    }),
  );
  return mapRecord(payload);
}

export async function updateAssetVerification(code: number, input: AssetVerificationInput) {
  const payload = await readJson(
    await requestApi(`api/assetverification/${encodeURIComponent(code)}`, {
      method: "PUT",
      body: JSON.stringify(toApiInput({ ...input, assetVerificationCode: code })),
    }),
  );
  return mapRecord(payload);
}
