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

async function requestJson(path: string) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) throw new FinanceApiError("unauthorized", "No FIS access cookie is available.");

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      cache: "no-store",
      headers: { accept: "application/json", cookie: cookieHeader },
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
