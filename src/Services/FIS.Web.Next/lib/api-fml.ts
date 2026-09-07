import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type LeaseTermRecord = {
  termId: number;
  vmfCode: number;
  agreedTerms: number | null;
  agreedKilos: number | null;
  appliedInterest: number | null;
  fixedMonthlyAmount: number | null;
  authorityStatus: number | null;
  createdBy: number | null;
  createdDate: string | null;
  modifiedBy: number | null;
  modifiedDate: string | null;
  startDate: string | null;
  endDate: string | null;
  agreedOverallKilo: number | null;
  excessKilosTariff: number | null;
  relieveVehicle: boolean | null;
  leaseSiteCode: number | null;
  comments: string | null;
  rejected: number | null;
  authorisedBy: number | null;
  authorisedDate: string | null;
  authorityComment: string | null;
  rejectionReason: string | null;
  leaseStatus: string | null;
  dateCreated: string | null;
  dateUpdated: string | null;
  createdByUserCode: number | null;
  modifiedByUserCode: number | null;
  isDeleted: boolean;
};

export type LeaseTermWriteInput = {
  vmf_Code: number;
  AgreedTerms: number | null;
  AgreedKilos: number | null;
  AppliedInterest: number | null;
  FixedMonthlyAmount: number | null;
  AuthorityStatus: number;
  StartDate: string | null;
  EndDate: string | null;
  AgreedOverallKilo?: number | null;
  ExcessKilosTarrif?: number | null;
  RelieveVehicle?: boolean | null;
  lease_site_code?: number | null;
  authority_comment?: string | null;
  rejection_reason?: string | null;
  lease_notes?: string | null;
};

export type LeaseTariffRecord = {
  leaseTariffCode: number;
  vmfCode: number;
  startDate: string;
  endDate: string;
  fixedTariff: number;
  excessKiloTariff: number | null;
  active: boolean;
  dateCreated: string | null;
  dateUpdated: string | null;
  isDeleted: boolean;
};

export type LeaseTariffWriteInput = {
  vmf_code: number;
  start_date: string;
  end_date: string;
  fixed_tariff: number;
  excess_kilo_tariff: number | null;
  active: boolean;
};

export type LeaseTariffImportInput = {
  vmfCode: number;
  ggNumber: string | null;
  gpNumber: string | null;
  startDate: string;
  endDate: string;
  fixedTariff: number;
  excessKiloTariff: number | null;
};

export type FmlMaintenanceHistoryRow = {
  ggNumber: string | null;
  yearManufactured: number | null;
  modelDescription: string | null;
  currentStatus: string | null;
  currentStatusDate: string | null;
  hiredFrom: string | null;
  maintenanceExpenseType: string | null;
  totalCostOverDateRange: number | null;
};

export type FmlContractRow = {
  rowNumber: number | null;
  ggNumber: string | null;
  gpNumber: string | null;
  model: string | null;
  yearModel: number | null;
  hiredFrom: string | null;
  hireType: string | null;
  stillCurrent: string | null;
  contractStartDate: string | null;
  targetReturnDate: string | null;
  contractType: string | null;
  siteName: string | null;
  fixedTariff: number | null;
};

export type FmlNoContractRow = {
  vehicleCounter: number | null;
  ggNumber: string | null;
  registrationNumber: string | null;
  hiredFrom: string | null;
  vehicleStatus: string | null;
  location: string | null;
  yearModel: number | null;
  modelDescription: string | null;
  classDescription: string | null;
  purchaseAmount: number | null;
};

export type FmlOverUtilizedRow = {
  vehicleCounter: number | null;
  ggNumber: string | null;
  gpNumber: string | null;
  hiredFrom: string | null;
  month: string | null;
  maxOdoMeter: number | null;
  minOdoMeter: number | null;
  actualKilos: number | null;
  agreedKilos: number | null;
  excessKilos: number | null;
  agreedOverallKilo: number | null;
  agreedTerms: number | null;
  actualTerm: number | null;
  totalKilos: number | null;
  totalExcessKilos: number | null;
  averageMonthlyKilos: number | null;
  projectedEndMonth: string | null;
  projectedEndDate: string | null;
  yearModel: number | null;
  modelDescription: string | null;
  purchaseAmount: number | null;
};

export type FmlReportQuery = {
  startDate?: string;
  endDate?: string;
  finYear?: string;
  ggNum?: string;
};

export type FmlApiErrorReason = "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class FmlApiError extends Error {
  constructor(public readonly reason: FmlApiErrorReason, message: string) {
    super(message);
    this.name = "FmlApiError";
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
  return ["true", "1", "yes", "y"].includes(String(value ?? "").trim().toLowerCase());
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
  if (!cookieHeader) throw new FmlApiError("unauthorized", "No FIS access cookie is available.");

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

    if (response.status === 401 || response.status === 403) {
      throw new FmlApiError("unauthorized", "The FIS access cookie was rejected.");
    }
    if (response.status === 404) throw new FmlApiError("not-found", "The requested FML record was not found.");
    if (!response.ok) throw new FmlApiError("unavailable", `FIS API returned HTTP ${response.status}.`);
    try {
      return (await response.json()) as unknown;
    } catch {
      throw new FmlApiError("invalid-response", "The FIS API returned invalid JSON.");
    }
  } catch (error) {
    if (error instanceof FmlApiError) throw error;
    throw new FmlApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

function mapTerm(value: unknown): LeaseTermRecord | null {
  if (!isRecord(value)) return null;
  const termId = asNumber(getValue(value, "VehicleContractTermID", "vehicleContractTermID", "leasecontract_code"));
  const vmfCode = asNumber(getValue(value, "vmf_Code", "vmf_code", "vmfCode"));
  if (termId === null || vmfCode === null) return null;

  return {
    termId,
    vmfCode,
    agreedTerms: asNumber(getValue(value, "AgreedTerms", "agreedTerms")),
    agreedKilos: asNumber(getValue(value, "AgreedKilos", "agreedKilos")),
    appliedInterest: asNumber(getValue(value, "AppliedInterest", "appliedInterest")),
    fixedMonthlyAmount: asNumber(getValue(value, "FixedMonthlyAmount", "fixedMonthlyAmount")),
    authorityStatus: asNumber(getValue(value, "AuthorityStatus", "authorityStatus")),
    createdBy: asNumber(getValue(value, "CreatedBy", "createdBy")),
    createdDate: asString(getValue(value, "CreatedDate", "createdDate")),
    modifiedBy: asNumber(getValue(value, "ModifiedBy", "modifiedBy")),
    modifiedDate: asString(getValue(value, "ModifiedDate", "modifiedDate")),
    startDate: asString(getValue(value, "StartDate", "startDate", "lease_startdate")),
    endDate: asString(getValue(value, "EndDate", "endDate", "lease_enddate")),
    agreedOverallKilo: asNumber(getValue(value, "AgreedOverallKilo", "agreedOverallKilo")),
    excessKilosTariff: asNumber(getValue(value, "ExcessKilosTarrif", "excessKilosTarrif")),
    relieveVehicle: getValue(value, "RelieveVehicle", "relieveVehicle") === undefined ? null : asBoolean(getValue(value, "RelieveVehicle", "relieveVehicle")),
    leaseSiteCode: asNumber(getValue(value, "lease_site_code", "leaseSiteCode")),
    comments: asString(getValue(value, "Comments", "comments")),
    rejected: asNumber(getValue(value, "Rejected", "rejected")),
    authorisedBy: asNumber(getValue(value, "AuthorisedBy", "authorisedBy")),
    authorisedDate: asString(getValue(value, "AuthorisedDate", "authorisedDate")),
    authorityComment: asString(getValue(value, "authority_comment", "authorityComment")),
    rejectionReason: asString(getValue(value, "rejection_reason", "rejectionReason")),
    leaseStatus: asString(getValue(value, "lease_status", "leaseStatus")),
    dateCreated: asString(getValue(value, "date_created", "dateCreated")),
    dateUpdated: asString(getValue(value, "date_updated", "dateUpdated")),
    createdByUserCode: asNumber(getValue(value, "created_by_user_code", "createdByUserCode")),
    modifiedByUserCode: asNumber(getValue(value, "modified_by_user_code", "modifiedByUserCode")),
    isDeleted: asBoolean(getValue(value, "is_deleted", "isDeleted")),
  };
}

function mapTariff(value: unknown): LeaseTariffRecord | null {
  if (!isRecord(value)) return null;
  const leaseTariffCode = asNumber(getValue(value, "lease_tariff_code", "leaseTariffCode"));
  const vmfCode = asNumber(getValue(value, "vmf_code", "vmfCode"));
  const startDate = asString(getValue(value, "start_date", "startDate"));
  const endDate = asString(getValue(value, "end_date", "endDate"));
  if (leaseTariffCode === null || vmfCode === null || !startDate || !endDate) return null;
  return {
    leaseTariffCode,
    vmfCode,
    startDate,
    endDate,
    fixedTariff: asNumber(getValue(value, "fixed_tariff", "fixedTariff")) ?? 0,
    excessKiloTariff: asNumber(getValue(value, "excess_kilo_tariff", "excessKiloTariff")),
    active: asBoolean(getValue(value, "active")),
    dateCreated: asString(getValue(value, "date_created", "dateCreated")),
    dateUpdated: asString(getValue(value, "date_updated", "dateUpdated")),
    isDeleted: asBoolean(getValue(value, "is_deleted", "isDeleted")),
  };
}

export async function getLeaseTerms() {
  const payload = await requestApi("api/leasecontractterms");
  return getCollection(payload).map(mapTerm).filter((value): value is LeaseTermRecord => value !== null);
}

export async function getLeaseTerm(termId: number) {
  const payload = await requestApi(`api/leasecontractterms/${termId}`);
  const term = mapTerm(payload);
  if (!term) throw new FmlApiError("invalid-response", "The FIS API returned an unexpected lease term.");
  return term;
}

export async function createLeaseTerm(input: LeaseTermWriteInput) {
  const payload = await requestApi("api/leasecontractterms", { method: "POST", body: JSON.stringify(input) });
  return mapTerm(payload);
}

export async function updateLeaseTerm(termId: number, input: LeaseTermWriteInput) {
  const payload = await requestApi(`api/leasecontractterms/${termId}`, {
    method: "PUT",
    body: JSON.stringify({ VehicleContractTermID: termId, ...input }),
  });
  return mapTerm(payload);
}

export async function getLeaseTariffs(vmfCode: number) {
  const payload = await requestApi(`api/lease-tariffs/vehicle/${vmfCode}`);
  return getCollection(payload).map(mapTariff).filter((value): value is LeaseTariffRecord => value !== null);
}

export async function getLatestLeaseTariff(vmfCode: number) {
  const payload = await requestApi(`api/lease-tariffs/vehicle/${vmfCode}/latest`);
  return mapTariff(payload);
}

export async function createLeaseTariff(input: LeaseTariffWriteInput) {
  const payload = await requestApi("api/lease-tariffs", { method: "POST", body: JSON.stringify(input) });
  return mapTariff(payload);
}

export async function importLeaseTariffs(rows: LeaseTariffImportInput[]) {
  const payload = await requestApi("api/lease-tariffs/import", { method: "POST", body: JSON.stringify(rows) });
  if (!isRecord(payload)) throw new FmlApiError("invalid-response", "The FIS API returned an unexpected tariff import result.");
  return {
    imported: asNumber(getValue(payload, "Imported", "imported")) ?? 0,
    failed: asNumber(getValue(payload, "Failed", "failed")) ?? 0,
  };
}

export async function updateLeaseTariff(code: number, input: LeaseTariffWriteInput) {
  const payload = await requestApi(`api/lease-tariffs/${code}`, {
    method: "PUT",
    body: JSON.stringify({ lease_tariff_code: code, ...input }),
  });
  return mapTariff(payload);
}

function reportRows(payload: unknown, key: string) {
  if (!isRecord(payload)) return [];
  const rows = getValue(payload, key, key[0].toLowerCase() + key.slice(1));
  return Array.isArray(rows) ? rows.filter(isRecord) : [];
}

function reportQuery(query: FmlReportQuery) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value?.trim()) params.set(key, value.trim());
  }
  const value = params.toString();
  return value ? `?${value}` : "";
}

export async function getFmlMaintenanceHistory(query: FmlReportQuery = {}) {
  const payload = await requestApi(`api/report/fml/maintenance-history${reportQuery(query)}`);
  const records = reportRows(payload, "Records").map((row) => ({
    ggNumber: asString(getValue(row, "GgNumber", "ggNumber")),
    yearManufactured: asNumber(getValue(row, "YearManufactured", "yearManufactured")),
    modelDescription: asString(getValue(row, "ModelDescription", "modelDescription")),
    currentStatus: asString(getValue(row, "CurrentStatus", "currentStatus")),
    currentStatusDate: asString(getValue(row, "CurrentStatusDate", "currentStatusDate")),
    hiredFrom: asString(getValue(row, "HiredFrom", "hiredFrom")),
    maintenanceExpenseType: asString(getValue(row, "MaintenanceExpenseType", "maintenanceExpenseType")),
    totalCostOverDateRange: asNumber(getValue(row, "TotalCostOverDateRange", "totalCostOverDateRange")),
  } satisfies FmlMaintenanceHistoryRow));
  return {
    records,
    grandTotal: isRecord(payload) ? asNumber(getValue(payload, "GrandTotal", "grandTotal")) : null,
    totalCount: isRecord(payload) ? asNumber(getValue(payload, "TotalCount", "totalCount")) ?? records.length : records.length,
  };
}

export async function getFmlContractsExpiring() {
  return getFmlContractReport("contracts-expiring");
}

export async function getFmlExpiredOpenContracts() {
  return getFmlContractReport("expired-open");
}

async function getFmlContractReport(path: string) {
  const payload = await requestApi(`api/report/fml/${path}`);
  const contracts = reportRows(payload, "Contracts").map((row) => ({
    rowNumber: asNumber(getValue(row, "RowNumber", "rowNumber")),
    ggNumber: asString(getValue(row, "GgNumber", "ggNumber")),
    gpNumber: asString(getValue(row, "GpNumber", "gpNumber")),
    model: asString(getValue(row, "Model", "model")),
    yearModel: asNumber(getValue(row, "YearModel", "yearModel")),
    hiredFrom: asString(getValue(row, "HiredFrom", "hiredFrom")),
    hireType: asString(getValue(row, "HireType", "hireType")),
    stillCurrent: asString(getValue(row, "StillCurrent", "stillCurrent")),
    contractStartDate: asString(getValue(row, "ContractStartDate", "contractStartDate")),
    targetReturnDate: asString(getValue(row, "TargetReturnDate", "targetReturnDate")),
    contractType: asString(getValue(row, "ContractType", "contractType")),
    siteName: asString(getValue(row, "SiteName", "siteName")),
    fixedTariff: asNumber(getValue(row, "FixedTariff", "fixedTariff")),
  } satisfies FmlContractRow));
  return { contracts, totalCount: isRecord(payload) ? asNumber(getValue(payload, "TotalCount", "totalCount")) ?? contracts.length : contracts.length };
}

export async function getFmlVehiclesNoContracts() {
  const payload = await requestApi("api/report/fml/vehicles-no-contracts");
  const vehicles = reportRows(payload, "Vehicles").map((row) => ({
    vehicleCounter: asNumber(getValue(row, "VehicleCounter", "vehicleCounter")),
    ggNumber: asString(getValue(row, "GgNumber", "ggNumber")),
    registrationNumber: asString(getValue(row, "RegistrationNumber", "registrationNumber")),
    hiredFrom: asString(getValue(row, "HiredFrom", "hiredFrom")),
    vehicleStatus: asString(getValue(row, "VehicleStatus", "vehicleStatus")),
    location: asString(getValue(row, "Location", "location")),
    yearModel: asNumber(getValue(row, "YearModel", "yearModel")),
    modelDescription: asString(getValue(row, "ModelDescription", "modelDescription")),
    classDescription: asString(getValue(row, "ClassDescription", "classDescription")),
    purchaseAmount: asNumber(getValue(row, "PurchaseAmount", "purchaseAmount")),
  } satisfies FmlNoContractRow));
  return { vehicles, totalCount: isRecord(payload) ? asNumber(getValue(payload, "TotalCount", "totalCount")) ?? vehicles.length : vehicles.length };
}

export async function getFmlOverUtilized(query: FmlReportQuery = {}) {
  const payload = await requestApi(`api/report/fml/over-utilized${reportQuery(query)}`);
  const vehicles = reportRows(payload, "Vehicles").map((row) => ({
    vehicleCounter: asNumber(getValue(row, "VehicleCounter", "vehicleCounter")),
    ggNumber: asString(getValue(row, "GgNumber", "ggNumber")),
    gpNumber: asString(getValue(row, "GpNumber", "gpNumber")),
    hiredFrom: asString(getValue(row, "HiredFrom", "hiredFrom")),
    month: asString(getValue(row, "Month", "month")),
    maxOdoMeter: asNumber(getValue(row, "MaxOdoMeter", "maxOdoMeter")),
    minOdoMeter: asNumber(getValue(row, "MinOdoMeter", "minOdoMeter")),
    actualKilos: asNumber(getValue(row, "ActualKilos", "actualKilos")),
    agreedKilos: asNumber(getValue(row, "AgreedKilos", "agreedKilos")),
    excessKilos: asNumber(getValue(row, "ExcessKilos", "excessKilos")),
    agreedOverallKilo: asNumber(getValue(row, "AgreedOverallKilo", "agreedOverallKilo")),
    agreedTerms: asNumber(getValue(row, "AgreedTerms", "agreedTerms")),
    actualTerm: asNumber(getValue(row, "ActualTerm", "actualTerm")),
    totalKilos: asNumber(getValue(row, "TotalKilos", "totalKilos")),
    totalExcessKilos: asNumber(getValue(row, "TotalExcessKilos", "totalExcessKilos")),
    averageMonthlyKilos: asNumber(getValue(row, "AverageMonthlyKilos", "averageMonthlyKilos")),
    projectedEndMonth: asString(getValue(row, "ProjectedEndMonth", "projectedEndMonth")),
    projectedEndDate: asString(getValue(row, "ProjectedEndDate", "projectedEndDate")),
    yearModel: asNumber(getValue(row, "YearModel", "yearModel")),
    modelDescription: asString(getValue(row, "ModelDescription", "modelDescription")),
    purchaseAmount: asNumber(getValue(row, "PurchaseAmount", "purchaseAmount")),
  } satisfies FmlOverUtilizedRow));
  return { vehicles, totalCount: isRecord(payload) ? asNumber(getValue(payload, "TotalCount", "totalCount")) ?? vehicles.length : vehicles.length };
}
