import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type TaxiRecord = {
  requestId: number;
  rekNum: string;
  contractorId: number | null;
  vmfCode: string | null;
  departmentCode: number | null;
  siteCode: number;
  dateRequired: string;
  timeRequired: string;
  vehicleTypeCode: number | null;
  official: string | null;
  rank: string | null;
  confirmed: number | null;
  subContractorId: number | null;
  cancelled: string | null;
  driver: string | null;
  regNum: string | null;
  parentTaxiCode: number | null;
  address1: string | null;
  address2: string | null;
  address3: string | null;
  flight: string | null;
  instructions: string | null;
  destination1: string | null;
  destination2: string | null;
  destination3: string | null;
  userAccessCode: number | null;
  requestDate: string | null;
  respCode: string | null;
  objectCode: string | null;
  fmsCode: string | null;
  dateRequired2: string | null;
  timeRequired2: string | null;
  address12: string | null;
  address22: string | null;
  address32: string | null;
  destination12: string | null;
  destination22: string | null;
  destination32: string | null;
  transManName: string | null;
  transManDate: string | null;
  transManRank: string | null;
  transManTel: string | null;
  bookingBy: string | null;
  arrivalTime: string | null;
  project: string | null;
  driverAvailable: boolean | null;
  persal: string | null;
  jiaPickup: boolean | null;
  officialTelNum: string | null;
  fundCode: string | null;
  dateCreated: string | null;
  dateUpdated: string | null;
  departmentName: string | null;
  siteName: string | null;
};

export type TaxiInput = Omit<
  TaxiRecord,
  "requestId" | "dateCreated" | "dateUpdated" | "departmentName" | "siteName"
> & {
  requestId?: number;
};

export type TaxiLogReference = {
  contractors: Array<{ contractorId: number; contractorName: string }>;
  classes: Array<{
    contractorId: number;
    classId: number;
    description: string;
    kmTariff: number | null;
    driverPerHour: number | null;
    dailyTariff: number | null;
  }>;
  notes: Array<{ noteCode: number; description: string }>;
};

export type TaxiLogLookup = {
  requestId: number;
  rekNum: string;
  contractorId: number | null;
  contractorName: string | null;
  vehicleTypeCode: number | null;
  vehicleTypeDescription: string | null;
  registrationNumber: string | null;
  fleetNumber: string | null;
  driver: string | null;
  official: string | null;
  dateRequired: string;
  timeRequired: string;
  departmentCode: number | null;
  departmentName: string | null;
  log: TaxiLogDetail | null;
};

export type TaxiLogDetail = {
  logId: number;
  driverStartOdo: number | null;
  driverEndOdo: number | null;
  driverStartDate: string | null;
  driverEndDate: string | null;
  driverStartTime: string | null;
  driverEndTime: string | null;
  taxiLogNoteCode: number | null;
  quotedTariff: number | null;
};

export type TaxiLogInput = {
  requestId: number;
  rekNum: string;
  contractorId: number;
  vehicleTypeCode: number | null;
  registrationNumber: string;
  driver: string;
  driverStartOdo: number;
  driverEndOdo: number;
  driverStartDate: string;
  driverEndDate: string;
  driverStartTime: string;
  driverEndTime: string;
  taxiLogNoteCode: number | null;
  quotedTariff: number | null;
};

export type WhiteLogInput = {
  vmfCode: number;
  startOdo: number;
  endOdo: number;
  startDate: string;
  endDate: string;
  driver: string;
};

export type TaxiScanDocRecord = {
  scanDocCode: number;
  vmfCode: number;
  image: string | null;
  periodBegin: string | null;
  periodEnd: string | null;
  dateCreated: string | null;
  dateUpdated: string | null;
  fleetNumber: string | null;
  registrationNumber: string | null;
  fileUrl: string;
};

export type TaxiPage<TItem> = {
  items: TItem[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
};

export const DEFAULT_TAXI_PAGE_SIZE = 24;

export type TaxiApiErrorReason = "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class TaxiApiError extends Error {
  constructor(
    public readonly reason: TaxiApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "TaxiApiError";
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
  if (typeof value === "string")
    return ["true", "1", "y", "yes"].includes(value.trim().toLowerCase());
  return null;
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
  if (!cookieHeader) throw new TaxiApiError("unauthorized", "No FIS access cookie is available.");

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers: {
        accept: "application/json",
        cookie: cookieHeader,
        ...(init.body && !(init.body instanceof FormData)
          ? { "content-type": "application/json" }
          : {}),
        ...init.headers,
      },
      signal: controller.signal,
    });
    if (response.status === 401 || response.status === 403)
      throw new TaxiApiError("unauthorized", "The FIS access cookie was rejected.");
    if (response.status === 404)
      throw new TaxiApiError("not-found", "The requested taxi record was not found.");
    if (!response.ok)
      throw new TaxiApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        `FIS API returned HTTP ${response.status}.`,
      );
    return response;
  } catch (error) {
    if (error instanceof TaxiApiError) throw error;
    throw new TaxiApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new TaxiApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapTaxi(value: unknown): TaxiRecord | null {
  if (!isRecord(value)) return null;
  const requestId = asNumber(getValue(value, "request_id", "requestId"));
  const rekNum = asString(getValue(value, "rek_num", "rekNum"));
  if (requestId === null || rekNum === null) return null;
  return {
    requestId,
    rekNum,
    contractorId: asNumber(getValue(value, "contractor_id", "contractorId")),
    vmfCode: asString(getValue(value, "vmf_code", "vmfCode")),
    departmentCode: asNumber(getValue(value, "department_code", "departmentCode")),
    siteCode: asNumber(getValue(value, "site_code", "siteCode")) ?? 0,
    dateRequired: asString(getValue(value, "date_required", "dateRequired")) ?? "",
    timeRequired: asString(getValue(value, "time_required", "timeRequired")) ?? "",
    vehicleTypeCode: asNumber(getValue(value, "vehicle_type_code", "vehicleTypeCode")),
    official: asString(getValue(value, "official")),
    rank: asString(getValue(value, "rank")),
    confirmed: asNumber(getValue(value, "confirmed")),
    subContractorId: asNumber(getValue(value, "sub_contractor_id", "subContractorId")),
    cancelled: asString(getValue(value, "cancelled")),
    driver: asString(getValue(value, "driver")),
    regNum: asString(getValue(value, "reg_num", "regNum")),
    parentTaxiCode: asNumber(getValue(value, "parent_taxi_code", "parentTaxiCode")),
    address1: asString(getValue(value, "address_1", "address1")),
    address2: asString(getValue(value, "address_2", "address2")),
    address3: asString(getValue(value, "address_3", "address3")),
    flight: asString(getValue(value, "flight")),
    instructions: asString(getValue(value, "instructions")),
    destination1: asString(getValue(value, "destination_1", "destination1")),
    destination2: asString(getValue(value, "destination_2", "destination2")),
    destination3: asString(getValue(value, "destination_3", "destination3")),
    userAccessCode: asNumber(getValue(value, "user_access_code", "userAccessCode")),
    requestDate: asString(getValue(value, "request_date", "requestDate")),
    respCode: asString(getValue(value, "resp_code", "respCode")),
    objectCode: asString(getValue(value, "object_code", "objectCode")),
    fmsCode: asString(getValue(value, "fms_code", "fmsCode")),
    dateRequired2: asString(getValue(value, "date_required_2", "dateRequired2")),
    timeRequired2: asString(getValue(value, "time_required_2", "timeRequired2")),
    address12: asString(getValue(value, "address_12", "address12")),
    address22: asString(getValue(value, "address_22", "address22")),
    address32: asString(getValue(value, "address_32", "address32")),
    destination12: asString(getValue(value, "destination_12", "destination12")),
    destination22: asString(getValue(value, "destination_22", "destination22")),
    destination32: asString(getValue(value, "destination_32", "destination32")),
    transManName: asString(getValue(value, "trans_man_name", "transManName")),
    transManDate: asString(getValue(value, "trans_man_date", "transManDate")),
    transManRank: asString(getValue(value, "trans_man_rank", "transManRank")),
    transManTel: asString(getValue(value, "trans_man_tel", "transManTel")),
    bookingBy: asString(getValue(value, "booking_by", "bookingBy")),
    arrivalTime: asString(getValue(value, "arrival_time", "arrivalTime")),
    project: asString(getValue(value, "project")),
    driverAvailable: asBoolean(getValue(value, "driver_available", "driverAvailable")),
    persal: asString(getValue(value, "persal")),
    jiaPickup: asBoolean(getValue(value, "JIA_pickup", "jiaPickup")),
    officialTelNum: asString(getValue(value, "official_tel_num", "officialTelNum")),
    fundCode: asString(getValue(value, "fund_code", "fundCode")),
    dateCreated: asString(getValue(value, "date_created", "dateCreated")),
    dateUpdated: asString(getValue(value, "date_updated", "dateUpdated")),
    departmentName: asString(getValue(value, "departmentName", "department_name", "Department")),
    siteName: asString(getValue(value, "siteName", "site_name", "Site")),
  };
}

function mapTaxiScanDoc(value: unknown): TaxiScanDocRecord | null {
  if (!isRecord(value)) return null;
  const scanDocCode = asNumber(
    getValue(value, "scan_doc_code", "scanDocCode", "taxi_scandoc_code"),
  );
  const vmfCode = asNumber(getValue(value, "vmf_code", "vmfCode"));
  if (scanDocCode === null || vmfCode === null) return null;
  return {
    scanDocCode,
    vmfCode,
    image: asString(getValue(value, "image")),
    periodBegin: asString(getValue(value, "period_begin", "periodBegin")),
    periodEnd: asString(getValue(value, "period_end", "periodEnd")),
    dateCreated: asString(getValue(value, "date_created", "dateCreated")),
    dateUpdated: asString(getValue(value, "date_updated", "dateUpdated")),
    fleetNumber: asString(getValue(value, "fleet_number", "fleetNumber")),
    registrationNumber: asString(getValue(value, "registration_number", "registrationNumber")),
    fileUrl:
      asString(getValue(value, "file_url", "fileUrl")) ?? `/api/taxi-scan-docs/${scanDocCode}/file`,
  };
}

function readPageMetadata(payload: JsonRecord) {
  const page = asNumber(getValue(payload, "page"));
  const pageSize = asNumber(getValue(payload, "pageSize", "page_size"));
  const total = asNumber(getValue(payload, "total", "totalRecords", "total_records"));
  const totalPages = asNumber(getValue(payload, "totalPages", "total_pages"));

  if (
    page === null ||
    pageSize === null ||
    total === null ||
    totalPages === null ||
    !Number.isInteger(page) ||
    !Number.isInteger(pageSize) ||
    !Number.isInteger(total) ||
    !Number.isInteger(totalPages) ||
    page < 1 ||
    pageSize < 1 ||
    total < 0 ||
    totalPages < 1
  ) {
    return null;
  }

  return { page, pageSize, total, totalPages };
}

function normalizePage(value: number | undefined) {
  return Number.isInteger(value) && (value ?? 0) > 0 ? (value ?? 1) : 1;
}

function normalizePageSize(value: number | undefined) {
  const pageSize =
    Number.isInteger(value) && (value ?? 0) > 0
      ? (value ?? DEFAULT_TAXI_PAGE_SIZE)
      : DEFAULT_TAXI_PAGE_SIZE;
  return Math.min(100, pageSize);
}

function readTaxiPage(payload: unknown): TaxiPage<TaxiRecord> {
  if (!isRecord(payload) || !Array.isArray(payload.items))
    throw new TaxiApiError("invalid-response", "The FIS API returned an invalid taxi page.");
  const metadata = readPageMetadata(payload);
  if (!metadata)
    throw new TaxiApiError("invalid-response", "The FIS API returned incomplete taxi pagination.");
  return {
    items: payload.items.map(mapTaxi).filter((taxi): taxi is TaxiRecord => taxi !== null),
    ...metadata,
  };
}

function readTaxiScanDocPage(payload: unknown): TaxiPage<TaxiScanDocRecord> {
  if (!isRecord(payload) || !Array.isArray(payload.items))
    throw new TaxiApiError(
      "invalid-response",
      "The FIS API returned an invalid scan-document page.",
    );
  const metadata = readPageMetadata(payload);
  if (!metadata)
    throw new TaxiApiError(
      "invalid-response",
      "The FIS API returned incomplete scan-document pagination.",
    );
  return {
    items: payload.items
      .map(mapTaxiScanDoc)
      .filter((document): document is TaxiScanDocRecord => document !== null),
    ...metadata,
  };
}

function taxiPayload(input: TaxiInput) {
  return {
    request_id: input.requestId ?? 0,
    rek_num: input.rekNum,
    contractor_id: input.contractorId,
    vmf_code: input.vmfCode,
    department_code: input.departmentCode,
    site_code: input.siteCode,
    date_required: input.dateRequired,
    time_required: input.timeRequired,
    vehicle_type_code: input.vehicleTypeCode,
    official: input.official,
    rank: input.rank,
    confirmed: input.confirmed,
    sub_contractor_id: input.subContractorId,
    cancelled: input.cancelled,
    driver: input.driver,
    reg_num: input.regNum,
    parent_taxi_code: input.parentTaxiCode,
    address_1: input.address1,
    address_2: input.address2,
    address_3: input.address3,
    flight: input.flight,
    instructions: input.instructions,
    destination_1: input.destination1,
    destination_2: input.destination2,
    destination_3: input.destination3,
    user_access_code: input.userAccessCode,
    request_date: input.requestDate,
    resp_code: input.respCode,
    object_code: input.objectCode,
    fms_code: input.fmsCode,
    date_required_2: input.dateRequired2,
    time_required_2: input.timeRequired2,
    address_12: input.address12,
    address_22: input.address22,
    address_32: input.address32,
    destination_12: input.destination12,
    destination_22: input.destination22,
    destination_32: input.destination32,
    trans_man_name: input.transManName,
    trans_man_date: input.transManDate,
    trans_man_rank: input.transManRank,
    trans_man_tel: input.transManTel,
    booking_by: input.bookingBy,
    arrival_time: input.arrivalTime,
    project: input.project,
    driver_available: input.driverAvailable,
    persal: input.persal,
    JIA_pickup: input.jiaPickup,
    official_tel_num: input.officialTelNum,
    fund_code: input.fundCode,
  };
}

export async function getTaxis() {
  return getTaxiCollection(await readJson(await requestApi("api/Taxi")));
}

export async function getTaxiPage(
  options: {
    page?: number;
    pageSize?: number;
    pendingOnly?: boolean;
    jiaPickupOnly?: boolean;
    search?: string;
  } = {},
): Promise<TaxiPage<TaxiRecord>> {
  const params = new URLSearchParams({
    page: String(normalizePage(options.page)),
    pageSize: String(normalizePageSize(options.pageSize)),
  });
  if (options.pendingOnly) params.set("pendingOnly", "true");
  if (options.jiaPickupOnly) params.set("jiaPickupOnly", "true");
  if (options.search?.trim()) params.set("search", options.search.trim());
  return readTaxiPage(await readJson(await requestApi(`api/Taxi/page?${params.toString()}`)));
}

export async function getTaxi(requestId: number) {
  const taxi = mapTaxi(
    await readJson(await requestApi(`api/Taxi/${encodeURIComponent(requestId)}`)),
  );
  if (!taxi)
    throw new TaxiApiError("invalid-response", "The FIS API returned an invalid taxi requisition.");
  return taxi;
}

export async function getTaxiByRequisition(rekNum: string) {
  const taxi = mapTaxi(
    await readJson(await requestApi(`api/Taxi/lookup/${encodeURIComponent(rekNum)}`)),
  );
  if (!taxi)
    throw new TaxiApiError("invalid-response", "The FIS API returned an invalid taxi requisition.");
  return taxi;
}

export async function createTaxi(input: TaxiInput) {
  const taxi = mapTaxi(
    await readJson(
      await requestApi("api/Taxi", { method: "POST", body: JSON.stringify(taxiPayload(input)) }),
    ),
  );
  if (!taxi)
    throw new TaxiApiError(
      "invalid-response",
      "The FIS API returned an invalid created taxi requisition.",
    );
  return taxi;
}

export async function updateTaxi(requestId: number, input: TaxiInput) {
  const taxi = mapTaxi(
    await readJson(
      await requestApi(`api/Taxi/${encodeURIComponent(requestId)}`, {
        method: "PUT",
        body: JSON.stringify(taxiPayload({ ...input, requestId })),
      }),
    ),
  );
  if (!taxi)
    throw new TaxiApiError(
      "invalid-response",
      "The FIS API returned an invalid updated taxi requisition.",
    );
  return taxi;
}

function getTaxiCollection(payload: unknown) {
  return getCollection(payload)
    .map(mapTaxi)
    .filter((taxi): taxi is TaxiRecord => taxi !== null);
}

export async function getTaxiLogReferences(): Promise<TaxiLogReference> {
  const payload = await readJson(await requestApi("api/TaxiLog/references"));
  if (!isRecord(payload))
    throw new TaxiApiError("invalid-response", "The FIS API returned invalid taxi-log references.");
  const contractors = getCollection(getValue(payload, "Contractors", "contractors")).flatMap(
    (value) => {
      if (!isRecord(value)) return [];
      const contractorId = asNumber(getValue(value, "ContractorId", "contractorId"));
      return contractorId === null
        ? []
        : [
            {
              contractorId,
              contractorName:
                asString(getValue(value, "ContractorName", "contractorName")) ??
                `Contractor ${contractorId}`,
            },
          ];
    },
  );
  const classes = getCollection(getValue(payload, "Classes", "classes")).flatMap((value) => {
    if (!isRecord(value)) return [];
    const contractorId = asNumber(getValue(value, "ContractorId", "contractorId"));
    const classId = asNumber(getValue(value, "ClassId", "classId"));
    return contractorId === null || classId === null
      ? []
      : [
          {
            contractorId,
            classId,
            description:
              asString(getValue(value, "Description", "description")) ?? `Class ${classId}`,
            kmTariff: asNumber(getValue(value, "KmTariff", "kmTariff")),
            driverPerHour: asNumber(getValue(value, "DriverPerHour", "driverPerHour")),
            dailyTariff: asNumber(getValue(value, "DailyTariff", "dailyTariff")),
          },
        ];
  });
  const notes = getCollection(getValue(payload, "Notes", "notes")).flatMap((value) => {
    if (!isRecord(value)) return [];
    const noteCode = asNumber(getValue(value, "NoteCode", "noteCode"));
    return noteCode === null
      ? []
      : [
          {
            noteCode,
            description:
              asString(getValue(value, "Description", "description")) ?? `Note ${noteCode}`,
          },
        ];
  });
  return { contractors, classes, notes };
}

function mapLookup(value: unknown): TaxiLogLookup | null {
  if (!isRecord(value)) return null;
  const requestId = asNumber(getValue(value, "RequestId", "requestId"));
  const rekNum = asString(getValue(value, "RekNum", "rekNum"));
  if (requestId === null || rekNum === null) return null;
  const detailValue = getValue(value, "Log", "log");
  const detail = isRecord(detailValue)
    ? ({
        logId: asNumber(getValue(detailValue, "LogId", "logId")) ?? 0,
        driverStartOdo: asNumber(getValue(detailValue, "DriverStartOdo", "driverStartOdo")),
        driverEndOdo: asNumber(getValue(detailValue, "DriverEndOdo", "driverEndOdo")),
        driverStartDate: asString(getValue(detailValue, "DriverStartDate", "driverStartDate")),
        driverEndDate: asString(getValue(detailValue, "DriverEndDate", "driverEndDate")),
        driverStartTime: asString(getValue(detailValue, "DriverStartTime", "driverStartTime")),
        driverEndTime: asString(getValue(detailValue, "DriverEndTime", "driverEndTime")),
        taxiLogNoteCode: asNumber(getValue(detailValue, "TaxiLogNoteCode", "taxiLogNoteCode")),
        quotedTariff: asNumber(getValue(detailValue, "QuotedTariff", "quotedTariff")),
      } satisfies TaxiLogDetail)
    : null;
  return {
    requestId,
    rekNum,
    contractorId: asNumber(getValue(value, "ContractorId", "contractorId")),
    contractorName: asString(getValue(value, "ContractorName", "contractorName")),
    vehicleTypeCode: asNumber(getValue(value, "VehicleTypeCode", "vehicleTypeCode")),
    vehicleTypeDescription: asString(
      getValue(value, "VehicleTypeDescription", "vehicleTypeDescription"),
    ),
    registrationNumber: asString(getValue(value, "RegistrationNumber", "registrationNumber")),
    fleetNumber: asString(getValue(value, "FleetNumber", "fleetNumber")),
    driver: asString(getValue(value, "Driver", "driver")),
    official: asString(getValue(value, "Official", "official")),
    dateRequired: asString(getValue(value, "DateRequired", "dateRequired")) ?? "",
    timeRequired: asString(getValue(value, "TimeRequired", "timeRequired")) ?? "",
    departmentCode: asNumber(getValue(value, "DepartmentCode", "departmentCode")),
    departmentName: asString(getValue(value, "DepartmentName", "departmentName")),
    log: detail,
  };
}

export async function getTaxiLogLookup(rekNum: string, mode?: "ENTER" | "EDIT") {
  const query = mode ? `?mode=${encodeURIComponent(mode)}` : "";
  const lookup = mapLookup(
    await readJson(await requestApi(`api/TaxiLog/lookup/${encodeURIComponent(rekNum)}${query}`)),
  );
  if (!lookup)
    throw new TaxiApiError("invalid-response", "The FIS API returned an invalid taxi-log lookup.");
  return lookup;
}

function taxiLogPayload(input: TaxiLogInput) {
  return {
    request_id: input.requestId,
    rek_num: input.rekNum,
    contractor_id: input.contractorId,
    vehicle_type_code: input.vehicleTypeCode,
    reg_num: input.registrationNumber,
    driver: input.driver,
    driver_start_odo: input.driverStartOdo,
    driver_end_odo: input.driverEndOdo,
    driver_start_date: input.driverStartDate,
    driver_end_date: input.driverEndDate,
    driver_start_time: input.driverStartTime,
    driver_end_time: input.driverEndTime,
    taxi_log_note_code: input.taxiLogNoteCode,
    quoted_tariff: input.quotedTariff,
  };
}

export async function saveTaxiLog(input: TaxiLogInput, logId?: number) {
  const path = logId ? `api/TaxiLog/${encodeURIComponent(logId)}` : "api/TaxiLog";
  const method = logId ? "PUT" : "POST";
  const lookup = mapLookup(
    await readJson(await requestApi(path, { method, body: JSON.stringify(taxiLogPayload(input)) })),
  );
  if (!lookup)
    throw new TaxiApiError("invalid-response", "The FIS API returned an invalid saved taxi log.");
  return lookup;
}

export async function createTaxiWhiteLog(input: WhiteLogInput) {
  return readJson(
    await requestApi("api/Taxi/white-log", {
      method: "POST",
      body: JSON.stringify({
        vmf_code: input.vmfCode,
        start_odo: input.startOdo,
        end_odo: input.endOdo,
        start_date: input.startDate,
        end_date: input.endDate,
        driver: input.driver,
      }),
    }),
  );
}

export async function getTaxiScanDocs() {
  return getCollection(await readJson(await requestApi("api/taxi-scan-docs")))
    .map(mapTaxiScanDoc)
    .filter((document): document is TaxiScanDocRecord => document !== null);
}

export async function getTaxiScanDocsPage(
  options: {
    page?: number;
    pageSize?: number;
    search?: string;
  } = {},
): Promise<TaxiPage<TaxiScanDocRecord>> {
  const params = new URLSearchParams({
    page: String(normalizePage(options.page)),
    pageSize: String(normalizePageSize(options.pageSize)),
  });
  const search = options.search?.trim();
  if (search) params.set("search", search);
  return readTaxiScanDocPage(
    await readJson(await requestApi(`api/taxi-scan-docs/page?${params.toString()}`)),
  );
}

export async function uploadTaxiScanDoc(input: {
  vmfCode: number;
  periodBegin: string;
  periodEnd: string;
  file: File;
}) {
  const body = new FormData();
  body.set("vmfCode", String(input.vmfCode));
  body.set("periodBegin", input.periodBegin);
  body.set("periodEnd", input.periodEnd);
  body.set("file", input.file, input.file.name);
  const document = mapTaxiScanDoc(
    await readJson(await requestApi("api/taxi-scan-docs", { method: "POST", body })),
  );
  if (!document)
    throw new TaxiApiError(
      "invalid-response",
      "The FIS API returned an invalid uploaded taxi scan.",
    );
  return document;
}

export async function deleteTaxiScanDoc(scanDocCode: number) {
  await requestApi(`api/taxi-scan-docs/${encodeURIComponent(scanDocCode)}`, { method: "DELETE" });
}
