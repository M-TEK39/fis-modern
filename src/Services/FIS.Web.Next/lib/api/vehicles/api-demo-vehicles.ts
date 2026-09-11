import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
const DEMO_VEHICLE_REPORT_PAGE_SIZE = 24;
type JsonRecord = Record<string, unknown>;

export type DemoVehicleRecord = {
  demoVehicleCode: number;
  ggNumber: string | null;
  registrationNumber: string | null;
  modelDescription: string | null;
  yearManufactured: number | null;
  siteCode: number | null;
  siteDescription: string | null;
  bankCode: string | null;
  tank: number | null;
  colour: string | null;
  engineNumber: string | null;
  chassisNumber: string | null;
  dateCreated: string | null;
  dateUpdated: string | null;
  createdByUserCode: number | null;
  modifiedByUserCode: number | null;
};

export type DemoVehicleReportPage = {
  items: DemoVehicleRecord[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
};

export type DemoVehicleSearchMode = "GG" | "GP";

export type DemoVehicleWriteInput = {
  ggNumber: string;
  registrationNumber: string;
  modelDescription: string;
  siteCode: string;
  yearManufactured: string;
  bankCode: string;
  tank: string;
  colour: string;
  engineNumber: string;
  chassisNumber: string;
};

export type DemoVehicleApiErrorReason =
  "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class DemoVehicleApiError extends Error {
  constructor(
    public readonly reason: DemoVehicleApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "DemoVehicleApiError";
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
  if (typeof value === "number" || typeof value === "bigint") return String(value);
  return null;
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader)
    throw new DemoVehicleApiError("unauthorized", "No FIS access cookie is available.");

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers: { accept: "application/json", cookie: cookieHeader, ...init.headers },
      signal: controller.signal,
    });

    if (response.status === 401 || response.status === 403) {
      throw new DemoVehicleApiError(
        "unauthorized",
        "The FIS access cookie was rejected.",
        response.status,
      );
    }
    if (response.status === 404) {
      throw new DemoVehicleApiError(
        "not-found",
        "The requested demo vehicle was not found.",
        response.status,
      );
    }
    if (!response.ok) {
      let message = `FIS API returned HTTP ${response.status}.`;
      try {
        const payload = await response.clone().json();
        if (isRecord(payload))
          message = asString(getValue(payload, "message", "Message", "error")) ?? message;
      } catch {
        // Keep the status-based message when the API body is not JSON.
      }
      throw new DemoVehicleApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
        response.status,
      );
    }
    return response;
  } catch (error) {
    if (error instanceof DemoVehicleApiError) throw error;
    throw new DemoVehicleApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new DemoVehicleApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapDemoVehicle(value: unknown): DemoVehicleRecord | null {
  if (!isRecord(value)) return null;
  const demoVehicleCode = asNumber(
    getValue(value, "demo_vehicle_code", "demoVehicleCode", "DemoVehicleCode"),
  );
  if (demoVehicleCode === null) return null;

  return {
    demoVehicleCode,
    ggNumber: asString(getValue(value, "gg_number", "ggNumber", "GgNumber")),
    registrationNumber: asString(
      getValue(value, "reg_number", "registrationNumber", "RegistrationNumber"),
    ),
    modelDescription: asString(
      getValue(value, "model_description", "modelDescription", "ModelDescription"),
    ),
    yearManufactured: asNumber(getValue(value, "year_mnf", "yearManufactured", "YearManufactured")),
    siteCode: asNumber(getValue(value, "site_code", "siteCode", "SiteCode")),
    siteDescription: asString(
      getValue(value, "site_description", "siteDescription", "SiteDescription"),
    ),
    bankCode: asString(getValue(value, "bank_code", "bankCode", "BankCode")),
    tank: asNumber(getValue(value, "tank", "Tank")),
    colour: asString(getValue(value, "colour", "color", "Colour")),
    engineNumber: asString(getValue(value, "engine_number", "engineNumber", "EngineNumber")),
    chassisNumber: asString(getValue(value, "chassis_number", "chassisNumber", "ChassisNumber")),
    dateCreated: asString(getValue(value, "date_created", "dateCreated", "DateCreated")),
    dateUpdated: asString(getValue(value, "date_updated", "dateUpdated", "DateUpdated")),
    createdByUserCode: asNumber(
      getValue(value, "created_by_user_code", "createdByUserCode", "CreatedByUserCode"),
    ),
    modifiedByUserCode: asNumber(
      getValue(value, "modified_by_user_code", "modifiedByUserCode", "ModifiedByUserCode"),
    ),
  };
}

async function readCollection(response: Response) {
  const payload = await readJson(response);
  if (!Array.isArray(payload)) {
    throw new DemoVehicleApiError("invalid-response", "The demo vehicle response was not a list.");
  }
  return payload
    .map(mapDemoVehicle)
    .filter((vehicle): vehicle is DemoVehicleRecord => vehicle !== null);
}

function readPageMetadata(payload: JsonRecord) {
  const page = asNumber(getValue(payload, "page", "Page"));
  const pageSize = asNumber(getValue(payload, "pageSize", "PageSize", "page_size"));
  const total = asNumber(getValue(payload, "total", "Total"));
  const totalPages = asNumber(getValue(payload, "totalPages", "TotalPages", "total_pages"));

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

async function readReportPage(response: Response): Promise<DemoVehicleReportPage> {
  const payload = await readJson(response);
  if (!isRecord(payload) || !Array.isArray(payload.items)) {
    throw new DemoVehicleApiError(
      "invalid-response",
      "The FIS API returned an invalid demo vehicle report page.",
    );
  }

  const metadata = readPageMetadata(payload);
  if (!metadata) {
    throw new DemoVehicleApiError(
      "invalid-response",
      "The FIS API returned incomplete demo vehicle pagination metadata.",
    );
  }

  return {
    items: payload.items
      .map(mapDemoVehicle)
      .filter((vehicle): vehicle is DemoVehicleRecord => vehicle !== null),
    ...metadata,
  };
}

export async function getDemoVehicles() {
  return readCollection(await requestApi("api/demo-vehicles"));
}

export async function getDemoVehicleReport() {
  return readCollection(await requestApi("api/demo-vehicles/reports/all"));
}

export async function getDemoVehicleReportPage(page = 1): Promise<DemoVehicleReportPage> {
  const safePage = Math.max(1, Math.trunc(page) || 1);
  const query = new URLSearchParams({
    page: String(safePage),
    pageSize: String(DEMO_VEHICLE_REPORT_PAGE_SIZE),
  });

  return readReportPage(await requestApi(`api/demo-vehicles/reports/all/page?${query.toString()}`));
}

export async function searchDemoVehicles(search: string, mode: DemoVehicleSearchMode) {
  const query = new URLSearchParams({ mode, search });
  return readCollection(await requestApi(`api/demo-vehicles/search?${query.toString()}`));
}

export async function getDemoVehicle(demoVehicleCode: number) {
  return mapDemoVehicle(
    await readJson(await requestApi(`api/demo-vehicles/${encodeURIComponent(demoVehicleCode)}`)),
  );
}

function toRequest(input: DemoVehicleWriteInput) {
  return {
    gg_number: input.ggNumber,
    reg_number: input.registrationNumber,
    model_description: input.modelDescription,
    site_code: input.siteCode,
    year_mnf: input.yearManufactured,
    bank_code: input.bankCode,
    tank: input.tank,
    colour: input.colour,
    engine_number: input.engineNumber,
    chassis_number: input.chassisNumber,
  };
}

export async function createDemoVehicle(input: DemoVehicleWriteInput) {
  const vehicle = mapDemoVehicle(
    await readJson(
      await requestApi("api/demo-vehicles", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(toRequest(input)),
      }),
    ),
  );
  if (!vehicle)
    throw new DemoVehicleApiError(
      "invalid-response",
      "The created demo vehicle response was invalid.",
    );
  return vehicle;
}

export async function updateDemoVehicle(demoVehicleCode: number, input: DemoVehicleWriteInput) {
  const vehicle = mapDemoVehicle(
    await readJson(
      await requestApi(`api/demo-vehicles/${encodeURIComponent(demoVehicleCode)}`, {
        method: "PUT",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(toRequest(input)),
      }),
    ),
  );
  if (!vehicle)
    throw new DemoVehicleApiError(
      "invalid-response",
      "The updated demo vehicle response was invalid.",
    );
  return vehicle;
}

export async function deleteDemoVehicle(demoVehicleCode: number) {
  await requestApi(`api/demo-vehicles/${encodeURIComponent(demoVehicleCode)}`, {
    method: "DELETE",
  });
}
