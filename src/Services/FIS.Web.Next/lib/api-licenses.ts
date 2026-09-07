import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type LicenseVehicleDetails = {
  vmfCode: number;
  numberType: "GG" | "GP";
  number: string;
  expDate: string | null;
  registerNumber: string | null;
  regDoc: string | null;
  tare: string | null;
  receiver: string | null;
  receiverId: string | null;
  receiverTel: string | null;
  receiverSiteCode: number | null;
  dateCollected: string | null;
  cofRequired: string | null;
  cofExpDate: string | null;
  comments: string | null;
};

export type LicenseVehicleWriteInput = Omit<LicenseVehicleDetails, "vmfCode" | "numberType" | "number"> & {
  vmfCode: number;
  numberType: "GG" | "GP";
  number: string;
  updateNotes?: string | null;
};

export type LicenseHistoryEntry = {
  historyId: number;
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  dueDate: string | null;
  registerNumber: string | null;
  registrationDocument: string | null;
  comments: string | null;
  cofDate: string | null;
  cofRequired: string | null;
  tare: number | null;
  receiver: string | null;
  receiverId: string | null;
  receiverTel: string | null;
  receiverSite: number | null;
  dateCollected: string | null;
  capturedAt: string | null;
  capturedByEmail: string | null;
  updateNotes: string | null;
};

export type LicenseHistory = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  history: LicenseHistoryEntry[];
};

export type LicenseApiErrorReason = "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class LicenseApiError extends Error {
  constructor(public readonly reason: LicenseApiErrorReason, message: string, public readonly status?: number) {
    super(message);
    this.name = "LicenseApiError";
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

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) throw new LicenseApiError("unauthorized", "No FIS access cookie is available.");
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers: { accept: "application/json", cookie: cookieHeader, ...init.headers },
      signal: controller.signal,
    });
    if (response.status === 401 || response.status === 403) throw new LicenseApiError("unauthorized", "The FIS access cookie was rejected.", response.status);
    if (response.status === 404) throw new LicenseApiError("not-found", "The requested licence record was not found.", response.status);
    if (!response.ok) {
      let message = `FIS API returned HTTP ${response.status}.`;
      try {
        const payload = await response.clone().json();
        if (isRecord(payload)) message = asString(getValue(payload, "message", "Message", "error")) ?? message;
      } catch {
        // Keep the status-based error when the body is not JSON.
      }
      throw new LicenseApiError(response.status >= 500 ? "unavailable" : "invalid-response", message, response.status);
    }
    return response;
  } catch (error) {
    if (error instanceof LicenseApiError) throw error;
    throw new LicenseApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new LicenseApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapDetails(value: unknown, numberType: "GG" | "GP", number: string): LicenseVehicleDetails | null {
  if (!isRecord(value)) return null;
  const vmfCode = asNumber(getValue(value, "vmfCode", "vmf_code"));
  if (vmfCode === null) return null;
  return {
    vmfCode,
    numberType,
    number,
    expDate: asString(getValue(value, "expDate", "licence_due_date")),
    registerNumber: asString(getValue(value, "registerNumber", "lic_register_number")),
    regDoc: asString(getValue(value, "regDoc", "lic_registration_doc")),
    tare: asString(getValue(value, "tare")),
    receiver: asString(getValue(value, "receiver", "Licence_receiver", "licence_receiver")),
    receiverId: asString(getValue(value, "receiverId", "Licence_receiver_id", "licence_receiver_id")),
    receiverTel: asString(getValue(value, "receiverTel", "Licence_receiver_tel", "licence_receiver_tel")),
    receiverSiteCode: asNumber(getValue(value, "receiverSiteCode", "Licence_receiver_site", "licence_receiver_site")),
    dateCollected: asString(getValue(value, "dateCollected", "Licence_date_taken", "licence_date_taken")),
    cofRequired: asString(getValue(value, "cofRequired", "cof_required")),
    cofExpDate: asString(getValue(value, "cofExpDate", "cof_last_done")),
    comments: asString(getValue(value, "comments", "licence_comments")),
  };
}

function mapHistory(value: unknown, fallbackVmfCode: number): LicenseHistory {
  if (!isRecord(value)) return { vmfCode: fallbackVmfCode, fleetNumber: null, registrationNumber: null, history: [] };
  const historyValue = getValue(value, "history");
  const history = Array.isArray(historyValue)
    ? historyValue.flatMap((item) => {
        if (!isRecord(item)) return [];
        const historyId = asNumber(getValue(item, "licence_history_id", "historyId"));
        const vmfCode = asNumber(getValue(item, "vmf_code", "vmfCode"));
        if (historyId === null || vmfCode === null) return [];
        return [{
          historyId,
          vmfCode,
          fleetNumber: asString(getValue(item, "fleet_number", "fleetNumber")),
          registrationNumber: asString(getValue(item, "registration_number", "registrationNumber")),
          dueDate: asString(getValue(item, "licence_due_date", "dueDate")),
          registerNumber: asString(getValue(item, "lic_register_number", "registerNumber")),
          registrationDocument: asString(getValue(item, "lic_registration_doc", "registrationDocument")),
          comments: asString(getValue(item, "licence_comments", "comments")),
          cofDate: asString(getValue(item, "cof_last_done", "cofDate")),
          cofRequired: asString(getValue(item, "cof_required", "cofRequired")),
          tare: asNumber(getValue(item, "tare")),
          receiver: asString(getValue(item, "Licence_receiver", "receiver")),
          receiverId: asString(getValue(item, "Licence_receiver_id", "receiverId")),
          receiverTel: asString(getValue(item, "Licence_receiver_tel", "receiverTel")),
          receiverSite: asNumber(getValue(item, "Licence_receiver_site", "receiverSite")),
          dateCollected: asString(getValue(item, "Licence_date_taken", "dateCollected")),
          capturedAt: asString(getValue(item, "captured_at", "capturedAt")),
          capturedByEmail: asString(getValue(item, "captured_by_user_email", "capturedByEmail")),
          updateNotes: asString(getValue(item, "update_notes", "updateNotes")),
        } satisfies LicenseHistoryEntry];
      })
    : [];
  return {
    vmfCode: asNumber(getValue(value, "vmf_code", "vmfCode")) ?? fallbackVmfCode,
    fleetNumber: asString(getValue(value, "fleet_number", "fleetNumber")),
    registrationNumber: asString(getValue(value, "registration_number", "registrationNumber")),
    history,
  };
}

export async function submitLicensePassword(password: string) {
  await requestApi("api/License/one-vehicle/password", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ password }),
  });
}

export async function getLicenseVehicle(numberType: "GG" | "GP", number: string) {
  const payload = await readJson(await requestApi("api/License/one-vehicle/lookup", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ number_type: numberType, number }),
  }));
  const details = mapDetails(payload, numberType, number);
  if (!details) throw new LicenseApiError("invalid-response", "The licence lookup response was invalid.");
  return details;
}

export async function getLicenseVehicleHistory(vmfCode: number) {
  return mapHistory(await readJson(await requestApi(`api/vehicles/${encodeURIComponent(vmfCode)}/licence/history`)), vmfCode);
}

export async function captureLicenseVehicle(input: LicenseVehicleWriteInput) {
  const payload = await readJson(await requestApi(`api/vehicles/${encodeURIComponent(input.vmfCode)}/licence`, {
    method: "PATCH",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({
      licence_due_date: input.expDate,
      lic_register_number: input.registerNumber,
      lic_registration_doc: input.regDoc,
      licence_comments: input.comments,
      cof_last_done: input.cofExpDate,
      cof_required: input.cofRequired,
      tare: input.tare ? Number(input.tare) : null,
      Licence_receiver: input.receiver,
      Licence_receiver_id: input.receiverId,
      Licence_receiver_tel: input.receiverTel,
      Licence_receiver_site: input.receiverSiteCode,
      Licence_date_taken: input.dateCollected,
      update_notes: input.updateNotes ?? null,
    }),
  }));
  return mapDetails(payload, input.numberType, input.number);
}
