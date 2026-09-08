import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;

export type FinanceApiErrorReason = "unauthorized" | "unavailable" | "invalid-response";

export class FinanceApiError extends Error {
  constructor(
    public readonly reason: FinanceApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
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
  if (!cookieHeader)
    throw new FinanceApiError("unauthorized", "No FIS access cookie is available.");

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers: { accept: "application/json", cookie: cookieHeader, ...init.headers },
      signal: controller.signal,
    });
    if (response.status === 401 || response.status === 403)
      throw new FinanceApiError(
        "unauthorized",
        "The FIS access cookie was rejected.",
        response.status,
      );
    if (!response.ok)
      throw new FinanceApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        `FIS API returned HTTP ${response.status}.`,
        response.status,
      );
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

export function getFinanceJson(path: string) {
  return requestJson(path);
}

export type FinanceOutput = {
  body: ArrayBuffer;
  contentType: string;
  filename: string | null;
};

export async function getFinanceOutput(
  path: string,
  init: RequestInit = {},
): Promise<FinanceOutput> {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader)
    throw new FinanceApiError("unauthorized", "No FIS access cookie is available.");

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers: { accept: "*/*", cookie: cookieHeader, ...init.headers },
      signal: controller.signal,
    });
    if (response.status === 401 || response.status === 403)
      throw new FinanceApiError(
        "unauthorized",
        "The FIS access cookie was rejected.",
        response.status,
      );
    if (!response.ok)
      throw new FinanceApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        `FIS API returned HTTP ${response.status}.`,
        response.status,
      );
    const disposition = response.headers.get("content-disposition");
    const filenameMatch = disposition?.match(/filename\*?=(?:UTF-8''|\")?([^\";]+)/i);
    return {
      body: await response.arrayBuffer(),
      contentType: response.headers.get("content-type") ?? "application/octet-stream",
      filename: filenameMatch ? decodeURIComponent(filenameMatch[1]) : null,
    };
  } catch (error) {
    if (error instanceof FinanceApiError) throw error;
    throw new FinanceApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

export async function getBatchStatus(): Promise<FinanceBatchStatus> {
  const value = await requestJson("api/finance/batch/status");
  if (!isRecord(value))
    throw new FinanceApiError("invalid-response", "The FIS API returned an invalid batch status.");
  return {
    batchCode: asNumber(getValue(value, "batchCode", "BatchCode")) ?? 0,
    batchDate: asString(getValue(value, "batchDate", "BatchDate")),
    status: asString(getValue(value, "status", "Status")),
    isActive: asBoolean(
      getValue(value, "isActive", "IsActive", "batchIsRunning", "BatchIsRunning"),
    ),
    totalTransactions: asNumber(getValue(value, "totalTransactions", "TotalTransactions")) ?? 0,
    processedTransactions:
      asNumber(getValue(value, "processedTransactions", "ProcessedTransactions")) ?? 0,
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
  const rawValue = valueKeys
    .map((key) => getValue(value, key))
    .find((item) => item !== undefined && item !== null);
  const rawLabel = labelKeys
    .map((key) => getValue(value, key))
    .find((item) => item !== undefined && item !== null);
  const optionValue =
    asString(rawValue) ?? (typeof rawValue === "number" ? String(rawValue) : null);
  if (!optionValue) return null;
  return { value: optionValue, label: asString(rawLabel) ?? optionValue };
}

async function getOptions(path: string, valueKeys: string[], labelKeys: string[]) {
  return collection(await requestJson(path))
    .map((item) => toOption(item, valueKeys, labelKeys))
    .filter((item): item is FinanceOption => item !== null);
}

export function getFinanceDepartments() {
  return getOptions(
    "api/department",
    ["departmentCode", "DepartmentCode", "code", "Code"],
    ["description", "Description", "departmentName", "DepartmentName", "name", "Name"],
  );
}

export function getFinanceSites(departmentCode?: number) {
  return getOptions(
    `api/site${queryString({ departmentCode })}`,
    ["siteCode", "SiteCode", "code", "Code"],
    ["description", "Description", "siteName", "SiteName", "name", "Name"],
  );
}

export function getFinanceProvinces() {
  return getOptions(
    "api/province",
    ["province_code", "provinceCode", "code", "Code"],
    ["province_name", "provinceName", "name", "Name"],
  );
}

export function getFinanceYears() {
  return getOptions(
    "api/finance/reference/financial-years",
    ["code", "Code", "value", "Value"],
    ["name", "Name", "label", "Label"],
  );
}

function fallbackFinanceYears(): FinanceOption[] {
  const currentYear = new Date().getUTCFullYear();
  return Array.from({ length: 7 }, (_, index) => currentYear - 5 + index).map((year) => ({
    value: String(year),
    label: String(year),
  }));
}

export async function getFinanceTariffYears() {
  try {
    const options = collection(await requestJson("api/finance/tariff-parameters/years"))
      .map((item) => {
        if (typeof item === "number" && Number.isSafeInteger(item))
          return { value: String(item), label: String(item) };
        if (typeof item === "string" && /^\d{4}$/.test(item.trim()))
          return { value: item.trim(), label: item.trim() };
        if (!isRecord(item)) return null;
        const raw = getValue(item, "value", "Value", "code", "Code", "year", "Year");
        const year = typeof raw === "number" ? raw : Number(raw);
        return Number.isSafeInteger(year) ? { value: String(year), label: String(year) } : null;
      })
      .filter((item): item is FinanceOption => item !== null);
    return options.length > 0 ? options : fallbackFinanceYears();
  } catch {
    return fallbackFinanceYears();
  }
}

export type FinanceTariffParameters = {
  year: number;
  isApproved: boolean;
  approvedBy: string | null;
  effectiveDate: string | null;
  parameters: Array<{ parameterName: string; value: number | null; unit: string }>;
  fixedTariffs: Array<{
    classCode: number | null;
    classDescription: string;
    amount: number | null;
    unit: string;
    effectiveDate: string | null;
  }>;
  kiloTariffs: Array<{
    classCode: number | null;
    classDescription: string;
    amount: number | null;
    unit: string;
    effectiveDate: string | null;
  }>;
  maintenanceValues: Array<{
    classCode: number | null;
    classDescription: string;
    monthsAge: number | null;
    kilometerAge: number | null;
    amount: number | null;
    randPerKilometer: number | null;
  }>;
};

function mapTariffClassRow(item: unknown) {
  if (!isRecord(item)) return null;
  return {
    classCode: asNumber(getValue(item, "classCode", "ClassCode")),
    classDescription: asString(getValue(item, "classDescription", "ClassDescription")) ?? "",
    amount: asNumber(getValue(item, "amount", "Amount")),
    unit: asString(getValue(item, "unit", "Unit")) ?? "",
    effectiveDate: asString(getValue(item, "effectiveDate", "EffectiveDate")),
  };
}

export async function getFinanceTariffParameters(year: number): Promise<FinanceTariffParameters> {
  const payload = await requestJson(
    `api/finance/tariff-parameters/${encodeURIComponent(String(year))}`,
  );
  if (!isRecord(payload))
    throw new FinanceApiError(
      "invalid-response",
      "The FIS API returned invalid tariff parameters.",
    );
  const mapRows = (keys: string[]) =>
    collection(getValue(payload, ...keys))
      .map(mapTariffClassRow)
      .filter((item): item is NonNullable<ReturnType<typeof mapTariffClassRow>> => item !== null);
  const parameters = collection(getValue(payload, "parameters", "Parameters"))
    .filter(isRecord)
    .map((item) => ({
      parameterName: asString(getValue(item, "parameterName", "ParameterName")) ?? "",
      value: asNumber(getValue(item, "value", "Value")),
      unit: asString(getValue(item, "unit", "Unit")) ?? "",
    }));
  const maintenanceValues = collection(getValue(payload, "maintenanceValues", "MaintenanceValues"))
    .filter(isRecord)
    .map((item) => ({
      classCode: asNumber(getValue(item, "classCode", "ClassCode")),
      classDescription: asString(getValue(item, "classDescription", "ClassDescription")) ?? "",
      monthsAge: asNumber(getValue(item, "monthsAge", "MonthsAge")),
      kilometerAge: asNumber(getValue(item, "kilometerAge", "KilometerAge")),
      amount: asNumber(getValue(item, "amount", "Amount")),
      randPerKilometer: asNumber(getValue(item, "randPerKilometer", "RandPerKilometer")),
    }));
  return {
    year: asNumber(getValue(payload, "year", "Year")) ?? year,
    isApproved: asBoolean(getValue(payload, "isApproved", "IsApproved", "is_approved")),
    approvedBy: asString(getValue(payload, "approvedBy", "ApprovedBy", "approved_by")),
    effectiveDate: asString(getValue(payload, "effectiveDate", "EffectiveDate", "effective_date")),
    parameters,
    fixedTariffs: mapRows(["fixedTariffs", "FixedTariffs"]),
    kiloTariffs: mapRows(["kiloTariffs", "KiloTariffs"]),
    maintenanceValues,
  };
}

function formatBatchDate(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat("en-ZA", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    timeZone: "UTC",
  }).format(date);
}

function fallbackBatchDates(): FinanceOption[] {
  const current = new Date();
  const options: FinanceOption[] = [];
  for (let offset = 0; offset < 12; offset += 1) {
    const month = new Date(Date.UTC(current.getUTCFullYear(), current.getUTCMonth() - offset, 1));
    const value = month.toISOString().slice(0, 10);
    options.push({
      value,
      label: new Intl.DateTimeFormat("en-ZA", {
        month: "short",
        year: "numeric",
        timeZone: "UTC",
      }).format(month),
    });
  }
  return options;
}

export async function getFinanceBatchDates() {
  try {
    const options = collection(await requestJson("api/finance/reference/batch-dates"))
      .map((item) => {
        if (typeof item === "string") return { value: item, label: formatBatchDate(item) };
        if (!isRecord(item)) return null;
        const raw = getValue(item, "value", "Value", "date", "Date", "batchDate", "BatchDate");
        if (typeof raw !== "string" || !raw.trim()) return null;
        return { value: raw, label: formatBatchDate(raw) };
      })
      .filter((item): item is FinanceOption => item !== null);
    return options.length > 0 ? options : fallbackBatchDates();
  } catch {
    return fallbackBatchDates();
  }
}

export function getFinanceSegmentTypes() {
  return getOptions(
    "api/finance/reference/segment-types",
    ["value", "Value", "code", "Code", "id", "Id"],
    ["label", "Label", "name", "Name", "description", "Description"],
  );
}

export async function getFinancePostingMonths(filterBy = "Department") {
  const payload = await requestJson(
    `api/finance/reports/posting-months${queryString({ filterBy })}`,
  );
  const values = isRecord(payload) ? getValue(payload, "months", "Months") : payload;
  return collection(values)
    .map((item) =>
      toOption(
        item,
        ["value", "Value", "posting_month_code", "postingMonthCode"],
        ["label", "Label", "month_name", "monthName"],
      ),
    )
    .filter((item): item is FinanceOption => item !== null);
}

function mapBasSegment(value: unknown): BasSegment | null {
  if (!isRecord(value)) return null;
  const segmentCode = asNumber(getValue(value, "segmentCode", "SegmentCode", "segment_code"));
  if (segmentCode === null) return null;
  return {
    segmentCode,
    segmentType: asString(getValue(value, "segmentType", "SegmentType")) ?? "",
    segmentValue: asString(getValue(value, "segmentValue", "SegmentValue")) ?? "",
    departmentCode: asNumber(
      getValue(value, "departmentCode", "DepartmentCode", "department_code"),
    ),
    isActive: asBoolean(getValue(value, "isActive", "IsActive")),
  };
}

export async function getBasSegments(departmentCode?: number, segmentType?: string) {
  return collection(
    await requestJson(`api/finance/bas/segments${queryString({ departmentCode, segmentType })}`),
  )
    .map(mapBasSegment)
    .filter((item): item is BasSegment => item !== null);
}

export async function getBasRows(path: string) {
  return collection(await requestJson(path))
    .filter(isRecord)
    .map((item) => {
      const row: FinanceRow = {};
      for (const [key, value] of Object.entries(item)) {
        row[key] =
          typeof value === "string" ||
          typeof value === "number" ||
          typeof value === "boolean" ||
          value === null
            ? value
            : JSON.stringify(value);
      }
      return row;
    });
}

export async function importBas(fileData: string, departmentCode?: number) {
  return requestJson("api/finance/bas/import", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ fileData, departmentCode }),
  });
}

export async function activateBasSegments(segmentCodes: number[]) {
  return requestJson("api/finance/bas/segments/activate", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ segmentCodes }),
  });
}

export async function importStandardBankFile(file: Blob, filename: string) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader)
    throw new FinanceApiError("unauthorized", "No FIS access cookie is available.");
  const formData = new FormData();
  formData.append("file", file, filename);
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  try {
    const response = await fetch(new URL("api/finance/standard-bank/import", getApiBaseUrl()), {
      method: "POST",
      cache: "no-store",
      headers: { accept: "application/json", cookie: cookieHeader },
      body: formData,
      signal: controller.signal,
    });
    if (response.status === 401 || response.status === 403)
      throw new FinanceApiError(
        "unauthorized",
        "The FIS access cookie was rejected.",
        response.status,
      );
    if (!response.ok)
      throw new FinanceApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        `FIS API returned HTTP ${response.status}.`,
        response.status,
      );
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

export function runFinanceAction(path: string, body: unknown = {}) {
  return requestJson(path, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(body),
  });
}
