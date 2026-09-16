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

export type LegacyVehicleStatusOption = {
  id: number;
  description: string;
};

export type DedicatedFinanceReport = {
  endpoint: string;
  defaultFormat: "json" | "html" | "csv";
  title: string;
};

export type DedicatedFinanceReportParams = {
  id: number;
  batchDate: string;
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
  const reportedTitle =
    record && typeof value(record, "title", "Title") === "string"
      ? String(value(record, "title", "Title")).trim()
      : "";
  // A legacy Finance action has its own report definition and title. The API
  // returns an explicit dependency error when that definition is unavailable,
  // rather than returning a generic report under a misleading heading.
  const title = reportedTitle || fallbackTitle;
  const supportsDateFilter = record
    ? typeof value(record, "supportsDateFilter", "SupportsDateFilter") === "boolean"
      ? Boolean(value(record, "supportsDateFilter", "SupportsDateFilter"))
      : null
    : null;
  return { title, rows, supportsDateFilter };
}

/**
 * Executes one of the fixed FinanceMain.aspx report actions. The API maps the
 * action to an allow-listed legacy procedure; this is not the generic report
 * builder used elsewhere in the application.
 */
export async function getFinanceMenuReport(input: {
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

export async function getBillingHistory(registrationNumber: string, financialYear: number) {
  const payload = await getFinanceJson(
    `api/report/billing/history?registrationNumber=${encodeURIComponent(registrationNumber)}&financialYear=${financialYear}`,
  );
  return mapFinanceReport(payload, "Vehicle billing history");
}

export async function getLegacyFinanceDetailReport(input: {
  item: string;
  startDate?: string;
  endDate?: string;
  departmentCode?: number;
  siteCode?: number;
  province?: string;
  journalNumber?: number;
  registrationNumber?: string;
  statusId?: number;
}) {
  const query = new URLSearchParams({ item: input.item });
  if (input.startDate) query.set("startDate", input.startDate);
  if (input.endDate) query.set("endDate", input.endDate);
  if (input.departmentCode) query.set("departmentCode", String(input.departmentCode));
  if (input.siteCode) query.set("siteCode", String(input.siteCode));
  if (input.province) query.set("province", input.province);
  if (input.journalNumber) query.set("journalNumber", String(input.journalNumber));
  if (input.registrationNumber) query.set("registrationNumber", input.registrationNumber);
  if (input.statusId) query.set("statusId", String(input.statusId));
  const payload = await getFinanceJson(`api/report/finance/legacy-detail?${query.toString()}`);
  return mapFinanceReport(payload, "Legacy Finance detail report");
}

export async function getLegacyVehicleStatusOptions(): Promise<LegacyVehicleStatusOption[]> {
  const payload = await getFinanceJson("api/report/vehicle-status-options");
  if (!Array.isArray(payload)) {
    throw new FinanceApiError(
      "invalid-response",
      "The FIS API returned invalid vehicle status options.",
    );
  }

  return payload
    .map((item) => {
      if (!isRecord(item)) return null;
      const id = Number(value(item, "id", "Id"));
      const description = value(item, "description", "Description");
      return Number.isSafeInteger(id) &&
        id >= 0 &&
        typeof description === "string" &&
        description.trim()
        ? { id, description: description.trim() }
        : null;
    })
    .filter((item): item is LegacyVehicleStatusOption => item !== null);
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
  vehicleNumber?: string;
  numberType?: "gg" | "gp";
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
    vehicleNumber: input.vehicleNumber ?? "",
    numberType: input.numberType ?? "gp",
    startDate: input.startDate || undefined,
    endDate: input.endDate || undefined,
  });
  return mapFinanceReport(payload, `${input.auditType} audit trail`);
}

export async function getWesbankFinanceReport(input: {
  mode: string;
  reportAction: string;
  provinceCode?: string;
  startDate?: string;
  endDate?: string;
}) {
  const payload = await runFinanceAction("api/report/finance/wesbank", {
    mode: input.mode,
    reportAction: input.reportAction,
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
  if (input.mode === "kilo-gaps-pdf" || input.mode === "kilo-gaps-xls") {
    const financialYear = input.financialYear?.trim();
    if (!financialYear)
      throw new FinanceApiError("invalid-response", "A financial year is required.");
    const payload = await getFinanceJson(
      `api/finance/missing-kilometres/kilo-gaps?financialYear=${encodeURIComponent(financialYear)}`,
    );
    return mapFinanceReport(payload, "Missing Kilometres Report");
  }

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
  "summary-by-cost-type": {
    endpoint: "invoice-by-cost-type",
    defaultFormat: "html",
    title: "Summarised Invoice",
  },
  "summary-by-cost-type-journal": {
    endpoint: "invoice-by-cost-type",
    defaultFormat: "html",
    title: "Invoice by Cost Type, Site and Journal",
  },
  "summary-html": {
    endpoint: "invoice-summary",
    defaultFormat: "html",
    title: "Summarised Invoice",
  },
  "summary-pdf": {
    endpoint: "invoice-summary",
    defaultFormat: "html",
    title: "Summarised Invoice",
  },
  "detailed-html": {
    endpoint: "invoice-detailed",
    defaultFormat: "html",
    title: "Detailed Invoice",
  },
  "detailed-pdf": {
    endpoint: "invoice-detailed",
    defaultFormat: "html",
    title: "Detailed Invoice",
  },
  "detailed-table": {
    endpoint: "invoice-detailed",
    defaultFormat: "json",
    title: "Detailed Invoice",
  },
  "detailed-excel": {
    endpoint: "invoice-detailed",
    defaultFormat: "csv",
    title: "Detailed Invoice",
  },
  "detailed-vip-taxi": {
    endpoint: "taxi-vip",
    defaultFormat: "html",
    title: "Detailed VIP and Taxi Invoice",
  },
  "detailed-vip-taxi-pdf": {
    endpoint: "taxi-vip",
    defaultFormat: "html",
    title: "Detailed VIP and Taxi Invoice",
  },
  "detailed-fuel-pdf": {
    endpoint: "fuel",
    defaultFormat: "html",
    title: "Detailed Fuel Invoice",
  },
  "detailed-fuel-excel": {
    endpoint: "fuel",
    defaultFormat: "csv",
    title: "Detailed Fuel Invoice",
  },
  "detailed-toll-oil-pdf": {
    endpoint: "toll-oil",
    defaultFormat: "html",
    title: "Detailed Toll and Oil Invoice",
  },
  "detailed-toll-oil-excel": {
    endpoint: "toll-oil",
    defaultFormat: "csv",
    title: "Detailed Toll and Oil Invoice",
  },
  "detailed-surcharge-pdf": {
    endpoint: "surcharge",
    defaultFormat: "html",
    title: "Detailed Surcharge Invoice",
  },
  "detailed-surcharge-excel": {
    endpoint: "surcharge",
    defaultFormat: "csv",
    title: "Detailed Surcharge Invoice",
  },
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
    batchDate: params.batchDate,
    filterBy: params.filterBy,
    format: params.format,
  });
  return `api/finance/reports/${report.endpoint}?${query.toString()}`;
}

export async function getDedicatedFinanceReportData(
  action: string,
  params: Omit<DedicatedFinanceReportParams, "format">,
) {
  const report = getDedicatedFinanceReport(action);
  if (!report)
    throw new FinanceApiError("invalid-response", "The requested Finance report is not available.");
  const path = dedicatedFinanceReportPath(action, { ...params, format: "json" });
  return mapFinanceReport(await getFinanceJson(path), report.title);
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
