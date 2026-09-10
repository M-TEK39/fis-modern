import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type RecoveredVehicleSearchMode = "GG" | "GP";

export type RecoveredVehicleSearchResult = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  vehicleStatusCode: number;
  statusDescription: string | null;
  renumberedTo: string | null;
};

export type RecoveredVehicleStatusOption = {
  code: number;
  description: string;
};

export type RecoveredVehicleDetails = RecoveredVehicleSearchResult & {
  previousFleetNumber: string | null;
  previousDateChanged: string | null;
  statusOptions: RecoveredVehicleStatusOption[];
};

export type RecoveredVehicleUpdateResult = {
  updatedVehicle: RecoveredVehicleDetails;
  newVmfCode: number;
};

export type RecoveredVehicleApiErrorReason =
  "unauthorized" | "unavailable" | "invalid-response" | "conflict" | "not-found";

export class RecoveredVehicleApiError extends Error {
  constructor(
    public readonly reason: RecoveredVehicleApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "RecoveredVehicleApiError";
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

function getCollection(payload: unknown) {
  if (Array.isArray(payload)) {
    return payload;
  }

  if (isRecord(payload)) {
    const collection = getValue(payload, "data", "items", "results");
    return Array.isArray(collection) ? collection : [];
  }

  return [];
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new RecoveredVehicleApiError("unauthorized", "No FIS access cookie is available.");
  }

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
      throw new RecoveredVehicleApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (response.status === 404) {
      throw new RecoveredVehicleApiError("not-found", "The recovered vehicle was not found.");
    }

    if (response.status === 409) {
      throw new RecoveredVehicleApiError(
        "conflict",
        await readErrorMessage(response, "The recovered GG number already exists."),
      );
    }

    if (!response.ok) {
      throw new RecoveredVehicleApiError(
        "unavailable",
        await readErrorMessage(response, `FIS API returned HTTP ${response.status}.`),
      );
    }

    return response;
  } catch (error) {
    if (error instanceof RecoveredVehicleApiError) {
      throw error;
    }

    throw new RecoveredVehicleApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readErrorMessage(response: Response, fallback: string) {
  try {
    const payload = (await response.json()) as unknown;
    return isRecord(payload)
      ? (asString(getValue(payload, "message", "error")) ?? fallback)
      : fallback;
  } catch {
    return fallback;
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new RecoveredVehicleApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapSearchResult(value: unknown): RecoveredVehicleSearchResult | null {
  if (!isRecord(value)) {
    return null;
  }

  const vmfCode = asNumber(getValue(value, "vmfCode", "VmfCode", "vmf_code"));
  const statusCode = asNumber(
    getValue(value, "vehicleStatusCode", "VehicleStatusCode", "vehicle_status_code"),
  );
  if (vmfCode === null || statusCode === null) {
    return null;
  }

  return {
    vmfCode,
    fleetNumber: asString(getValue(value, "fleetNumber", "FleetNumber", "fleet_number")),
    registrationNumber: asString(
      getValue(value, "registrationNumber", "RegistrationNumber", "registration_number"),
    ),
    vehicleStatusCode: statusCode,
    statusDescription: asString(
      getValue(value, "statusDescription", "StatusDescription", "status_description"),
    ),
    renumberedTo: asString(getValue(value, "renumberedTo", "RenumberedTo", "renumbered_to")),
  };
}

function mapStatusOption(value: unknown): RecoveredVehicleStatusOption | null {
  if (!isRecord(value)) {
    return null;
  }

  const code = asNumber(getValue(value, "code", "Code"));
  const description = asString(getValue(value, "description", "Description"));
  return code !== null && description ? { code, description } : null;
}

function mapDetails(value: unknown): RecoveredVehicleDetails | null {
  const result = mapSearchResult(value);
  if (!result || !isRecord(value)) {
    return null;
  }

  return {
    ...result,
    previousFleetNumber: asString(getValue(value, "previousFleetNumber", "PreviousFleetNumber")),
    previousDateChanged: asString(getValue(value, "previousDateChanged", "PreviousDateChanged")),
    statusOptions: getCollection(getValue(value, "statusOptions", "StatusOptions"))
      .map(mapStatusOption)
      .filter((option): option is RecoveredVehicleStatusOption => option !== null),
  };
}

export async function getRecoveredVehicleSearch(
  searchTerm: string,
  mode: RecoveredVehicleSearchMode,
) {
  const query = new URLSearchParams({ mode, search: searchTerm.trim() });
  const payload = await readJson(
    await requestApi(`api/vehicles/recovered/search?${query.toString()}`),
  );
  return getCollection(payload)
    .map(mapSearchResult)
    .filter((vehicle): vehicle is RecoveredVehicleSearchResult => vehicle !== null);
}

export async function getRecoveredVehicleDetails(vmfCode: number) {
  const payload = await readJson(
    await requestApi(`api/vehicles/recovered/${encodeURIComponent(vmfCode)}`),
  );
  const details = mapDetails(payload);
  if (!details) {
    throw new RecoveredVehicleApiError(
      "invalid-response",
      "The FIS API returned invalid recovered vehicle details.",
    );
  }

  return details;
}

export async function updateRecoveredVehicle(input: {
  vmfCode: number;
  recoveredFleetNumber: string;
  dateChanged: string;
  newStatusCode: number;
}) {
  const payload = await readJson(
    await requestApi("api/vehicles/recovered", {
      method: "POST",
      body: JSON.stringify({
        vmfCode: input.vmfCode,
        recoveredFleetNumber: input.recoveredFleetNumber,
        dateChanged: input.dateChanged,
        newStatusCode: input.newStatusCode,
      }),
    }),
  );

  if (!isRecord(payload)) {
    throw new RecoveredVehicleApiError(
      "invalid-response",
      "The FIS API returned invalid recovered vehicle update details.",
    );
  }

  const updatedVehicle = mapDetails(getValue(payload, "updatedVehicle", "UpdatedVehicle"));
  const newVmfCode = asNumber(getValue(payload, "newVmfCode", "NewVmfCode"));
  if (!updatedVehicle || newVmfCode === null) {
    throw new RecoveredVehicleApiError(
      "invalid-response",
      "The FIS API returned incomplete recovered vehicle update details.",
    );
  }

  return { updatedVehicle, newVmfCode } satisfies RecoveredVehicleUpdateResult;
}
