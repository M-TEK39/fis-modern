import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type VehicleSource = {
  vsCode: number;
  name: string;
  physicalAddress: string;
  postalAddress: string;
  telephoneNumber: string;
  faxNumber: string;
  emailAddress: string;
  contactPerson: string;
};

export type VehicleSourceInput = Omit<VehicleSource, "vsCode">;

export type VehicleSourceCapabilities = {
  emailAddress: boolean;
  contactPerson: boolean;
};

export type VehicleSourcePage = {
  items: VehicleSource[];
  capabilities: VehicleSourceCapabilities;
};

export type VehicleSourceApiErrorReason = "unauthorized" | "unavailable" | "invalid-response" | "not-found" | "conflict";

export class VehicleSourceApiError extends Error {
  constructor(
    public readonly reason: VehicleSourceApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "VehicleSourceApiError";
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
    return value;
  }

  if (typeof value === "number" || typeof value === "bigint") {
    return String(value);
  }

  return "";
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

function asBoolean(value: unknown) {
  return typeof value === "boolean" ? value : false;
}

function getCollection(payload: unknown) {
  if (Array.isArray(payload)) {
    return payload;
  }

  if (isRecord(payload)) {
    const collection = getValue(payload, "items", "data", "results");
    return Array.isArray(collection) ? collection : [];
  }

  return [];
}

function mapSource(value: unknown): VehicleSource | null {
  if (!isRecord(value)) {
    return null;
  }

  const vsCode = asNumber(getValue(value, "vsCode", "vs_code"));
  if (vsCode === null) {
    return null;
  }

  return {
    vsCode,
    name: asString(getValue(value, "name")),
    physicalAddress: asString(getValue(value, "physicalAddress", "physical_address")),
    postalAddress: asString(getValue(value, "postalAddress", "postal_address")),
    telephoneNumber: asString(getValue(value, "telephoneNumber", "tel_number", "telephone")),
    faxNumber: asString(getValue(value, "faxNumber", "fax_number", "fax")),
    emailAddress: asString(getValue(value, "emailAddress", "email_address", "email")),
    contactPerson: asString(getValue(value, "contactPerson", "contact_person", "contact")),
  };
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new VehicleSourceApiError("unauthorized", "No FIS access cookie is available.");
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
      throw new VehicleSourceApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (response.status === 404) {
      throw new VehicleSourceApiError("not-found", "The requested vehicle source was not found.");
    }

    if (response.status === 409) {
      throw new VehicleSourceApiError("conflict", await getErrorMessage(response, "The vehicle source could not be saved."));
    }

    if (!response.ok) {
      throw new VehicleSourceApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        await getErrorMessage(response, `FIS API returned HTTP ${response.status}.`),
      );
    }

    return response;
  } catch (error) {
    if (error instanceof VehicleSourceApiError) {
      throw error;
    }

    throw new VehicleSourceApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function getErrorMessage(response: Response, fallback: string) {
  try {
    const payload = (await response.json()) as unknown;
    if (isRecord(payload)) {
      return asString(getValue(payload, "message", "error")) || fallback;
    }

    return typeof payload === "string" && payload.trim() ? payload.trim() : fallback;
  } catch {
    return fallback;
  }
}

export async function getVehicleSources(): Promise<VehicleSourcePage> {
  const response = await requestApi("api/vehicle-source");
  let payload: unknown;
  try {
    payload = await response.json();
  } catch {
    throw new VehicleSourceApiError("invalid-response", "The FIS API returned invalid vehicle source data.");
  }

  const items = getCollection(payload)
    .map(mapSource)
    .filter((source): source is VehicleSource => source !== null);
  const capabilities = isRecord(payload) && isRecord(payload.capabilities) ? payload.capabilities : {};

  return {
    items,
    capabilities: {
      emailAddress: asBoolean(getValue(capabilities, "emailAddress", "email_address")),
      contactPerson: asBoolean(getValue(capabilities, "contactPerson", "contact_person")),
    },
  };
}

export async function createVehicleSource(input: VehicleSourceInput) {
  await requestApi("api/vehicle-source", {
    method: "POST",
    body: JSON.stringify({
      name: input.name,
      physicalAddress: input.physicalAddress,
      postalAddress: input.postalAddress,
      telephoneNumber: input.telephoneNumber,
      faxNumber: input.faxNumber,
      emailAddress: input.emailAddress,
      contactPerson: input.contactPerson,
    }),
  });
}

export async function updateVehicleSource(vsCode: number, input: VehicleSourceInput) {
  await requestApi(`api/vehicle-source/${encodeURIComponent(vsCode)}`, {
    method: "PUT",
    body: JSON.stringify({
      name: input.name,
      physicalAddress: input.physicalAddress,
      postalAddress: input.postalAddress,
      telephoneNumber: input.telephoneNumber,
      faxNumber: input.faxNumber,
      emailAddress: input.emailAddress,
      contactPerson: input.contactPerson,
    }),
  });
}
