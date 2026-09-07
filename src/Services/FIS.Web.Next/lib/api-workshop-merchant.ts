import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type WorkshopMerchantRecord = {
  merchantCode: number;
  name: string | null;
  tel: string | null;
  fax: string | null;
  email: string | null;
};

export type WorkshopMerchantInput = {
  name: string;
  tel: string | null;
  fax: string | null;
  email: string | null;
};

export class WorkshopMerchantApiError extends Error {
  constructor(public readonly reason: "unauthorized" | "unavailable" | "invalid-response" | "not-found", message: string) {
    super(message);
    this.name = "WorkshopMerchantApiError";
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
  return typeof value === "string" ? value.trim() || null : value === null || value === undefined ? null : String(value);
}

function asNumber(value: unknown) {
  const parsed = typeof value === "number" ? value : Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function getCollection(payload: unknown) {
  if (Array.isArray(payload)) return payload;
  if (isRecord(payload)) {
    const values = getValue(payload, "data", "items", "results");
    return Array.isArray(values) ? values : [];
  }
  return [];
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookie = await getForwardedAuthCookieHeader();
  if (!cookie) throw new WorkshopMerchantApiError("unauthorized", "No FIS access cookie is available.");
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers: { accept: "application/json", cookie, ...(init.body ? { "content-type": "application/json" } : {}), ...init.headers },
      signal: controller.signal,
    });
    if (response.status === 401 || response.status === 403) throw new WorkshopMerchantApiError("unauthorized", "The FIS access cookie was rejected.");
    if (response.status === 404) throw new WorkshopMerchantApiError("not-found", "The workshop merchant was not found.");
    if (!response.ok) throw new WorkshopMerchantApiError("invalid-response", `FIS API returned HTTP ${response.status}.`);
    return response;
  } catch (error) {
    if (error instanceof WorkshopMerchantApiError) throw error;
    throw new WorkshopMerchantApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try { return (await response.json()) as unknown; }
  catch { throw new WorkshopMerchantApiError("invalid-response", "The FIS API returned invalid JSON."); }
}

function mapMerchant(value: unknown): WorkshopMerchantRecord | null {
  if (!isRecord(value)) return null;
  const merchantCode = asNumber(getValue(value, "merchantCode", "MerchantCode", "wwmerch_code"));
  if (merchantCode === null) return null;
  return {
    merchantCode,
    name: asString(getValue(value, "name", "Name", "wwmerch_name")),
    tel: asString(getValue(value, "tel", "Tel", "wwmerch_tel")),
    fax: asString(getValue(value, "fax", "Fax", "wwmerch_fax")),
    email: asString(getValue(value, "email", "Email", "wwmerch_email")),
  };
}

export async function getWorkshopMerchants() {
  const response = await requestApi("api/workshop/merchants");
  return getCollection(await readJson(response)).map(mapMerchant).filter((merchant): merchant is WorkshopMerchantRecord => merchant !== null);
}

export async function getWorkshopMerchant(merchantCode: number) {
  const response = await requestApi(`api/workshop/merchants/${encodeURIComponent(merchantCode)}`);
  const merchant = mapMerchant(await readJson(response));
  if (!merchant) throw new WorkshopMerchantApiError("invalid-response", "The FIS API returned an invalid workshop merchant.");
  return merchant;
}

export async function createWorkshopMerchant(input: WorkshopMerchantInput) {
  const response = await requestApi("api/workshop/merchants", { method: "POST", body: JSON.stringify({ Name: input.name, Tel: input.tel, Fax: input.fax, Email: input.email }) });
  const merchant = mapMerchant(await readJson(response));
  if (!merchant) throw new WorkshopMerchantApiError("invalid-response", "The FIS API returned an invalid workshop merchant.");
  return merchant;
}

export async function updateWorkshopMerchant(merchantCode: number, input: WorkshopMerchantInput) {
  const response = await requestApi(`api/workshop/merchants/${encodeURIComponent(merchantCode)}`, { method: "PUT", body: JSON.stringify({ Name: input.name, Tel: input.tel, Fax: input.fax, Email: input.email }) });
  const merchant = mapMerchant(await readJson(response));
  if (!merchant) throw new WorkshopMerchantApiError("invalid-response", "The FIS API returned an invalid workshop merchant.");
  return merchant;
}

export async function deleteWorkshopMerchant(merchantCode: number) {
  await requestApi(`api/workshop/merchants/${encodeURIComponent(merchantCode)}`, { method: "DELETE" });
}
