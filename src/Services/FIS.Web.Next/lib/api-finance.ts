import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;

export type FinanceApiErrorReason = "unauthorized" | "unavailable" | "invalid-response";

export class FinanceApiError extends Error {
  constructor(public readonly reason: FinanceApiErrorReason, message: string, public readonly status?: number) {
    super(message);
    this.name = "FinanceApiError";
  }
}

export type FinanceBatchStatus = {
  batchCode: number;
  batchDate: string | null;
  status: string | null;
  isActive: boolean;
  totalTransactions: number;
  processedTransactions: number;
};

function getApiBaseUrl() {
  const value = process.env.API_BASE_URL?.trim() || "http://localhost:5010";
  return `${value.replace(/\/$/, "")}/`;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function getValue(record: Record<string, unknown>, ...keys: string[]) {
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
  return null;
}

function asBoolean(value: unknown) {
  if (typeof value === "boolean") return value;
  if (typeof value === "number") return value !== 0;
  return ["true", "1", "yes", "y"].includes(String(value).trim().toLowerCase());
}

async function requestJson(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) throw new FinanceApiError("unauthorized", "No FIS access cookie is available.");

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers: { accept: "application/json", cookie: cookieHeader, ...init.headers },
      signal: controller.signal,
    });
    if (response.status === 401 || response.status === 403) throw new FinanceApiError("unauthorized", "The FIS access cookie was rejected.", response.status);
    if (!response.ok) throw new FinanceApiError(response.status >= 500 ? "unavailable" : "invalid-response", `FIS API returned HTTP ${response.status}.`, response.status);
    try {
      return (await response.json()) as unknown;
    } catch {
      throw new FinanceApiError("invalid-response", "The FIS API returned invalid JSON.");
    }
  } catch (error) {
    if (error instanceof FinanceApiError) throw error;
    throw new FinanceApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

export async function getBatchStatus(): Promise<FinanceBatchStatus> {
  const value = await requestJson("api/finance/batch/status");
  if (!isRecord(value)) throw new FinanceApiError("invalid-response", "The FIS API returned an invalid batch status.");
  return {
    batchCode: asNumber(getValue(value, "batchCode", "BatchCode")) ?? 0,
    batchDate: asString(getValue(value, "batchDate", "BatchDate")),
    status: asString(getValue(value, "status", "Status")),
    isActive: asBoolean(getValue(value, "isActive", "IsActive", "batchIsRunning", "BatchIsRunning")),
    totalTransactions: asNumber(getValue(value, "totalTransactions", "TotalTransactions")) ?? 0,
    processedTransactions: asNumber(getValue(value, "processedTransactions", "ProcessedTransactions")) ?? 0,
  };
}

export type FinanceOption = { value: string; label: string };

export type BasSegment = {
  segmentCode: number;
  segmentType: string;
  segmentValue: string;
  departmentCode: number | null;
  isActive: boolean;
};

export type FinanceRow = Record<string, string | number | boolean | null>;

function queryString(values: Record<string, string | number | undefined>) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(values)) {
    if (value !== undefined && String(value).trim() !== "") params.set(key, String(value));
  }
  return params.size > 0 ? `?${params.toString()}` : "";
}

function collection(value: unknown) {
  if (Array.isArray(value)) return value;
  if (isRecord(value)) {
    const items = getValue(value, "items", "Items", "data", "Data", "results", "Results");
    return Array.isArray(items) ? items : [];
  }
  return [];
}

function toOption(value: unknown, valueKeys: string[], labelKeys: string[]): FinanceOption | null {
  if (!isRecord(value)) return null;
  const rawValue = valueKeys.map((key) => getValue(value, key)).find((item) => item !== undefined && item !== null);
  const rawLabel = labelKeys.map((key) => getValue(value, key)).find((item) => item !== undefined && item !== null);
  const optionValue = asString(rawValue) ?? (typeof rawValue === "number" ? String(rawValue) : null);
  if (!optionValue) return null;
  return { value: optionValue, label: asString(rawLabel) ?? optionValue };
}

async function getOptions(path: string, valueKeys: string[], labelKeys: string[]) {
  return collection(await requestJson(path))
    .map((item) => toOption(item, valueKeys, labelKeys))
    .filter((item): item is FinanceOption => item !== null);
}

export function getFinanceDepartments() {
  return getOptions("api/department", ["departmentCode", "DepartmentCode", "code", "Code"], ["description", "Description", "departmentName", "DepartmentName", "name", "Name"]);
}

export function getFinanceSites(departmentCode?: number) {
  return getOptions(`api/site${queryString({ departmentCode })}`, ["siteCode", "SiteCode", "code", "Code"], ["description", "Description", "siteName", "SiteName", "name", "Name"]);
}

export function getFinanceProvinces() {
  return getOptions("api/province", ["province_code", "provinceCode", "code", "Code"], ["province_name", "provinceName", "name", "Name"]);
}

export function getFinanceYears() {
  return getOptions("api/finance/reference/financial-years", ["code", "Code", "value", "Value"], ["name", "Name", "label", "Label"]);
}

export function getFinanceSegmentTypes() {
  return getOptions("api/finance/reference/segment-types", ["value", "Value", "code", "Code", "id", "Id"], ["label", "Label", "name", "Name", "description", "Description"]);
}

function mapBasSegment(value: unknown): BasSegment | null {
  if (!isRecord(value)) return null;
  const segmentCode = asNumber(getValue(value, "segmentCode", "SegmentCode", "segment_code"));
  if (segmentCode === null) return null;
  return {
    segmentCode,
    segmentType: asString(getValue(value, "segmentType", "SegmentType")) ?? "",
    segmentValue: asString(getValue(value, "segmentValue", "SegmentValue")) ?? "",
    departmentCode: asNumber(getValue(value, "departmentCode", "DepartmentCode", "department_code")),
    isActive: asBoolean(getValue(value, "isActive", "IsActive")),
  };
}

export async function getBasSegments(departmentCode?: number, segmentType?: string) {
  return collection(await requestJson(`api/finance/bas/segments${queryString({ departmentCode, segmentType })}`))
    .map(mapBasSegment)
    .filter((item): item is BasSegment => item !== null);
}

export async function getBasRows(path: string) {
  return collection(await requestJson(path)).filter(isRecord).map((item) => {
    const row: FinanceRow = {};
    for (const [key, value] of Object.entries(item)) {
      row[key] = typeof value === "string" || typeof value === "number" || typeof value === "boolean" || value === null ? value : JSON.stringify(value);
    }
    return row;
  });
}

export async function importBas(fileData: string, departmentCode?: number) {
  return requestJson("api/finance/bas/import", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ fileData, departmentCode }) });
}

export async function activateBasSegments(segmentCodes: number[]) {
  return requestJson("api/finance/bas/segments/activate", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ segmentCodes }) });
}

export function runFinanceAction(path: string, body: unknown = {}) {
  return requestJson(path, { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify(body) });
}
