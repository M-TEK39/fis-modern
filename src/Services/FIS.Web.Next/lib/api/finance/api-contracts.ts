import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type ContractSearchType = "GG" | "GP";

export type ContractRecord = {
  contractCode: number;
  vmfCode: number;
  siteCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  siteDescription: string | null;
  driverId: string | null;
  driverName: string | null;
  contractTypeCode: string | null;
  contractStatusCode: number | null;
  contractStatusDate: string | null;
  stillCurrent: string | null;
  startDate: string | null;
  startTime: string | null;
  endDate: string | null;
  endTime: string | null;
  startOdometer: number | null;
  endOdometer: number | null;
  targetReturnDate: string | null;
  userCode: number | null;
  siteDriverCode: number | null;
  approverCode: number | null;
  parentContractCode: number | null;
  reliefForContract: number | null;
  vehicleAssessmentCode: number | null;
  monthlyKm: number | null;
  hoursUsed: number | null;
  basFundCode: string | null;
  basObjectiveCode: string | null;
  basProjectNumber: string | null;
  basResponsibilityCode: string | null;
  journalDetailCode: string | null;
  contractGroupCode: number | null;
  lockedForTransfer: boolean;
  notes: string | null;
  authorisation: string | null;
  chargedUntil: string | null;
  collectorFirstname: string | null;
  collectorSurname: string | null;
  collectorSaId: string | null;
  collectorPassportNumber: string | null;
  collectorOfficeNumber: string | null;
  collectorCellphoneNumber: string | null;
  collectorOffice: string | null;
  collectorDesignation: string | null;
  reliefVehicleOption: boolean | null;
  leaseContractPeriod: number | null;
  contractEstimatedOverallKm: number | null;
  intendedStartDate: string | null;
  intendedStartTime: string | null;
  captureDate: string | null;
  modifiedDate: string | null;
  reassignedFromContractCode: number | null;
  dateCreated: string | null;
  dateUpdated: string | null;
  createdByUserCode: number | null;
  modifiedByUserCode: number | null;
  isDeleted: boolean;
  backdatingStartDate: string | null;
  backdatingRequestedDate: string | null;
  backdatingRequestedByUsername: string | null;
  backdatingApprovedDate: string | null;
  backdatingApprovedByUsername: string | null;
  backdatingDeclinedDate: string | null;
  backdatingDeclinedByUsername: string | null;
};

export type ContractPage = {
  items: ContractRecord[];
  page: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
};

export type ContractVehicleSearchResult = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  chassisNumber: string | null;
  engineNumber: string | null;
  invoiceNumber: string | null;
};

export type ReliefVehicleSearchResult = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  isAvailable: boolean;
};

export type HireContractRequest = {
  VmfCode: number;
  SiteCode: number;
  StartDate?: string | null;
  StartOdometer: number | null;
  DriverId: string | null;
  SiteDriverCode: number | null;
  UserCode: number | null;
  ContractType?: string | null;
  Authorisation: string | null;
  Notes: string | null;
  TargetReturnDate: string | null;
  BackdatingStartDate?: string | null;
};

export type EditContractRequest = {
  SiteCode: number | null;
  DriverId: string | null;
  SiteDriverCode: number | null;
  UserCode: number | null;
  Authorisation: string | null;
  Notes: string | null;
  TargetReturnDate: string | null;
  StartOdometer: number | null;
};

export type CloseContractRequest = {
  EndDate: string;
  EndOdometer: number;
  Notes: string | null;
  HomeDepartmentCode: number;
  HomeSiteCode: number;
  HomeSiteDriverCode: number | null;
  CreateHomeCustodyContract: boolean;
};

export type ContractHistoryBackdatingRequest = {
  StartDate: string;
  EndDate: string | null;
  StartOdometer: number | null;
  EndOdometer: number | null;
};

export type ContractReassignRequest = {
  NewSiteCode: number | null;
  NewSiteDriverCode: number | null;
  StartDate: string | null;
  StartOdometer: number | null;
  Reason: string;
};

export type ReliefVehicleRequest = {
  ReliefVmfCode: number;
  StartOdometer: number | null;
  TargetReturnDate: string | null;
  Reason: string;
};

export type ContractApiErrorReason =
  "unauthorized" | "forbidden" | "unavailable" | "invalid-response" | "not-found";

export class ContractApiError extends Error {
  constructor(
    public readonly reason: ContractApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "ContractApiError";
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
  if (typeof value === "string")
    return ["true", "1", "y", "yes"].includes(value.trim().toLowerCase());
  if (typeof value === "number") return value !== 0;
  return false;
}

function getCollection(payload: unknown) {
  if (Array.isArray(payload)) return payload;
  if (isRecord(payload)) {
    const value = getValue(payload, "data", "items", "results");
    return Array.isArray(value) ? value : [];
  }
  return [];
}

function mapContract(value: unknown): ContractRecord | null {
  if (!isRecord(value)) return null;

  const contractCode = asNumber(getValue(value, "contractCode", "contract_code", "contract_id"));
  const vmfCode = asNumber(getValue(value, "vmfCode", "vmf_code"));
  const siteCode = asNumber(getValue(value, "siteCode", "site_code"));
  if (contractCode === null || vmfCode === null || siteCode === null) return null;

  const stillCurrent = asString(getValue(value, "stillCurrent", "still_current"));
  const statusCode = asNumber(getValue(value, "contractStatusCode", "contract_status_code"));

  return {
    contractCode,
    vmfCode,
    siteCode,
    fleetNumber: asString(getValue(value, "vehicleFleetNumber", "fleetNumber", "fleet_number")),
    registrationNumber: asString(
      getValue(value, "vehicleRegistrationNumber", "registrationNumber", "registration_number"),
    ),
    siteDescription: asString(getValue(value, "siteDescription", "siteName", "site_name")),
    driverId: asString(getValue(value, "driverId", "DriverId", "driver_id")),
    driverName: asString(getValue(value, "driverName", "DriverName", "driver_name")),
    contractTypeCode: asString(
      getValue(value, "contractTypeCode", "contract_type", "contract_type_code"),
    ),
    contractStatusCode: statusCode ?? (stillCurrent?.toUpperCase() === "Y" ? 3 : null),
    contractStatusDate: asString(getValue(value, "contractStatusDate", "contract_status_date")),
    stillCurrent,
    startDate: asString(getValue(value, "startDate", "start_date")),
    startTime: asString(getValue(value, "startTime", "start_time")),
    endDate: asString(getValue(value, "endDate", "end_date")),
    endTime: asString(getValue(value, "endTime", "end_time")),
    startOdometer: asNumber(getValue(value, "startOdometer", "start_odometer")),
    endOdometer: asNumber(getValue(value, "endOdometer", "end_odometer")),
    targetReturnDate: asString(getValue(value, "targetReturnDate", "target_return_date")),
    userCode: asNumber(getValue(value, "userCode", "user_code")),
    siteDriverCode: asNumber(getValue(value, "siteDriverCode", "site_driver_code")),
    approverCode: asNumber(getValue(value, "approverCode", "approver_code")),
    parentContractCode: asNumber(getValue(value, "parentContractCode", "parent_contract_code")),
    reliefForContract: asNumber(getValue(value, "reliefForContract", "relief_for_contract")),
    vehicleAssessmentCode: asNumber(
      getValue(value, "vehicleAssessmentCode", "vehicle_assessment_code"),
    ),
    monthlyKm: asNumber(getValue(value, "monthlyKm", "monthly_km")),
    hoursUsed: asNumber(getValue(value, "hoursUsed", "hours_used")),
    basFundCode: asString(getValue(value, "basFundCode", "bas_fund_code")),
    basObjectiveCode: asString(getValue(value, "basObjectiveCode", "bas_objective_code")),
    basProjectNumber: asString(getValue(value, "basProjectNumber", "bas_project_number")),
    basResponsibilityCode: asString(
      getValue(value, "basResponsibilityCode", "bas_responsibility_code"),
    ),
    journalDetailCode: asString(getValue(value, "journalDetailCode", "journal_detail_code")),
    contractGroupCode: asNumber(getValue(value, "contractGroupCode", "contract_group_code")),
    lockedForTransfer: asBoolean(getValue(value, "lockedForTransfer", "locked_for_transfer")),
    notes: asString(getValue(value, "notes", "Notes", "contract_notes")),
    authorisation: asString(getValue(value, "authorisation", "Authorisation")),
    chargedUntil: asString(getValue(value, "chargedUntil", "Charged_Until")),
    collectorFirstname: asString(getValue(value, "collectorFirstname", "collector_firstname")),
    collectorSurname: asString(getValue(value, "collectorSurname", "collector_surname")),
    collectorSaId: asString(getValue(value, "collectorSaId", "collector_sa_id")),
    collectorPassportNumber: asString(
      getValue(value, "collectorPassportNumber", "collector_passportnumber"),
    ),
    collectorOfficeNumber: asString(
      getValue(value, "collectorOfficeNumber", "collector_office_number"),
    ),
    collectorCellphoneNumber: asString(
      getValue(value, "collectorCellphoneNumber", "collector_cellphone_number"),
    ),
    collectorOffice: asString(getValue(value, "collectorOffice", "collector_office")),
    collectorDesignation: asString(
      getValue(value, "collectorDesignation", "collector_designation"),
    ),
    reliefVehicleOption:
      getValue(value, "reliefVehicleOption", "relief_vehicle_option") === undefined
        ? null
        : asBoolean(getValue(value, "reliefVehicleOption", "relief_vehicle_option")),
    leaseContractPeriod: asNumber(getValue(value, "leaseContractPeriod", "lease_contract_period")),
    contractEstimatedOverallKm: asNumber(
      getValue(value, "contractEstimatedOverallKm", "contract_estimated_overall_km"),
    ),
    intendedStartDate: asString(getValue(value, "intendedStartDate", "intended_start_date")),
    intendedStartTime: asString(getValue(value, "intendedStartTime", "intended_start_time")),
    captureDate: asString(getValue(value, "captureDate", "capture_date")),
    modifiedDate: asString(getValue(value, "modifiedDate", "modified_date")),
    reassignedFromContractCode: asNumber(
      getValue(value, "reassignedFromContractCode", "reassigned_from_contract_code"),
    ),
    dateCreated: asString(getValue(value, "dateCreated", "date_created")),
    dateUpdated: asString(getValue(value, "dateUpdated", "date_updated")),
    createdByUserCode: asNumber(getValue(value, "createdByUserCode", "created_by_user_code")),
    modifiedByUserCode: asNumber(getValue(value, "modifiedByUserCode", "modified_by_user_code")),
    isDeleted: asBoolean(getValue(value, "isDeleted", "is_deleted")),
    backdatingStartDate: asString(getValue(value, "backdatingStartDate", "backdating_start_date")),
    backdatingRequestedDate: asString(getValue(value, "backdatingRequestedDate", "backdating_requested_date")),
    backdatingRequestedByUsername: asString(
      getValue(value, "backdatingRequestedByUsername", "backdating_requested_by_username"),
    ),
    backdatingApprovedDate: asString(getValue(value, "backdatingApprovedDate", "backdating_approved_date")),
    backdatingApprovedByUsername: asString(
      getValue(value, "backdatingApprovedByUsername", "backdating_approved_by_Username"),
    ),
    backdatingDeclinedDate: asString(getValue(value, "backdatingDeclinedDate", "backdating_declined_date")),
    backdatingDeclinedByUsername: asString(
      getValue(value, "backdatingDeclinedByUsername", "backdating_declined_by_Username"),
    ),
  };
}

function mapVehicleSearchResult(value: unknown): ContractVehicleSearchResult | null {
  if (!isRecord(value)) return null;
  const vmfCode = asNumber(getValue(value, "vmfCode", "vmf_code"));
  return vmfCode === null
    ? null
    : {
        vmfCode,
        fleetNumber: asString(getValue(value, "fleetNumber", "fleet_number")),
        registrationNumber: asString(getValue(value, "registrationNumber", "registration_number")),
        chassisNumber: asString(getValue(value, "chassisNumber", "chassis_number")),
        engineNumber: asString(getValue(value, "engineNumber", "engine_number_1")),
        invoiceNumber: asString(getValue(value, "invoiceNumber", "invoice_number")),
      };
}

function mapReliefVehicleSearchResult(value: unknown): ReliefVehicleSearchResult | null {
  if (!isRecord(value)) return null;
  const vmfCode = asNumber(getValue(value, "vmfCode", "vmf_code"));
  return vmfCode === null
    ? null
    : {
        vmfCode,
        fleetNumber: asString(getValue(value, "fleetNumber", "fleet_number")),
        registrationNumber: asString(getValue(value, "registrationNumber", "registration_number")),
        isAvailable: asBoolean(getValue(value, "isAvailable", "is_available")),
      };
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader)
    throw new ContractApiError("unauthorized", "No FIS access cookie is available.");

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

    if (response.status === 401) {
      throw new ContractApiError("unauthorized", "The FIS access cookie was rejected.");
    }
    if (response.status === 403) {
      throw new ContractApiError(
        "forbidden",
        "You do not have permission to access this contract operation.",
      );
    }
    if (response.status === 404) {
      throw new ContractApiError("not-found", "The requested contract was not found.");
    }
    if (!response.ok) {
      let message = `FIS API returned HTTP ${response.status}.`;
      try {
        const payload = (await response.json()) as unknown;
        if (isRecord(payload) && typeof getValue(payload, "error", "message") === "string") {
          message = String(getValue(payload, "error", "message"));
        }
      } catch {
        // Keep the stable HTTP error when the API did not return JSON.
      }
      throw new ContractApiError("invalid-response", message);
    }

    return response;
  } catch (error) {
    if (error instanceof ContractApiError) throw error;
    throw new ContractApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new ContractApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapPage(payload: unknown, requestedPage: number, requestedPageSize: number): ContractPage {
  const record = isRecord(payload) ? payload : {};
  const items = getCollection(payload)
    .map(mapContract)
    .filter((item): item is ContractRecord => item !== null);
  const page = asNumber(getValue(record, "page")) ?? requestedPage;
  const pageSize = asNumber(getValue(record, "page_size", "pageSize")) ?? requestedPageSize;
  const totalRecords = asNumber(getValue(record, "total_records", "totalRecords")) ?? items.length;
  const totalPages =
    asNumber(getValue(record, "total_pages", "totalPages")) ??
    Math.max(1, Math.ceil(totalRecords / pageSize));
  return { items, page, pageSize, totalRecords, totalPages: Math.max(1, totalPages) };
}

export async function getContractPage(
  options: {
    page?: number;
    pageSize?: number;
    statusCode?: number | null;
    siteCode?: number | null;
    stillCurrent?: string | null;
    startDateFrom?: string | null;
    startDateTo?: string | null;
    vmfCode?: number | null;
  } = {},
) {
  const page = Math.max(1, options.page ?? 1);
  const pageSize = Math.min(100, Math.max(1, options.pageSize ?? 12));
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (options.statusCode !== null && options.statusCode !== undefined)
    params.set("status", String(options.statusCode));
  if (options.siteCode !== null && options.siteCode !== undefined)
    params.set("siteCode", String(options.siteCode));
  if (options.stillCurrent) params.set("stillCurrent", options.stillCurrent);
  if (options.startDateFrom) params.set("startDateFrom", options.startDateFrom);
  if (options.startDateTo) params.set("startDateTo", options.startDateTo);
  if (options.vmfCode !== null && options.vmfCode !== undefined)
    params.set("vmfCode", String(options.vmfCode));

  return mapPage(
    await readJson(await requestApi(`api/contracts?${params.toString()}`)),
    page,
    pageSize,
  );
}

export async function getContract(contractCode: number) {
  const record = mapContract(
    await readJson(await requestApi(`api/contracts/${encodeURIComponent(contractCode)}`)),
  );
  if (!record)
    throw new ContractApiError(
      "invalid-response",
      "The FIS API returned an invalid contract record.",
    );
  return record;
}

export async function searchContractVehicles(query: string) {
  const payload = await readJson(
    await requestApi(`api/contracts/vehicle-search?query=${encodeURIComponent(query)}`),
  );
  return getCollection(payload)
    .map(mapVehicleSearchResult)
    .filter((item): item is ContractVehicleSearchResult => item !== null);
}

export async function searchReliefVehicles(query: string) {
  const payload = await readJson(
    await requestApi(`api/contracts/relief/search?query=${encodeURIComponent(query)}`),
  );
  return getCollection(payload)
    .map(mapReliefVehicleSearchResult)
    .filter((item): item is ReliefVehicleSearchResult => item !== null && item.isAvailable);
}

export async function hireContractAgainstApi(request: HireContractRequest) {
  return readJson(
    await requestApi("api/contracts/hire", { method: "POST", body: JSON.stringify(request) }),
  );
}

export async function editContractAgainstApi(contractCode: number, request: EditContractRequest) {
  return readJson(
    await requestApi(`api/contracts/${encodeURIComponent(contractCode)}/edit`, {
      method: "PUT",
      body: JSON.stringify(request),
    }),
  );
}

export async function postContractAction(path: string, body: unknown = {}) {
  return readJson(await requestApi(path, { method: "POST", body: JSON.stringify(body) }));
}

export async function extendContractAgainstApi(
  contractCode: number,
  request: {
    NewTargetReturnDate: string;
    Notes?: string | null;
    EstimatedOverallKilometres?: number | null;
  },
) {
  return readJson(
    await requestApi(`api/contracts/${encodeURIComponent(contractCode)}/extend`, {
      method: "PUT",
      body: JSON.stringify(request),
    }),
  );
}

export async function closeContractAgainstApi(contractCode: number, request: CloseContractRequest) {
  return postContractAction(`api/contracts/${encodeURIComponent(contractCode)}/close`, request);
}

export async function reassignContractAgainstApi(
  contractCode: number,
  request: ContractReassignRequest,
) {
  return postContractAction(`api/contracts/${encodeURIComponent(contractCode)}/reassign`, request);
}

export async function createReliefContractAgainstApi(
  contractCode: number,
  request: ReliefVehicleRequest,
) {
  return postContractAction(`api/contracts/${encodeURIComponent(contractCode)}/relief`, request);
}

export async function updateContractHistoryAgainstApi(
  contractCode: number,
  request: ContractHistoryBackdatingRequest,
) {
  return readJson(
    await requestApi(`api/contracts/${encodeURIComponent(contractCode)}/history`, {
      method: "PUT",
      body: JSON.stringify(request),
    }),
  );
}

export async function getContractPrintout(contractCode: number) {
  return readJson(await requestApi(`api/contracts/${encodeURIComponent(contractCode)}/printout`));
}
