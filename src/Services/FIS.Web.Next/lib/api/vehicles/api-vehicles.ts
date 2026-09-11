import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
const PAGE_SIZE = 24;
export const DEFAULT_RENUMBERED_REPORT_PAGE_SIZE = PAGE_SIZE;

type JsonRecord = Record<string, unknown>;

export type VehicleSnapshotRow = {
  vmfCode: number;
  statusCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  invoiceNumber: string | null;
  modelName: string | null;
  statusDescription: string | null;
  recoveredGgNumber: string | null;
  renumberedTo: string | null;
};

export type VehicleOption = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  modelCode: number | null;
};

export type ContractSnapshot = {
  label: string;
  badgeClass: "badge" | "badge-warning" | "badge-info" | "badge-success" | "badge-error";
  targetReturnDate: string | null;
};

export type VehicleSnapshotPage = {
  rows: VehicleSnapshotRow[];
  contractsByVmf: Record<string, ContractSnapshot>;
  page: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
};

export type RenumberedVehicleReportRow = {
  oldVmfCode: number;
  oldFleetNumber: string | null;
  oldStatusDescription: string | null;
  newFleetNumber: string | null;
  newStatusDescription: string | null;
};

export type RenumberedVehicleReportPage = {
  items: RenumberedVehicleReportRow[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
};

export class VehicleApiError extends Error {
  constructor(
    public readonly reason: "unauthorized" | "unavailable" | "invalid-response",
    message: string,
  ) {
    super(message);
    this.name = "VehicleApiError";
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
    if (key in record) {
      return record[key];
    }
  }

  return undefined;
}

function asString(value: unknown) {
  if (typeof value === "string") {
    return value.trim() || null;
  }

  if (typeof value === "number" || typeof value === "bigint") {
    return String(value);
  }

  return null;
}

function asNumber(value: unknown) {
  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }

  if (typeof value === "string" && value.trim()) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }

  return null;
}

function statusDescriptionForCode(statusCode: number) {
  const descriptions: Record<number, string> = {
    1: "In Service",
    2: "Withdrawn",
    3: "Board of Survey",
    4: "Stolen",
    5: "Sold",
    6: "Transferred",
    7: "Subsidized",
    8: "From Focus",
    9: "Privatised",
    10: "Recovered",
    11: "Missing",
    12: "Destroyed",
  };

  return descriptions[statusCode] ?? null;
}

function getCollection(payload: unknown) {
  if (Array.isArray(payload)) {
    return payload;
  }

  if (isRecord(payload)) {
    const data = getValue(payload, "data", "items", "results");
    return Array.isArray(data) ? data : [];
  }

  return [];
}

function toVehicleSnapshot(value: unknown): VehicleSnapshotRow | null {
  if (!isRecord(value)) {
    return null;
  }

  const vmfCode = asNumber(getValue(value, "vmf_code", "vmfCode"));
  if (vmfCode === null) {
    return null;
  }

  const statusCode = asNumber(getValue(value, "vehicle_status_code", "vehicleStatusCode")) ?? 0;
  return {
    vmfCode,
    statusCode,
    fleetNumber: asString(getValue(value, "fleet_number", "fleetNumber")),
    registrationNumber: asString(getValue(value, "registration_number", "registrationNumber")),
    invoiceNumber: asString(getValue(value, "invoice_number", "invoiceNumber")),
    modelName: asString(getValue(value, "model_name", "modelName")),
    statusDescription:
      asString(getValue(value, "status_description", "statusDescription")) ??
      statusDescriptionForCode(statusCode),
    recoveredGgNumber: asString(getValue(value, "recovered_gg_number", "recoveredGgNumber")),
    renumberedTo: asString(getValue(value, "renumbered_to", "renumberedTo")),
  };
}

function toRenumberedVehicleReportRow(value: unknown): RenumberedVehicleReportRow | null {
  if (!isRecord(value)) {
    return null;
  }

  const oldVmfCode = asNumber(getValue(value, "oldVmfCode", "old_vmf_code"));
  if (oldVmfCode === null) {
    return null;
  }

  return {
    oldVmfCode,
    oldFleetNumber: asString(getValue(value, "oldFleetNumber", "old_fleet_number")),
    oldStatusDescription: asString(
      getValue(value, "oldStatusDescription", "old_status_description"),
    ),
    newFleetNumber: asString(getValue(value, "newFleetNumber", "new_fleet_number")),
    newStatusDescription: asString(
      getValue(value, "newStatusDescription", "new_status_description"),
    ),
  };
}

async function requestApi(path: string) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new VehicleApiError("unauthorized", "No FIS access cookie is available.");
  }

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);

  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      cache: "no-store",
      headers: {
        accept: "application/json",
        cookie: cookieHeader,
      },
      signal: controller.signal,
    });

    if (response.status === 401 || response.status === 403) {
      throw new VehicleApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (!response.ok) {
      throw new VehicleApiError("unavailable", `FIS API returned HTTP ${response.status}.`);
    }

    try {
      return (await response.json()) as unknown;
    } catch {
      throw new VehicleApiError("invalid-response", "The FIS API returned invalid JSON.");
    }
  } catch (error) {
    if (error instanceof VehicleApiError) {
      throw error;
    }

    throw new VehicleApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

function toContractSnapshot(value: unknown): ContractSnapshot {
  if (!isRecord(value)) {
    return { label: "No Contract", badgeClass: "badge", targetReturnDate: null };
  }

  const statusCode = asNumber(getValue(value, "contractStatusCode", "contract_status_code"));
  const statusText = asString(getValue(value, "status", "statusText"));

  let label: string;
  let badgeClass: ContractSnapshot["badgeClass"];

  switch (statusCode) {
    case 0:
      label = "Draft";
      badgeClass = "badge";
      break;
    case 1:
      label = "Pending Review";
      badgeClass = "badge-warning";
      break;
    case 2:
      label = "Approved";
      badgeClass = "badge-info";
      break;
    case 3:
      label = "Active";
      badgeClass = "badge-success";
      break;
    case 4:
      label = "Declined for Correction";
      badgeClass = "badge-warning";
      break;
    case 5:
      label = "Declined";
      badgeClass = "badge-error";
      break;
    case 6:
      label = "Cancelled";
      badgeClass = "badge-error";
      break;
    case 7:
      label = "Closed";
      badgeClass = "badge";
      break;
    default:
      label = statusText ?? "Unknown";
      badgeClass = "badge";
      break;
  }

  const targetReturnDate = asString(getValue(value, "targetReturnDate", "target_return_date"));
  return {
    label,
    badgeClass,
    targetReturnDate: targetReturnDate ? targetReturnDate.slice(0, 10) : null,
  };
}

async function getLatestContract(vmfCode: number): Promise<ContractSnapshot> {
  const payload = await requestApi(
    `api/contracts?page=1&pageSize=25&vmfCode=${encodeURIComponent(vmfCode)}`,
  );
  const contracts = getCollection(payload).filter(isRecord);
  const latest = contracts.toSorted((left, right) => {
    const leftCode = asNumber(getValue(left, "contractCode", "contract_id", "contract_code")) ?? 0;
    const rightCode =
      asNumber(getValue(right, "contractCode", "contract_id", "contract_code")) ?? 0;
    return rightCode - leftCode;
  })[0];

  return latest
    ? toContractSnapshot(latest)
    : ({
        label: "No Contract",
        badgeClass: "badge",
        targetReturnDate: null,
      } satisfies ContractSnapshot);
}

export async function getVehicleSnapshotPage(
  page: number,
  pageSize = PAGE_SIZE,
): Promise<VehicleSnapshotPage> {
  const safeRequestedPage = Math.max(1, Math.trunc(page) || 1);
  const safeRequestedPageSize = Math.min(100, Math.max(1, Math.trunc(pageSize) || PAGE_SIZE));
  const payload = await requestApi(
    `api/vehicles/snapshot?page=${safeRequestedPage}&pageSize=${safeRequestedPageSize}`,
  );

  if (!isRecord(payload)) {
    throw new VehicleApiError(
      "invalid-response",
      "The FIS API returned an invalid vehicle snapshot.",
    );
  }

  const parsedPage = asNumber(getValue(payload, "page"));
  const parsedPageSize = asNumber(getValue(payload, "pageSize", "page_size"));
  const totalRecords = asNumber(getValue(payload, "totalRecords", "total_records"));
  const totalPages = asNumber(getValue(payload, "totalPages", "total_pages"));

  if (
    parsedPage === null ||
    parsedPageSize === null ||
    totalRecords === null ||
    totalPages === null
  ) {
    throw new VehicleApiError(
      "invalid-response",
      "The FIS API returned incomplete vehicle snapshot pagination metadata.",
    );
  }

  const rows = getCollection(payload)
    .map(toVehicleSnapshot)
    .filter((vehicle): vehicle is VehicleSnapshotRow => vehicle !== null);

  const contractEntries = await Promise.all(
    rows.map(
      async (vehicle) =>
        [String(vehicle.vmfCode), await getLatestContract(vehicle.vmfCode)] as const,
    ),
  );

  return {
    rows,
    contractsByVmf: Object.fromEntries(contractEntries),
    page: Math.max(1, Math.trunc(parsedPage)),
    pageSize: Math.max(1, Math.trunc(parsedPageSize)),
    totalRecords: Math.max(0, Math.trunc(totalRecords)),
    totalPages: Math.max(1, Math.trunc(totalPages)),
  };
}

export async function getVehicleOptions(): Promise<VehicleOption[]> {
  const payload = await requestApi("api/vehicles");
  const vehicles: VehicleOption[] = [];
  for (const value of getCollection(payload)) {
    if (!isRecord(value)) continue;
    const vmfCode = asNumber(getValue(value, "vmf_code", "vmfCode"));
    if (vmfCode === null) continue;
    vehicles.push({
      vmfCode,
      fleetNumber: asString(getValue(value, "fleet_number", "fleetNumber")),
      registrationNumber: asString(getValue(value, "registration_number", "registrationNumber")),
      modelCode: asNumber(getValue(value, "model_code", "modelCode")),
    });
  }
  return vehicles;
}

export async function getRenumberedVehicleReportPage(
  page: number,
  pageSize = DEFAULT_RENUMBERED_REPORT_PAGE_SIZE,
): Promise<RenumberedVehicleReportPage> {
  const safeRequestedPage = Math.max(1, Math.trunc(page) || 1);
  const safeRequestedPageSize = Math.min(
    100,
    Math.max(1, Math.trunc(pageSize) || DEFAULT_RENUMBERED_REPORT_PAGE_SIZE),
  );
  const payload = await requestApi(
    `api/vehicles/renumbered/page?page=${safeRequestedPage}&pageSize=${safeRequestedPageSize}`,
  );

  if (!isRecord(payload) || !Array.isArray(payload.items)) {
    throw new VehicleApiError(
      "invalid-response",
      "The FIS API returned an invalid renumbered vehicle page.",
    );
  }

  const parsedPage = asNumber(getValue(payload, "page"));
  const parsedPageSize = asNumber(getValue(payload, "pageSize", "page_size"));
  const total = asNumber(getValue(payload, "total"));
  const totalPages = asNumber(getValue(payload, "totalPages", "total_pages"));

  if (
    parsedPage === null ||
    parsedPageSize === null ||
    total === null ||
    totalPages === null ||
    !Number.isInteger(parsedPage) ||
    !Number.isInteger(parsedPageSize) ||
    !Number.isInteger(total) ||
    !Number.isInteger(totalPages) ||
    parsedPage < 1 ||
    parsedPageSize < 1 ||
    total < 0 ||
    totalPages < 1
  ) {
    throw new VehicleApiError(
      "invalid-response",
      "The FIS API returned incomplete renumbered vehicle pagination.",
    );
  }

  return {
    items: payload.items
      .map(toRenumberedVehicleReportRow)
      .filter((item): item is RenumberedVehicleReportRow => item !== null),
    page: parsedPage,
    pageSize: parsedPageSize,
    total,
    totalPages,
  };
}
