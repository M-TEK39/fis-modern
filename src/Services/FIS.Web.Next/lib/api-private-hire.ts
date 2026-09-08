import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type PrivateHireVehicleRecord = {
  phvCode: number;
  registrationNumber: string;
  modelCode: number;
  modelDescription: string | null;
  siteCode: number;
  contractedTo: number | null;
  engineNumber: string | null;
  chassisNumber: string | null;
  yearManufactured: string | null;
  bankCode: string | null;
  colour: string | null;
  tankCapacity: number | null;
  contractorId: number;
  fuelCard: string | null;
  fuelCardReceiver: string | null;
  takeOnDate: string | null;
  takeOnOdo: number;
  returnDate: string | null;
  returnOdo: number;
  kmTariff: number | null;
  dailyTariff: number | null;
  hourlyTariff: number | null;
  hireStatus: string;
};

export type PrivateHireContractorRecord = {
  contractorId: number;
  companyName: string;
  physicalAddress: string | null;
  postalAddress: string | null;
  phone: string | null;
  faxNumber: string | null;
  email: string | null;
  contactPerson: string | null;
  active: number | null;
  type: string | null;
  quotations: boolean | null;
  projectName: string | null;
  projectBeginDate: string | null;
  projectEndDate: string | null;
  status: string;
};

export type PrivateHireApiErrorReason =
  "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class PrivateHireApiError extends Error {
  constructor(
    public readonly reason: PrivateHireApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "PrivateHireApiError";
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
  if (!cookieHeader)
    throw new PrivateHireApiError("unauthorized", "No FIS access cookie is available.");

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
    if (response.status === 401 || response.status === 403)
      throw new PrivateHireApiError("unauthorized", "The FIS access cookie was rejected.");
    if (response.status === 404)
      throw new PrivateHireApiError(
        "not-found",
        "The requested Private Hire record was not found.",
      );
    if (!response.ok)
      throw new PrivateHireApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        `FIS API returned HTTP ${response.status}.`,
      );
    return response;
  } catch (error) {
    if (error instanceof PrivateHireApiError) throw error;
    throw new PrivateHireApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new PrivateHireApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapVehicle(value: unknown): PrivateHireVehicleRecord | null {
  if (!isRecord(value)) return null;
  const phvCode = asNumber(getValue(value, "PHV_code", "phvCode", "vehicle_id"));
  const registrationNumber = asString(getValue(value, "registration_number", "registrationNumber"));
  if (phvCode === null || registrationNumber === null) return null;
  const returnDate = asString(
    getValue(value, "return_date", "returnDate", "date_retired", "hire_end_date"),
  );
  return {
    phvCode,
    registrationNumber,
    modelCode: asNumber(getValue(value, "model_code", "modelCode")) ?? 0,
    modelDescription: asString(
      getValue(value, "model_desc", "modelDescription", "model_description", "make_model"),
    ),
    siteCode: asNumber(getValue(value, "site_code", "siteCode", "department_code")) ?? 0,
    contractedTo: asNumber(getValue(value, "contracted_to", "contractedTo")),
    engineNumber: asString(getValue(value, "engine_number", "engineNumber")),
    chassisNumber: asString(getValue(value, "chassis_number", "chassisNumber")),
    yearManufactured: asString(getValue(value, "year_manufactured", "yearManufactured")),
    bankCode: asString(getValue(value, "bank_code", "bankCode")),
    colour: asString(getValue(value, "colour", "color")),
    tankCapacity: asNumber(getValue(value, "tank_capacity", "tankCapacity")),
    contractorId: asNumber(getValue(value, "contractor_id", "contractorId")) ?? 0,
    fuelCard: asString(getValue(value, "fuel_card", "fuelCard")),
    fuelCardReceiver: asString(getValue(value, "fuel_card_receiver", "fuelCardReceiver")),
    takeOnDate: asString(
      getValue(value, "take_on_date", "takeOnDate", "date_hired", "hire_start_date"),
    ),
    takeOnOdo: asNumber(getValue(value, "take_on_odo", "takeOnOdo")) ?? 0,
    returnDate,
    returnOdo: asNumber(getValue(value, "return_odo", "returnOdo")) ?? 0,
    kmTariff: asNumber(getValue(value, "km_tariff", "kmTariff")),
    dailyTariff: asNumber(getValue(value, "daily_tariff", "dailyTariff")),
    hourlyTariff: asNumber(getValue(value, "hourly_tariff", "hourlyTariff")),
    hireStatus:
      asString(getValue(value, "hire_status", "hireStatus")) ?? (returnDate ? "Retired" : "Active"),
  };
}

function mapContractor(value: unknown): PrivateHireContractorRecord | null {
  if (!isRecord(value)) return null;
  const contractorId = asNumber(getValue(value, "contractor_id", "contractorId"));
  const companyName = asString(
    getValue(value, "company_name", "companyName", "contractor_name", "contractorName"),
  );
  if (contractorId === null || companyName === null) return null;
  const active = asNumber(getValue(value, "active"));
  return {
    contractorId,
    companyName,
    physicalAddress: asString(getValue(value, "address", "physical_address", "physicalAddress")),
    postalAddress: asString(getValue(value, "postal_address", "postalAddress")),
    phone: asString(getValue(value, "phone", "tel_number", "telNumber")),
    faxNumber: asString(getValue(value, "fax_number", "faxNumber", "business_registration")),
    email: asString(getValue(value, "email", "email_address", "emailAddress")),
    contactPerson: asString(getValue(value, "contact_person", "contactPerson")),
    active,
    type: asString(getValue(value, "type")),
    quotations: asBoolean(getValue(value, "quotations")),
    projectName: asString(getValue(value, "project_name", "projectName")),
    projectBeginDate: asString(getValue(value, "project_begdat", "projectBeginDate")),
    projectEndDate: asString(getValue(value, "project_enddat", "projectEndDate")),
    status: asString(getValue(value, "status")) ?? (active === 0 ? "Inactive" : "Active"),
  };
}

async function getCollectionFromApi(path: string) {
  return getCollection(await readJson(await requestApi(path)));
}

export async function getPrivateHireVehicles() {
  return (await getCollectionFromApi("api/PrivateHire"))
    .map(mapVehicle)
    .filter((record): record is PrivateHireVehicleRecord => record !== null);
}

export async function getPrivateHireVehicle(phvCode: number) {
  const record = mapVehicle(
    await readJson(await requestApi(`api/PrivateHire/${encodeURIComponent(phvCode)}`)),
  );
  if (!record)
    throw new PrivateHireApiError(
      "invalid-response",
      "The FIS API returned an invalid Private Hire vehicle.",
    );
  return record;
}

export async function searchPrivateHireVehicles(searchTerm: string) {
  const query = new URLSearchParams({ searchTerm });
  return (await getCollectionFromApi(`api/PrivateHire/search?${query.toString()}`))
    .map(mapVehicle)
    .filter((record): record is PrivateHireVehicleRecord => record !== null);
}

export type PrivateHireVehicleInput = Omit<PrivateHireVehicleRecord, "hireStatus" | "phvCode"> & {
  phvCode?: number;
};

function vehiclePayload(input: PrivateHireVehicleInput) {
  return {
    PHV_code: input.phvCode ?? 0,
    registration_number: input.registrationNumber,
    model_code: input.modelCode,
    site_code: input.siteCode,
    contracted_to: input.contractedTo,
    engine_number: input.engineNumber,
    chassis_number: input.chassisNumber,
    year_manufactured: input.yearManufactured,
    bank_code: input.bankCode,
    colour: input.colour,
    tank_capacity: input.tankCapacity,
    contractor_id: input.contractorId,
    fuel_card: input.fuelCard,
    fuel_card_receiver: input.fuelCardReceiver,
    take_on_date: input.takeOnDate,
    take_on_odo: input.takeOnOdo,
    return_date: input.returnDate,
    return_odo: input.returnOdo,
    km_tariff: input.kmTariff,
    daily_tariff: input.dailyTariff,
    hourly_tariff: input.hourlyTariff,
    model_desc: input.modelDescription,
  };
}

export async function createPrivateHireVehicle(input: PrivateHireVehicleInput) {
  const record = mapVehicle(
    await readJson(
      await requestApi("api/PrivateHire", {
        method: "POST",
        body: JSON.stringify(vehiclePayload(input)),
      }),
    ),
  );
  if (!record)
    throw new PrivateHireApiError(
      "invalid-response",
      "The FIS API returned an invalid created Private Hire vehicle.",
    );
  return record;
}

export async function updatePrivateHireVehicle(phvCode: number, input: PrivateHireVehicleInput) {
  const record = mapVehicle(
    await readJson(
      await requestApi(`api/PrivateHire/${encodeURIComponent(phvCode)}`, {
        method: "PUT",
        body: JSON.stringify(vehiclePayload({ ...input, phvCode })),
      }),
    ),
  );
  if (!record)
    throw new PrivateHireApiError(
      "invalid-response",
      "The FIS API returned an invalid updated Private Hire vehicle.",
    );
  return record;
}

export async function deletePrivateHireVehicle(phvCode: number) {
  await requestApi(`api/PrivateHire/${encodeURIComponent(phvCode)}`, { method: "DELETE" });
}

export async function getPrivateHireContractors() {
  return (await getCollectionFromApi("api/PrivateHire/contractors"))
    .map(mapContractor)
    .filter((record): record is PrivateHireContractorRecord => record !== null);
}

export async function getPrivateHireContractor(contractorId: number) {
  const record = mapContractor(
    await readJson(
      await requestApi(`api/PrivateHire/contractors/${encodeURIComponent(contractorId)}`),
    ),
  );
  if (!record)
    throw new PrivateHireApiError(
      "invalid-response",
      "The FIS API returned an invalid Private Hire contractor.",
    );
  return record;
}

export type PrivateHireContractorInput = Omit<
  PrivateHireContractorRecord,
  "status" | "contractorId"
> & { contractorId?: number; status?: string };

function contractorPayload(input: PrivateHireContractorInput) {
  return {
    contractor_id: input.contractorId ?? 0,
    company_name: input.companyName,
    contact_person: input.contactPerson,
    phone: input.phone,
    email: input.email,
    business_registration: input.faxNumber,
    address: input.physicalAddress,
    status: input.status ?? (input.active === 0 ? "Inactive" : "Active"),
    postal_address: input.postalAddress,
    fax_number: input.faxNumber,
    quotations: input.quotations,
    type: input.type,
    project_name: input.projectName,
    project_begdat: input.projectBeginDate,
    project_enddat: input.projectEndDate,
  };
}

export async function createPrivateHireContractor(input: PrivateHireContractorInput) {
  const record = mapContractor(
    await readJson(
      await requestApi("api/PrivateHire/contractors", {
        method: "POST",
        body: JSON.stringify(contractorPayload(input)),
      }),
    ),
  );
  if (!record)
    throw new PrivateHireApiError(
      "invalid-response",
      "The FIS API returned an invalid created Private Hire contractor.",
    );
  return record;
}

export async function updatePrivateHireContractor(
  contractorId: number,
  input: PrivateHireContractorInput,
) {
  const record = mapContractor(
    await readJson(
      await requestApi(`api/PrivateHire/contractors/${encodeURIComponent(contractorId)}`, {
        method: "PUT",
        body: JSON.stringify(contractorPayload({ ...input, contractorId })),
      }),
    ),
  );
  if (!record)
    throw new PrivateHireApiError(
      "invalid-response",
      "The FIS API returned an invalid updated Private Hire contractor.",
    );
  return record;
}

export async function deletePrivateHireContractor(contractorId: number) {
  await requestApi(`api/PrivateHire/contractors/${encodeURIComponent(contractorId)}`, {
    method: "DELETE",
  });
}
