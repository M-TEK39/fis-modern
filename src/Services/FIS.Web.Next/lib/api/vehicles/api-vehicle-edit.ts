import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type VehicleEditVehicle = {
  vmfCode: number;
  fleetNumber: string;
  registrationNumber: string;
  previousGgNumber: string | null;
  followupGgNumber: string | null;
  recoveredGgNumber: string | null;
  renumberedTo: string | null;
  assetNumber: string | null;
  modelCode: number;
  modelName: string | null;
  typeCode: number;
  typeName: string | null;
  vehicleStatusCode: number;
  statusDescription: string | null;
  locationCode: number;
  locationDescription: string | null;
  siteCode: number | null;
  takeOnDate: string | null;
  takeOnOdo: number;
  currentOdo: number;
  odoAdjustment: number | null;
  odoUpdateDate: string | null;
  firstRegistrationDate: string | null;
  vehicleStatusDate: string | null;
  engineNumber: string;
  chassisNumber: string;
  tare: number | null;
  gvm: number | null;
  yearManufactured: number | null;
  colour: string;
  transmission: string | null;
  optionalExtras: string | null;
  towHitch: string | null;
  canopy: string | null;
  additionalFuelTank: number | null;
  averageConsumption: number | null;
  licenceDueDate: string | null;
  fuelCardNumber: string | null;
  fuelCardDate: string | null;
  maintCardNumber: string | null;
  maintCardExpiry: string | null;
  operatorCardNumber: string | null;
  purchaseDate: string | null;
  purchaseAmount: number | null;
  purchasedFrom: string | null;
  invoiceNumber: string | null;
  bookValue: number | null;
  bookValueDate: string | null;
  soldTo: string | null;
  soldDate: string | null;
  soldAmount: number | null;
  monthlyOverhead: number | null;
  serviceLastDone: string | null;
  serviceLastOdo: number | null;
  cofLastDone: string | null;
  cofRequired: string | null;
  cofNumber: string | null;
  cofAmount: number | null;
  licenceRegisterNumber: string | null;
  ifmsVehicleRegisterNumber: string | null;
  natisModelNumber: string | null;
  dateCreated: string | null;
  dateUpdated: string | null;
  capturedDate: string | null;
};

export type VehicleUpdateRequest = {
  model_code: number;
  type_code: number;
  vehicle_status_code: number;
  location_code: number;
  fleet_number: string;
  registration_number: string;
  engine_number_1: string;
  chassis_number: string;
  take_on_odo: number;
  current_odo: number;
  tare: number;
  gvm: number | null;
  year_manufactured: number;
  colour: string;
  ifms_vehicle_register_number: string | null;
  natis_model_number: string | null;
  recalculate_tariff: boolean;
};

export type VehicleEditApiErrorReason =
  | "unauthorized"
  | "forbidden"
  | "unavailable"
  | "invalid-response"
  | "not-found";

export class VehicleEditApiError extends Error {
  constructor(
    public readonly reason: VehicleEditApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "VehicleEditApiError";
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
    return value.trim();
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

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new VehicleEditApiError("unauthorized", "No FIS access cookie is available.");
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

    if (response.status === 401) {
      throw new VehicleEditApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (response.status === 403) {
      throw new VehicleEditApiError(
        "forbidden",
        "Your account is not assigned the required Vehicle Master role.",
      );
    }

    if (response.status === 404) {
      throw new VehicleEditApiError("not-found", "The requested vehicle was not found.");
    }

    if (!response.ok) {
      let message = `FIS API returned HTTP ${response.status}.`;
      try {
        const payload = (await response.json()) as unknown;
        if (isRecord(payload)) {
          message = asString(getValue(payload, "message", "error")) || message;
        } else if (typeof payload === "string" && payload.trim()) {
          message = payload.trim();
        }
      } catch {
        // Keep the status-based message when the API has no JSON error body.
      }

      throw new VehicleEditApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
      );
    }

    return response;
  } catch (error) {
    if (error instanceof VehicleEditApiError) {
      throw error;
    }

    throw new VehicleEditApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new VehicleEditApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapVehicle(payload: unknown): VehicleEditVehicle {
  if (!isRecord(payload)) {
    throw new VehicleEditApiError("invalid-response", "The FIS API returned an invalid vehicle.");
  }

  const vmfCode = asNumber(getValue(payload, "vmf_code", "vmfCode"));
  if (vmfCode === null) {
    throw new VehicleEditApiError(
      "invalid-response",
      "The FIS API returned a vehicle without a VMF code.",
    );
  }

  return {
    vmfCode,
    fleetNumber: asString(getValue(payload, "fleet_number", "fleetNumber")),
    registrationNumber: asString(getValue(payload, "registration_number", "registrationNumber")),
    previousGgNumber: asString(getValue(payload, "previos_gg_number", "previousGgNumber")) || null,
    followupGgNumber: asString(getValue(payload, "followup_gg_number", "followupGgNumber")) || null,
    recoveredGgNumber:
      asString(getValue(payload, "recovered_gg_number", "recoveredGgNumber")) || null,
    renumberedTo: asString(getValue(payload, "renumbered_to", "renumberedTo")) || null,
    assetNumber: asString(getValue(payload, "asset_number", "assetNumber")) || null,
    modelCode: asNumber(getValue(payload, "model_code", "modelCode")) ?? 0,
    modelName: asString(getValue(payload, "model_name", "modelName")) || null,
    typeCode: asNumber(getValue(payload, "type_code", "typeCode")) ?? 0,
    typeName: asString(getValue(payload, "type_name", "typeName")) || null,
    vehicleStatusCode: asNumber(getValue(payload, "vehicle_status_code", "vehicleStatusCode")) ?? 0,
    statusDescription:
      asString(
        getValue(payload, "status_description", "statusDescription", "vehicle_status_description"),
      ) || null,
    locationCode: asNumber(getValue(payload, "location_code", "locationCode")) ?? 0,
    locationDescription:
      asString(getValue(payload, "location_description", "locationDescription")) || null,
    siteCode: asNumber(getValue(payload, "site_code", "siteCode", "Site_code")),
    takeOnDate: asString(getValue(payload, "take_on_date", "takeOnDate")) || null,
    takeOnOdo: asNumber(getValue(payload, "take_on_odo", "takeOnOdo")) ?? 0,
    currentOdo: asNumber(getValue(payload, "current_odo", "currentOdo")) ?? 0,
    odoAdjustment: asNumber(getValue(payload, "odo_adjustment", "odoAdjustment")),
    odoUpdateDate: asString(getValue(payload, "odo_update_date", "odoUpdateDate")) || null,
    firstRegistrationDate:
      asString(getValue(payload, "date_First_Regist", "firstRegistrationDate")) || null,
    vehicleStatusDate:
      asString(getValue(payload, "vehicle_status_date", "vehicleStatusDate")) || null,
    engineNumber: asString(getValue(payload, "engine_number_1", "engineNumber1", "engine_number")),
    chassisNumber: asString(getValue(payload, "chassis_number", "chassisNumber")),
    tare: asNumber(getValue(payload, "tare")),
    gvm: asNumber(getValue(payload, "gvm")),
    yearManufactured: asNumber(getValue(payload, "year_manufactured", "yearManufactured")),
    colour: asString(getValue(payload, "colour")),
    transmission: asString(getValue(payload, "transmission")) || null,
    optionalExtras: asString(getValue(payload, "optional_extras", "optionalExtras")) || null,
    towHitch: asString(getValue(payload, "tow_hitch", "towHitch")) || null,
    canopy: asString(getValue(payload, "canopy")) || null,
    additionalFuelTank: asNumber(getValue(payload, "additional_fuel_tank", "additionalFuelTank")),
    averageConsumption: asNumber(getValue(payload, "average_consumption", "averageConsumption")),
    licenceDueDate: asString(getValue(payload, "licence_due_date", "licenceDueDate")) || null,
    fuelCardNumber: asString(getValue(payload, "fuel_card_number", "fuelCardNumber")) || null,
    fuelCardDate: asString(getValue(payload, "fuel_card_date", "fuelCardDate")) || null,
    maintCardNumber: asString(getValue(payload, "maint_card_number", "maintCardNumber")) || null,
    maintCardExpiry: asString(getValue(payload, "maint_card_exdate", "maintCardExpiry")) || null,
    operatorCardNumber:
      asString(getValue(payload, "operator_card_number", "operatorCardNumber")) || null,
    purchaseDate: asString(getValue(payload, "purchase_date", "purchaseDate")) || null,
    purchaseAmount: asNumber(getValue(payload, "purchase_amount", "purchaseAmount")),
    purchasedFrom: asString(getValue(payload, "purchased_from", "purchasedFrom")) || null,
    invoiceNumber: asString(getValue(payload, "invoice_number", "invoiceNumber")) || null,
    bookValue: asNumber(getValue(payload, "book_value", "bookValue")),
    bookValueDate: asString(getValue(payload, "book_value_date", "bookValueDate")) || null,
    soldTo: asString(getValue(payload, "sold_to", "soldTo")) || null,
    soldDate: asString(getValue(payload, "sold_date", "soldDate")) || null,
    soldAmount: asNumber(getValue(payload, "sold_amount", "soldAmount")),
    monthlyOverhead: asNumber(getValue(payload, "monthly_overhead", "monthlyOverhead")),
    serviceLastDone: asString(getValue(payload, "service_last_done", "serviceLastDone")) || null,
    serviceLastOdo: asNumber(getValue(payload, "service_last_odo", "serviceLastOdo")),
    cofLastDone: asString(getValue(payload, "cof_last_done", "cofLastDone")) || null,
    cofRequired: asString(getValue(payload, "cof_required", "cofRequired")) || null,
    cofNumber: asString(getValue(payload, "cof_number", "cofNumber")) || null,
    cofAmount: asNumber(getValue(payload, "Cof_amount", "cofAmount")),
    licenceRegisterNumber:
      asString(getValue(payload, "lic_register_number", "licenceRegisterNumber")) || null,
    ifmsVehicleRegisterNumber:
      asString(getValue(payload, "ifms_vehicle_register_number", "ifmsVehicleRegisterNumber")) ||
      null,
    natisModelNumber: asString(getValue(payload, "natis_model_number", "natisModelNumber")) || null,
    dateCreated: asString(getValue(payload, "date_created", "dateCreated")) || null,
    dateUpdated: asString(getValue(payload, "date_updated", "dateUpdated")) || null,
    capturedDate: asString(getValue(payload, "captured_date", "capturedDate")) || null,
  };
}

export async function getVehicleForEdit(vmfCode: number) {
  const response = await requestApi(`api/vehicles/${encodeURIComponent(vmfCode)}`);
  return mapVehicle(await readJson(response));
}

export async function updateVehicleAgainstApi(vmfCode: number, request: VehicleUpdateRequest) {
  const response = await requestApi(`api/vehicles/${encodeURIComponent(vmfCode)}`, {
    method: "PUT",
    body: JSON.stringify(request),
  });

  await readJson(response);
  return { ok: true as const };
}

export async function updateVehicleInvoiceAgainstApi(
  vmfCode: number,
  invoiceNumber: string | null,
) {
  const response = await requestApi(`api/vehicles/${encodeURIComponent(vmfCode)}/invoice`, {
    method: "PATCH",
    body: JSON.stringify({ invoice_number: invoiceNumber }),
  });

  await readJson(response);
  return { ok: true as const };
}
