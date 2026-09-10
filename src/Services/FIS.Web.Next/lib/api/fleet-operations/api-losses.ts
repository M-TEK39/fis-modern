import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type LossRecord = {
  lossCode: number;
  vmfCode: number;
  lossDate: string | null;
  lossReference: string | null;
  lossTypeCode: number | null;
  lossTypeDescription: string | null;
  siteCode: number | null;
  siteDescription: string | null;
  departmentContact: string | null;
  lossAmount: number | null;
  departmentClaim: number | null;
  sapd: string | null;
  inspector: string | null;
  caseNumber: string | null;
  cancelled: boolean;
  coverForfeit: boolean;
  prosecute: boolean;
  compensationOrder: boolean;
  remarks: string | null;
  hqReference: string | null;
  placeOfLoss: string | null;
  garagingAuthority: boolean;
  driverName: string | null;
  reportFromDepartment: boolean;
  dateReportedGgmt: string | null;
  dateReportedSapd: string | null;
  callReference: number | null;
  towNeed: string | null;
  lossStatus: string | null;
  dateCreated: string | null;
  dateUpdated: string | null;
  vehicleIdentifier: string | null;
};

export type LossInput = Omit<
  LossRecord,
  | "lossCode"
  | "lossTypeDescription"
  | "siteDescription"
  | "vehicleIdentifier"
  | "dateCreated"
  | "dateUpdated"
>;

export type LossVehicleMatch = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
};

export type LossApiErrorReason = "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class LossApiError extends Error {
  constructor(
    public readonly reason: LossApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "LossApiError";
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

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) throw new LossApiError("unauthorized", "No FIS access cookie is available.");

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
      throw new LossApiError(
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
      throw new LossApiError(
        response.status === 404
          ? "not-found"
          : response.status >= 500
            ? "unavailable"
            : "invalid-response",
        message,
        response.status,
      );
    }
    return response;
  } catch (error) {
    if (error instanceof LossApiError) throw error;
    throw new LossApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new LossApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapLoss(value: unknown): LossRecord | null {
  if (!isRecord(value)) return null;
  const lossCode = asNumber(getValue(value, "loss_code", "lossCode"));
  const vmfCode = asNumber(getValue(value, "vmf_code", "vmfCode"));
  if (lossCode === null || vmfCode === null) return null;

  return {
    lossCode,
    vmfCode,
    lossDate: asString(getValue(value, "loss_date", "lossDate")),
    lossReference: asString(getValue(value, "loss_reference", "lossReference")),
    lossTypeCode: asNumber(getValue(value, "loss_type_code", "lossTypeCode")),
    lossTypeDescription: asString(getValue(value, "loss_type_description", "lossTypeDescription")),
    siteCode: asNumber(getValue(value, "site_code", "siteCode")),
    siteDescription: asString(getValue(value, "site_description", "siteDescription")),
    departmentContact: asString(getValue(value, "dept_contact", "departmentContact")),
    lossAmount: asNumber(getValue(value, "loss_amount", "lossAmount")),
    departmentClaim: asNumber(getValue(value, "dept_claim", "departmentClaim")),
    sapd: asString(getValue(value, "sapd", "sapdStation")),
    inspector: asString(getValue(value, "inspector")),
    caseNumber: asString(getValue(value, "case_number", "caseNumber")),
    cancelled: asBoolean(getValue(value, "cancelled")),
    coverForfeit: asBoolean(getValue(value, "cover_forfeit", "coverForfeit")),
    prosecute: asBoolean(getValue(value, "prosecute")),
    compensationOrder: asBoolean(getValue(value, "compensation_order", "compensationOrder")),
    remarks: asString(getValue(value, "remarks")),
    hqReference: asString(getValue(value, "hq_reference", "hqReference")),
    placeOfLoss: asString(getValue(value, "place_of_loss", "placeOfLoss")),
    garagingAuthority: asBoolean(getValue(value, "garaging_authority", "garagingAuthority")),
    driverName: asString(getValue(value, "driver_name", "driverName")),
    reportFromDepartment: asBoolean(getValue(value, "report_from_dept", "reportFromDepartment")),
    dateReportedGgmt: asString(getValue(value, "date_reported_ggmt", "dateReportedGgmt")),
    dateReportedSapd: asString(getValue(value, "date_reported_sapd", "dateReportedSapd")),
    callReference: asNumber(getValue(value, "Call_Refer", "callReference")),
    towNeed: asString(getValue(value, "Tow_need", "towNeed")),
    lossStatus: asString(getValue(value, "loss_status", "lossStatus")),
    dateCreated: asString(getValue(value, "date_created", "dateCreated")),
    dateUpdated: asString(getValue(value, "date_updated", "dateUpdated")),
    vehicleIdentifier: asString(
      getValue(value, "vehicle_identifier", "vehicleIdentifier", "vehicle_fleet_number"),
    ),
  };
}

function mapVehicle(value: unknown): LossVehicleMatch | null {
  if (!isRecord(value)) return null;
  const vmfCode = asNumber(getValue(value, "VmfCode", "vmfCode", "vmf_code"));
  if (vmfCode === null) return null;
  return {
    vmfCode,
    fleetNumber: asString(getValue(value, "FleetNumber", "fleetNumber", "fleet_number")),
    registrationNumber: asString(
      getValue(value, "RegistrationNumber", "registrationNumber", "registration_number"),
    ),
  };
}

function toApiInput(input: LossInput) {
  return {
    vmf_code: input.vmfCode,
    loss_date: input.lossDate,
    loss_reference: input.lossReference,
    loss_type_code: input.lossTypeCode,
    site_code: input.siteCode,
    dept_contact: input.departmentContact,
    loss_amount: input.lossAmount,
    dept_claim: input.departmentClaim,
    sapd: input.sapd,
    inspector: input.inspector,
    case_number: input.caseNumber,
    cancelled: input.cancelled ? 1 : 0,
    cover_forfeit: input.coverForfeit ? 1 : 0,
    prosecute: input.prosecute ? 1 : 0,
    compensation_order: input.compensationOrder ? 1 : 0,
    remarks: input.remarks,
    hq_reference: input.hqReference,
    place_of_loss: input.placeOfLoss,
    garaging_authority: input.garagingAuthority ? 1 : 0,
    driver_name: input.driverName,
    report_from_dept: input.reportFromDepartment ? 1 : 0,
    date_reported_ggmt: input.dateReportedGgmt,
    date_reported_sapd: input.dateReportedSapd,
    Call_Refer: input.callReference,
    Tow_need: input.towNeed,
    loss_status: input.lossStatus,
  };
}

export async function getLosses() {
  const payload = await readJson(await requestApi("api/loss"));
  return getCollection(payload)
    .map(mapLoss)
    .filter((item): item is LossRecord => item !== null);
}

export async function getLoss(lossCode: number) {
  try {
    return mapLoss(await readJson(await requestApi(`api/loss/${encodeURIComponent(lossCode)}`)));
  } catch (error) {
    if (error instanceof LossApiError && error.reason === "not-found") return null;
    throw error;
  }
}

export async function getLossesByVehicleIdentifier(identifier: string, mode: "GG" | "GP" = "GG") {
  const payload = await readJson(
    await requestApi(`api/loss/vehicle/${encodeURIComponent(identifier)}?mode=${mode}`),
  );
  return getCollection(payload)
    .map(mapLoss)
    .filter((item): item is LossRecord => item !== null);
}

export async function getLossesByVmfCode(vmfCode: number) {
  const payload = await readJson(await requestApi(`api/loss/vmf/${encodeURIComponent(vmfCode)}`));
  return getCollection(payload)
    .map(mapLoss)
    .filter((item): item is LossRecord => item !== null);
}

export async function getLossVehicleMatches(identifier: string) {
  const payload = await readJson(
    await requestApi(`api/vehiclelookup?keyword=${encodeURIComponent(identifier)}&limit=20`),
  );
  return getCollection(payload)
    .map(mapVehicle)
    .filter((item): item is LossVehicleMatch => item !== null);
}

export async function createLoss(input: LossInput) {
  return mapLoss(
    await readJson(
      await requestApi("api/loss", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(toApiInput(input)),
      }),
    ),
  );
}

export async function updateLoss(lossCode: number, input: LossInput) {
  return mapLoss(
    await readJson(
      await requestApi(`api/loss/${encodeURIComponent(lossCode)}`, {
        method: "PUT",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ loss_code: lossCode, ...toApiInput(input) }),
      }),
    ),
  );
}

export async function deleteLoss(lossCode: number) {
  await requestApi(`api/loss/${encodeURIComponent(lossCode)}`, { method: "DELETE" });
}
