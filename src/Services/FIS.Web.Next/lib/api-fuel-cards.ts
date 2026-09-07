import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type FuelCardRecord = {
  fuelCardCode: number;
  vmfCode: number | null;
  counter: number | null;
  cardNumber: string | null;
  panNumber: string | null;
  receiver: string | null;
  receiverTelephone: string | null;
  takenDate: string | null;
  expireDate: string | null;
  reason: string | null;
  comment: string | null;
  ggNumber: string | null;
  statusDate: string | null;
  siteCode: number | null;
  garage: string | null;
  isDeleted: boolean;
};

export type PrivateHireFuelCardRecord = FuelCardRecord & {
  privateHireCode: number;
};

export type FuelCardActivity = {
  cardNumber: string | null;
  vmfCode: number;
  action: string;
  date: string;
  receiver: string;
};

export type FuelCardAllocationReport = {
  totalCards: number;
  activeCards: number;
  returnedCards: number;
  expiringCards: number;
  statusBreakdown: Record<string, number>;
  cardsByGarage: Record<string, number>;
  recentActivity: FuelCardActivity[];
};

export type FuelCardApiErrorReason = "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class FuelCardApiError extends Error {
  constructor(public readonly reason: FuelCardApiErrorReason, message: string) {
    super(message);
    this.name = "FuelCardApiError";
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
  return typeof value === "string" && ["true", "1", "y"].includes(value.trim().toLowerCase());
}

function asDate(value: unknown) {
  return asString(value);
}

function getCollection(payload: unknown) {
  if (Array.isArray(payload)) return payload;
  if (isRecord(payload)) {
    const collection = getValue(payload, "data", "items", "results");
    return Array.isArray(collection) ? collection : [];
  }
  return [];
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) throw new FuelCardApiError("unauthorized", "No FIS access cookie is available.");

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
    if (response.status === 401 || response.status === 403) throw new FuelCardApiError("unauthorized", "The FIS access cookie was rejected.");
    if (response.status === 404) throw new FuelCardApiError("not-found", "The requested fuel card record was not found.");
    if (!response.ok) throw new FuelCardApiError(response.status >= 500 ? "unavailable" : "invalid-response", `FIS API returned HTTP ${response.status}.`);
    return response;
  } catch (error) {
    if (error instanceof FuelCardApiError) throw error;
    throw new FuelCardApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new FuelCardApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapFuelCard(value: unknown): FuelCardRecord | null {
  if (!isRecord(value)) return null;
  const code = asNumber(getValue(value, "Fuel_card_code", "fuelCardCode", "PHFuel_card_code"));
  if (code === null) return null;
  return {
    fuelCardCode: code,
    vmfCode: asNumber(getValue(value, "vmf_code", "vmfCode", "phv_code")),
    counter: asNumber(getValue(value, "Counter", "counter")),
    cardNumber: asString(getValue(value, "card_number", "cardNumber")),
    panNumber: asString(getValue(value, "PAN_number", "panNumber")),
    receiver: asString(getValue(value, "PetReceiver", "receiverName", "receiver")),
    receiverTelephone: asString(getValue(value, "PetRecTel", "receiverTelephone")),
    takenDate: asDate(getValue(value, "PetTaken", "takenDate", "issuedDate")),
    expireDate: asDate(getValue(value, "PetExpire", "expireDate", "expiryDate")),
    reason: asString(getValue(value, "ExpReason", "reason", "status")),
    comment: asString(getValue(value, "PetComment", "comment")),
    ggNumber: asString(getValue(value, "LinkGGNum", "ggNumber")),
    statusDate: asDate(getValue(value, "Status_date", "statusDate")),
    siteCode: asNumber(getValue(value, "Petrecsite", "siteCode")),
    garage: asString(getValue(value, "Garage", "garage")),
    isDeleted: asBoolean(getValue(value, "is_deleted", "isDeleted")),
  };
}

function mapPrivateHireFuelCard(value: unknown): PrivateHireFuelCardRecord | null {
  const mapped = mapFuelCard(value);
  if (!mapped) return null;
  return { ...mapped, privateHireCode: mapped.vmfCode ?? 0 };
}

export async function getFuelCardsByVehicle(vmfCode: number) {
  const records = getCollection(await readJson(await requestApi(`api/FuelCard/vehicle/${encodeURIComponent(vmfCode)}`)))
    .map(mapFuelCard)
    .filter((record): record is FuelCardRecord => record !== null);
  return records;
}

export async function getFuelCardsByCardNumber(cardNumber: string) {
  const response = await requestApi("api/FuelCard/delete/search", { method: "POST", body: JSON.stringify({ CardNumber: cardNumber }) });
  const payload = await readJson(response);
  if (!isRecord(payload)) return [];
  return getCollection(getValue(payload, "fuelCards", "FuelCards")).map(mapFuelCard).filter((record): record is FuelCardRecord => record !== null);
}

export async function createFuelCard(input: {
  vmf_code: number;
  Counter: number;
  card_number: string | null;
  PAN_number: string | null;
  ExpReason: string;
  Status_date: string;
  PetTaken: string;
  PetExpire: string;
  LinkGGNum: string | null;
}) {
  const record = mapFuelCard(await readJson(await requestApi("api/FuelCard", { method: "POST", body: JSON.stringify(input) })));
  if (!record) throw new FuelCardApiError("invalid-response", "The FIS API returned an invalid fuel card record.");
  return record;
}

export async function deleteFuelCard(fuelCardCode: number) {
  await requestApi(`api/FuelCard/${encodeURIComponent(fuelCardCode)}`, { method: "DELETE" });
}

export async function getPrivateHireFuelCardsByRegistration(registrationNumber: string) {
  const records = getCollection(await readJson(await requestApi(`api/PrivateHireFuelCard/registration/${encodeURIComponent(registrationNumber)}`)))
    .map(mapPrivateHireFuelCard)
    .filter((record): record is PrivateHireFuelCardRecord => record !== null);
  return records;
}

export async function getAllPrivateHireFuelCards() {
  const records = getCollection(await readJson(await requestApi("api/PrivateHireFuelCard")))
    .map(mapPrivateHireFuelCard)
    .filter((record): record is PrivateHireFuelCardRecord => record !== null);
  return records;
}

export async function createPrivateHireFuelCard(input: {
  RegistrationNumber: string;
  Counter: number;
  CardNumber: string | null;
  PanNumber: string | null;
}) {
  const record = mapPrivateHireFuelCard(await readJson(await requestApi("api/PrivateHireFuelCard", { method: "POST", body: JSON.stringify(input) })));
  if (!record) throw new FuelCardApiError("invalid-response", "The FIS API returned an invalid private hire fuel card record.");
  return record;
}

export async function deletePrivateHireFuelCard(fuelCardCode: number) {
  await requestApi(`api/PrivateHireFuelCard/${encodeURIComponent(fuelCardCode)}`, { method: "DELETE" });
}

export async function getFuelCardAllocation(siteCode?: number) {
  const query = siteCode === undefined ? "" : `?siteCode=${encodeURIComponent(siteCode)}`;
  const payload = await readJson(await requestApi(`api/FleetManagement/reports/fuelcard-allocation${query}`));
  if (!isRecord(payload)) throw new FuelCardApiError("invalid-response", "The FIS API returned an invalid fuel card report.");
  const report = getValue(payload, "report", "Report");
  if (!isRecord(report)) throw new FuelCardApiError("invalid-response", "The FIS API returned no fuel card report.");
  return {
    totalCards: asNumber(getValue(report, "totalCards", "TotalCards")) ?? 0,
    activeCards: asNumber(getValue(report, "activeCards", "ActiveCards")) ?? 0,
    returnedCards: asNumber(getValue(report, "returnedCards", "ReturnedCards")) ?? 0,
    expiringCards: asNumber(getValue(report, "expiringCards", "ExpiringCards")) ?? 0,
    statusBreakdown: (getValue(report, "statusBreakdown", "StatusBreakdown") as Record<string, number> | undefined) ?? {},
    cardsByGarage: (getValue(report, "cardsByGarage", "CardsByGarage") as Record<string, number> | undefined) ?? {},
    recentActivity: getCollection(getValue(report, "recentActivity", "RecentActivity")).flatMap((value) => {
      if (!isRecord(value)) return [];
      return [{
        cardNumber: asString(getValue(value, "cardNumber", "CardNumber")),
        vmfCode: asNumber(getValue(value, "vmfCode", "VmfCode")) ?? 0,
        action: asString(getValue(value, "action", "Action")) ?? "Unknown",
        date: asString(getValue(value, "date", "Date")) ?? "",
        receiver: asString(getValue(value, "receiver", "Receiver")) ?? "Unknown",
      } satisfies FuelCardActivity];
    }),
  } satisfies FuelCardAllocationReport;
}
