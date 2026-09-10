import "server-only";

import {
  FinanceApiError,
  getFinanceJson,
  runFinanceAction,
  type FinanceRow,
} from "@/lib/api/finance/api-finance";

export type FinanceReport = {
  title: string;
  rows: FinanceRow[];
  supportsDateFilter: boolean | null;
};

export type DedicatedFinanceReport = {
  endpoint: string;
  defaultFormat: "json" | "html" | "csv";
};

export type DedicatedFinanceReportParams = {
  id: number;
  postingMonthCode: number;
  filterBy: "Department" | "Site";
  format: "json" | "html" | "csv";
};

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function value(record: Record<string, unknown>, ...keys: string[]) {
  for (const key of keys) if (key in record) return record[key];
  return undefined;
}

function rowValue(item: unknown): FinanceRow | null {
  if (!isRecord(item)) return null;
  const row: FinanceRow = {};
  for (const [key, itemValue] of Object.entries(item)) {
    row[key] =
      typeof itemValue === "string" ||
      typeof itemValue === "number" ||
      typeof itemValue === "boolean" ||
      itemValue === null
        ? itemValue
        : JSON.stringify(itemValue);
  }
  return row;
}

function rowsFrom(valueToMap: unknown) {
  if (Array.isArray(valueToMap))
    return valueToMap.map(rowValue).filter((row): row is FinanceRow => row !== null);
  if (!isRecord(valueToMap)) return [];
  const rows = value(valueToMap, "dataRows", "DataRows", "rows", "Rows", "items", "Items");
  return Array.isArray(rows)
    ? rows.map(rowValue).filter((row): row is FinanceRow => row !== null)
    : [];
}

export function mapFinanceReport(payload: unknown, fallbackTitle: string): FinanceReport {
  const rows = rowsFrom(payload);
  const record = isRecord(payload) ? payload : null;
  const title =
    record && typeof value(record, "title", "Title") === "string"
      ? String(value(record, "title", "Title"))
      : fallbackTitle;
  const supportsDateFilter = record
    ? typeof value(record, "supportsDateFilter", "SupportsDateFilter") === "boolean"
      ? Boolean(value(record, "supportsDateFilter", "SupportsDateFilter"))
      : null
    : null;
  return { title, rows, supportsDateFilter };
}

export async function getUniversalFinanceReport(input: {
  mode: string;
  action: string;
  departmentCode?: string;
  siteCode?: string;
  province?: string;
  financialYear?: string;
  batchDate?: string;
  vmfCode?: number;
  startDate?: string;
  endDate?: string;
}) {
  const payload = await runFinanceAction("api/report/finance/reports", {
    reportType: "financial",
    mode: input.mode,
    action: input.action,
    departmentCode: input.departmentCode ?? "",
    siteCode: input.siteCode ?? "",
    province: input.province ?? "",
    financialYear: input.financialYear ?? "",
    batchDate: input.batchDate ?? "",
    vmfCode: input.vmfCode,
    startDate: input.startDate || undefined,
    endDate: input.endDate || undefined,
  });
  return mapFinanceReport(payload, `${input.action} results`);
}

export async function getBillingHistory(vmfCode: number, financialYear: number) {
  const payload = await getFinanceJson(
    `api/report/billing/history/${vmfCode}?financialYear=${financialYear}`,
  );
  return mapFinanceReport(payload, "Vehicle billing history");
}

export async function getReversalTree(journalNumber: string) {
  const payload = await getFinanceJson(
    `api/finance/reports/reversals-tree/${encodeURIComponent(journalNumber)}`,
  );
  return mapFinanceReport(payload, "Reversals tree");
}

export async function getAuditFinanceReport(input: {
  mode: string;
  auditType: string;
  outputFormat: "pdf" | "excel";
  departmentCode?: string;
  siteCode?: string;
  vmfCode?: number;
  startDate?: string;
  endDate?: string;
}) {
  const payload = await runFinanceAction("api/report/finance/audit-trail", {
    mode: input.mode,
    auditType: input.auditType,
    outputFormat: input.outputFormat,
    departmentCode: input.departmentCode ?? "",
    siteCode: input.siteCode ?? "",
    vmfCode: input.vmfCode,
    startDate: input.startDate || undefined,
    endDate: input.endDate || undefined,
  });
  return mapFinanceReport(payload, `${input.auditType} audit trail`);
}

export async function getWesbankFinanceReport(input: {
  mode: string;
  provinceCode?: string;
  startDate?: string;
  endDate?: string;
}) {
  const payload = await runFinanceAction("api/report/finance/wesbank", {
    mode: input.mode,
    provinceCode: input.provinceCode ?? "",
    startDate: input.startDate || undefined,
    endDate: input.endDate || undefined,
  });
  const report = mapFinanceReport(payload, `${input.mode} Wesbank expenses`);
  return { ...report, title: `${input.mode} Wesbank expenses` };
}

export async function getRegionalFinanceReport(input: {
  mode: string;
  summaryType: string;
  provinceCode?: string;
  startDate?: string;
  endDate?: string;
}) {
  const payload = await runFinanceAction("api/report/finance/regional", {
    reportType: "financial",
    mode: input.mode,
    provinceCode: input.provinceCode ?? "",
    summaryType: input.summaryType,
    startDate: input.startDate || undefined,
    endDate: input.endDate || undefined,
  });
  const report = mapFinanceReport(payload, `${input.summaryType} regional finance`);
  return { ...report, title: `${input.summaryType} regional finance` };
}

export async function getMissingKilometresFinanceReport(input: {
  mode: string;
  departmentCode?: string;
  provinceCode?: string;
  financialYear?: string;
  excludeUnposted?: boolean;
  startDate?: string;
  endDate?: string;
}) {
  const payload = await runFinanceAction("api/report/finance/missing-kilometres", {
    mode: input.mode,
    departmentCode: input.departmentCode ?? "",
    provinceCode: input.provinceCode ?? "",
    financialYear: input.financialYear ?? "",
    excludeUnposted: input.excludeUnposted ?? false,
    startDate: input.startDate || undefined,
    endDate: input.endDate || undefined,
  });
  return mapFinanceReport(payload, `${input.mode} missing kilometres`);
}

export const WESBANK_REPORT_ACTIONS = [
  "summary-all",
  "department-summary",
  "site-summary",
  "summary-all-download",
  "department-summary-download",
  "site-summary-download",
  "summary-province",
  "department-province-summary",
  "site-province-summary",
  "summary-province-download",
  "department-province-summary-download",
  "site-province-summary-download",
  "detailed-fuel-download",
  "detailed-other-download",
  "detailed-fuel-province-download",
  "detailed-other-province-download",
] as const;

export const REGIONAL_SUMMARY_ACTIONS = [
  "summary",
  "department-cost-type",
  "site-cost-type",
  "summary-download",
  "department-cost-type-download",
  "site-cost-type-download",
] as const;

const DEDICATED_REPORTS: Record<string, DedicatedFinanceReport> = {
  "summary-by-cost-type": { endpoint: "invoice-summary", defaultFormat: "html" },
  "summary-by-cost-type-journal": { endpoint: "invoice-by-cost-type", defaultFormat: "html" },
  "summary-html": { endpoint: "invoice-summary", defaultFormat: "html" },
  "summary-pdf": { endpoint: "invoice-summary", defaultFormat: "html" },
  "detailed-html": { endpoint: "invoice-detailed", defaultFormat: "html" },
  "detailed-pdf": { endpoint: "invoice-detailed", defaultFormat: "html" },
  "detailed-table": { endpoint: "invoice-detailed", defaultFormat: "json" },
  "detailed-excel": { endpoint: "invoice-detailed", defaultFormat: "csv" },
  "detailed-vip-taxi": { endpoint: "taxi-vip", defaultFormat: "html" },
  "detailed-vip-taxi-pdf": { endpoint: "taxi-vip", defaultFormat: "html" },
  "detailed-fuel-pdf": { endpoint: "fuel", defaultFormat: "html" },
  "detailed-fuel-excel": { endpoint: "fuel", defaultFormat: "csv" },
  "detailed-toll-oil-pdf": { endpoint: "toll-oil", defaultFormat: "html" },
  "detailed-toll-oil-excel": { endpoint: "toll-oil", defaultFormat: "csv" },
  "detailed-surcharge-pdf": { endpoint: "surcharge", defaultFormat: "html" },
  "detailed-surcharge-excel": { endpoint: "surcharge", defaultFormat: "csv" },
};

export function getDedicatedFinanceReport(action: string) {
  return DEDICATED_REPORTS[action] ?? null;
}

export function dedicatedFinanceReportPath(action: string, params: DedicatedFinanceReportParams) {
  const report = getDedicatedFinanceReport(action);
  if (!report)
    throw new FinanceApiError("invalid-response", "The requested Finance report is not available.");
  const query = new URLSearchParams({
    id: String(params.id),
    postingMonthCode: String(params.postingMonthCode),
    filterBy: params.filterBy,
    format: params.format,
  });
  return `api/finance/reports/${report.endpoint}?${query.toString()}`;
}

export async function getDedicatedFinanceReportData(
  action: string,
  params: Omit<DedicatedFinanceReportParams, "format">,
) {
  const path = dedicatedFinanceReportPath(action, { ...params, format: "json" });
  return mapFinanceReport(await getFinanceJson(path), `${action} results`);
}

export const FINANCE_REPORT_ACTIONS = [
  "summary-by-cost-type",
  "summary-by-cost-type-journal",
  "summary-html",
  "summary-pdf",
  "detailed-html",
  "detailed-pdf",
  "detailed-table",
  "detailed-excel",
  "detailed-vip-taxi",
  "detailed-vip-taxi-pdf",
  "detailed-fuel-pdf",
  "detailed-fuel-excel",
  "detailed-toll-oil-pdf",
  "detailed-toll-oil-excel",
  "detailed-surcharge-pdf",
  "detailed-surcharge-excel",
] as const;
