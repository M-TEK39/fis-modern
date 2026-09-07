import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
const PAGE_SIZE = 12;

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

function mapPresent<T>(values: readonly unknown[], mapper: (value: unknown) => T | null) {
  const result: T[] = [];
  for (const value of values) {
    const mapped = mapper(value);
    if (mapped !== null) {
      result.push(mapped);
    }
  }
  return result;
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
      asString(getValue(value, "status_description", "statusDescription")) ?? statusDescriptionForCode(statusCode),
    recoveredGgNumber: asString(getValue(value, "recovered_gg_number", "recoveredGgNumber")),
    renumberedTo: asString(getValue(value, "renumbered_to", "renumberedTo")),
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
  const payload = await requestApi(`api/contracts?page=1&pageSize=25&vmfCode=${encodeURIComponent(vmfCode)}`);
  const contracts = getCollection(payload).filter(isRecord);
  const latest = contracts.toSorted((left, right) => {
    const leftCode = asNumber(getValue(left, "contractCode", "contract_id", "contract_code")) ?? 0;
    const rightCode = asNumber(getValue(right, "contractCode", "contract_id", "contract_code")) ?? 0;
    return rightCode - leftCode;
  })[0];

  return latest
    ? toContractSnapshot(latest)
    : { label: "No Contract", badgeClass: "badge", targetReturnDate: null } satisfies ContractSnapshot;
}

export async function getVehicleSnapshotPage(page: number, pageSize = PAGE_SIZE): Promise<VehicleSnapshotPage> {
  const payload = await requestApi("api/vehicles");
  const vehicles = getCollection(payload)
    .map(toVehicleSnapshot)
    .filter((vehicle): vehicle is VehicleSnapshotRow => vehicle !== null);
  const totalRecords = vehicles.length;
  const totalPages = Math.max(1, Math.ceil(totalRecords / pageSize));
  const safePage = Math.min(Math.max(page, 1), totalPages);
  const rows = vehicles.slice((safePage - 1) * pageSize, safePage * pageSize);

  const contractEntries = await Promise.all(
    rows.map(async (vehicle) => [String(vehicle.vmfCode), await getLatestContract(vehicle.vmfCode)] as const),
  );

  return {
    rows,
    contractsByVmf: Object.fromEntries(contractEntries),
    page: safePage,
    pageSize,
    totalRecords,
    totalPages,
  };
}

export async function getVehicleOptions(): Promise<VehicleOption[]> {
  const payload = await requestApi("api/vehicles");
  return getCollection(payload)
    .filter(isRecord)
    .map((value) => {
      const vmfCode = asNumber(getValue(value, "vmf_code", "vmfCode"));
      if (vmfCode === null) return null;
      return {
        vmfCode,
        fleetNumber: asString(getValue(value, "fleet_number", "fleetNumber")),
        registrationNumber: asString(getValue(value, "registration_number", "registrationNumber")),
        modelCode: asNumber(getValue(value, "model_code", "modelCode")),
      } satisfies VehicleOption;
    })
    .filter((vehicle): vehicle is VehicleOption => vehicle !== null);
}

export async function getRenumberedVehicleReport(): Promise<RenumberedVehicleReportRow[]> {
  const payload = await requestApi("api/vehicles");
  if (isRecord(payload) && !["data", "items", "results"].some((key) => key in payload)) {
    throw new VehicleApiError("invalid-response", "The FIS API returned an unexpected vehicle collection.");
  }

  const vehicles = mapPresent(getCollection(payload), toVehicleSnapshot);
  const vehiclesByFleetNumber = new Map<string, VehicleSnapshotRow>();
  for (const vehicle of vehicles) {
    if (vehicle.fleetNumber) {
      vehiclesByFleetNumber.set(vehicle.fleetNumber.trim().toLocaleLowerCase(), vehicle);
    }
  }

  return vehicles.reduce<RenumberedVehicleReportRow[]>((rows, vehicle) => {
    if (!vehicle.renumberedTo) {
      return rows;
    }

      const replacement = vehiclesByFleetNumber.get(vehicle.renumberedTo!.trim().toLocaleLowerCase());

      rows.push({
        oldVmfCode: vehicle.vmfCode,
        oldFleetNumber: vehicle.fleetNumber,
        oldStatusDescription: vehicle.statusDescription,
        newFleetNumber: vehicle.renumberedTo,
        newStatusDescription: replacement?.statusDescription ?? null,
      });
      return rows;
    }, [])
    .sort((left, right) => (left.oldFleetNumber ?? "").localeCompare(right.oldFleetNumber ?? ""));
}
