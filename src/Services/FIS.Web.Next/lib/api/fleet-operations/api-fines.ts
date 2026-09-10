import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type FineSearchType = "GG" | "GP";

export type FineRecord = {
  fineCode: number;
  vmfCode: number | null;
  offenceDate: string | null;
  offenceReference: string | null;
  offenceIssuer: string | null;
  fineAmount: number | null;
  appearDate: string | null;
  receiveGgDate: string | null;
  notifyDeptDate: string | null;
  siteCode: number | null;
  offenceName: string | null;
  finePayDate: string | null;
  withdrawDate: string | null;
  payDueDate: string | null;
  issuerNotifyDate: string | null;
  deptPersonName: string | null;
  deptPersonId: string | null;
  documentType: string | null;
  trafficDeptCode: number | null;
};

export type FineRequest = {
  vmf_code: number;
  Offence_date: string;
  Offence_reference: string | null;
  Offence_issuer: string | null;
  Fine_amount: number | null;
  Appear_date: string | null;
  Receive_gg_date: string | null;
  Notify_dept_date: string | null;
  Site_code: number | null;
  Offence_name: string | null;
  Fine_pay_date: string | null;
  Withdraw_date: string | null;
  Pay_due_date: string | null;
  Issuer_notify_date: string | null;
  Dept_person_name: string | null;
  Dept_person_id: string | null;
  Document_type: string | null;
  Traffic_dept_code: number | null;
};

export type FineVehicleOption = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  matchedRegistration: string | null;
  isHistoricalMatch: boolean;
};

export type TrafficDeptRecord = {
  trafficDeptCode: number;
  name: string | null;
  responsiblePerson: string | null;
  postalAddress1: string | null;
  postalAddress2: string | null;
  postalCode: string | null;
  telephone: string | null;
  fax: string | null;
  cell: string | null;
  email: string | null;
};

export type TrafficDeptRequest = {
  Traf_name: string;
  Traf_res_person: string | null;
  Traf_post_address1: string | null;
  Traf_post_address2: string | null;
  Traf_post_code: string | null;
  Traf_telephone: string | null;
  Traf_fax: string | null;
  Traf_cell: string | null;
  Traf_email: string | null;
};

export type FineSite = {
  siteCode: number;
  departmentNumber: string | null;
  description: string | null;
};

export type FineReportMode =
  | "one-vehicle"
  | "appear-date"
  | "fine-detail"
  | "reissue-submission"
  | "traffic-dept-detail"
  | "dept-site-period"
  | "vehicle-period"
  | "metro-period"
  | "all";

export type FineReport = {
  title: string;
  legacyTarget: string | null;
  isApproximate: boolean;
  approximationReason: string | null;
  columns: Array<{ key: string; header: string }>;
  rows: Array<Record<string, string | null>>;
  totalCount: number;
};

export type FineApiErrorReason = "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class FineApiError extends Error {
  constructor(
    public readonly reason: FineApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "FineApiError";
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

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new FineApiError("unauthorized", "No FIS access cookie is available.");
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
      throw new FineApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (response.status === 404) {
      throw new FineApiError("not-found", "The requested fine record was not found.");
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

      throw new FineApiError(response.status >= 500 ? "unavailable" : "invalid-response", message);
    }

    return response;
  } catch (error) {
    if (error instanceof FineApiError) {
      throw error;
    }

    throw new FineApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new FineApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapFine(value: unknown): FineRecord | null {
  if (!isRecord(value)) {
    return null;
  }

  const fineCode = asNumber(getValue(value, "Fine_code", "fine_code", "fineCode"));
  if (fineCode === null) {
    return null;
  }

  return {
    fineCode,
    vmfCode: asNumber(getValue(value, "vmf_code", "vmfCode")),
    offenceDate: asString(getValue(value, "Offence_date", "offence_date", "offenceDate")),
    offenceReference: asString(
      getValue(value, "Offence_reference", "offence_reference", "offenceReference"),
    ),
    offenceIssuer: asString(getValue(value, "Offence_issuer", "offence_issuer", "offenceIssuer")),
    fineAmount: asNumber(getValue(value, "Fine_amount", "fine_amount", "fineAmount")),
    appearDate: asString(getValue(value, "Appear_date", "appear_date", "appearDate")),
    receiveGgDate: asString(getValue(value, "Receive_gg_date", "receive_gg_date", "receiveGgDate")),
    notifyDeptDate: asString(
      getValue(value, "Notify_dept_date", "notify_dept_date", "notifyDeptDate"),
    ),
    siteCode: asNumber(getValue(value, "Site_code", "site_code", "siteCode")),
    offenceName: asString(getValue(value, "Offence_name", "offence_name", "offenceName")),
    finePayDate: asString(getValue(value, "Fine_pay_date", "fine_pay_date", "finePayDate")),
    withdrawDate: asString(getValue(value, "Withdraw_date", "withdraw_date", "withdrawDate")),
    payDueDate: asString(getValue(value, "Pay_due_date", "pay_due_date", "payDueDate")),
    issuerNotifyDate: asString(
      getValue(value, "Issuer_notify_date", "issuer_notify_date", "issuerNotifyDate"),
    ),
    deptPersonName: asString(
      getValue(value, "Dept_person_name", "dept_person_name", "deptPersonName"),
    ),
    deptPersonId: asString(getValue(value, "Dept_person_id", "dept_person_id", "deptPersonId")),
    documentType: asString(getValue(value, "Document_type", "document_type", "documentType")),
    trafficDeptCode: asNumber(
      getValue(value, "Traffic_dept_code", "traffic_dept_code", "trafficDeptCode"),
    ),
  };
}

function mapVehicle(value: unknown): FineVehicleOption | null {
  if (!isRecord(value)) {
    return null;
  }

  const vmfCode = asNumber(getValue(value, "vmf_code", "vmfCode"));
  if (vmfCode === null) {
    return null;
  }

  return {
    vmfCode,
    fleetNumber: asString(getValue(value, "fleet_number", "fleetNumber")),
    registrationNumber: asString(
      getValue(
        value,
        "registration_number",
        "registrationNumber",
        "current_registration",
        "currentRegistration",
      ),
    ),
    matchedRegistration: asString(getValue(value, "matched_registration", "matchedRegistration")),
    isHistoricalMatch: getValue(value, "is_historical_match", "isHistoricalMatch") === true,
  };
}

function mapTrafficDept(value: unknown): TrafficDeptRecord | null {
  if (!isRecord(value)) {
    return null;
  }

  const trafficDeptCode = asNumber(
    getValue(value, "Traffic_dept_code", "traffic_dept_code", "trafficDeptCode"),
  );
  if (trafficDeptCode === null) {
    return null;
  }

  return {
    trafficDeptCode,
    name: asString(getValue(value, "Traf_name", "traf_name", "name")),
    responsiblePerson: asString(
      getValue(value, "Traf_res_person", "traf_res_person", "responsiblePerson"),
    ),
    postalAddress1: asString(
      getValue(value, "Traf_post_address1", "traf_post_address1", "postalAddress1"),
    ),
    postalAddress2: asString(
      getValue(value, "Traf_post_address2", "traf_post_address2", "postalAddress2"),
    ),
    postalCode: asString(getValue(value, "Traf_post_code", "traf_post_code", "postalCode")),
    telephone: asString(getValue(value, "Traf_telephone", "traf_telephone", "telephone")),
    fax: asString(getValue(value, "Traf_fax", "traf_fax", "fax")),
    cell: asString(getValue(value, "Traf_cell", "traf_cell", "cell")),
    email: asString(getValue(value, "Traf_email", "traf_email", "email")),
  };
}

function mapFineReport(value: unknown): FineReport | null {
  if (!isRecord(value)) {
    return null;
  }

  const rawColumns = getValue(value, "Columns", "columns");
  const rawRows = getValue(value, "Rows", "rows");
  if (!Array.isArray(rawColumns) || !Array.isArray(rawRows)) {
    return null;
  }

  const columns = mapPresent(rawColumns, (column) => {
    if (!isRecord(column)) return null;
    const key = asString(getValue(column, "Key", "key"));
    const header = asString(getValue(column, "Header", "header"));
    return key && header ? { key, header } : null;
  });

  const rows = mapPresent(rawRows, (row) => {
    if (!isRecord(row)) return null;
    const mapped: Record<string, string | null> = {};
    for (const [key, value] of Object.entries(row)) {
      mapped[key] = asString(value);
    }
    return mapped;
  });

  const title = asString(getValue(value, "Title", "title"));
  if (!title || columns.length === 0) {
    return null;
  }

  return {
    title,
    legacyTarget: asString(getValue(value, "LegacyTarget", "legacyTarget")),
    isApproximate: getValue(value, "IsApproximate", "isApproximate") === true,
    approximationReason: asString(getValue(value, "ApproximationReason", "approximationReason")),
    columns,
    rows,
    totalCount: asNumber(getValue(value, "TotalCount", "totalCount")) ?? rows.length,
  };
}

function mapSite(value: unknown): FineSite | null {
  if (!isRecord(value)) {
    return null;
  }

  const siteCode = asNumber(getValue(value, "SiteCode", "siteCode", "Site_code", "site_code"));
  if (siteCode === null) {
    return null;
  }

  return {
    siteCode,
    departmentNumber: asString(
      getValue(value, "DepartmentNumber", "departmentNumber", "Department_number"),
    ),
    description: asString(getValue(value, "Description", "description")),
  };
}

export async function getFines() {
  const response = await requestApi("api/fine");
  return getCollection(await readJson(response))
    .map(mapFine)
    .filter((fine): fine is FineRecord => fine !== null)
    .toSorted((left, right) => {
      const leftDate = left.offenceDate ?? left.receiveGgDate ?? "";
      const rightDate = right.offenceDate ?? right.receiveGgDate ?? "";
      return rightDate.localeCompare(leftDate) || right.fineCode - left.fineCode;
    });
}

export async function getFine(fineCode: number) {
  const response = await requestApi(`api/fine/${encodeURIComponent(fineCode)}`);
  const fine = mapFine(await readJson(response));
  if (!fine) {
    throw new FineApiError("invalid-response", "The FIS API returned an invalid fine record.");
  }

  return fine;
}

export async function getFineVehicle(vmfCode: number) {
  const response = await requestApi(`api/vehicles/${encodeURIComponent(vmfCode)}`);
  const vehicle = mapVehicle(await readJson(response));
  if (!vehicle) {
    throw new FineApiError("invalid-response", "The FIS API returned an invalid vehicle record.");
  }

  return vehicle;
}

export async function searchFineVehicles(searchType: FineSearchType, searchTerm: string) {
  const normalized = searchTerm.trim();
  if (!normalized) {
    return [];
  }

  const path =
    searchType === "GP"
      ? `api/registration/search?q=${encodeURIComponent(normalized)}`
      : `api/vehicles/search?searchTerm=${encodeURIComponent(normalized)}`;
  const vehicles = getCollection(await readJson(await requestApi(path)))
    .map(mapVehicle)
    .filter((vehicle): vehicle is FineVehicleOption => vehicle !== null);

  const normalizedLower = normalized.toLocaleLowerCase();
  return vehicles
    .filter((vehicle) => {
      const value =
        searchType === "GP"
          ? (vehicle.matchedRegistration ?? vehicle.registrationNumber)
          : vehicle.fleetNumber;
      return value?.trim().toLocaleLowerCase() === normalizedLower;
    })
    .toSorted((left, right) => {
      const leftLabel = left.fleetNumber ?? left.registrationNumber ?? String(left.vmfCode);
      const rightLabel = right.fleetNumber ?? right.registrationNumber ?? String(right.vmfCode);
      return leftLabel.localeCompare(rightLabel);
    });
}

export async function getFineSites() {
  const response = await requestApi("api/site");
  return getCollection(await readJson(response))
    .map(mapSite)
    .filter((site): site is FineSite => site !== null)
    .toSorted((left, right) => {
      const leftLabel = left.departmentNumber ?? left.description ?? String(left.siteCode);
      const rightLabel = right.departmentNumber ?? right.description ?? String(right.siteCode);
      return leftLabel.localeCompare(rightLabel);
    });
}

export async function getTrafficDepts() {
  const response = await requestApi("api/TrafficDept");
  return getCollection(await readJson(response))
    .map(mapTrafficDept)
    .filter((dept): dept is TrafficDeptRecord => dept !== null)
    .toSorted((left, right) => (left.name ?? "").localeCompare(right.name ?? ""));
}

export async function getFineReport(
  mode: FineReportMode,
  filters: Record<string, string | number | undefined> = {},
) {
  const params = new URLSearchParams({ mode });
  for (const [key, value] of Object.entries(filters)) {
    if (value !== undefined && String(value).trim() !== "") {
      params.set(key, String(value));
    }
  }

  const response = await requestApi(`api/report/dynamic/fines?${params.toString()}`);
  const report = mapFineReport(await readJson(response));
  if (!report) {
    throw new FineApiError("invalid-response", "The FIS API returned an invalid fines report.");
  }

  return report;
}

export async function getTrafficDept(code: number) {
  const response = await requestApi(`api/TrafficDept/${encodeURIComponent(code)}`);
  const dept = mapTrafficDept(await readJson(response));
  if (!dept) {
    throw new FineApiError(
      "invalid-response",
      "The FIS API returned an invalid traffic department record.",
    );
  }

  return dept;
}

export async function createFineAgainstApi(request: FineRequest) {
  const response = await requestApi("api/fine", { method: "POST", body: JSON.stringify(request) });
  return mapFine(await readJson(response));
}

export async function updateFineAgainstApi(fineCode: number, request: FineRequest) {
  const response = await requestApi(`api/fine/${encodeURIComponent(fineCode)}`, {
    method: "PUT",
    body: JSON.stringify({ ...request, Fine_code: fineCode }),
  });
  return mapFine(await readJson(response));
}

export async function deleteFineAgainstApi(fineCode: number) {
  await requestApi(`api/fine/${encodeURIComponent(fineCode)}`, { method: "DELETE" });
}

export async function createTrafficDeptAgainstApi(request: TrafficDeptRequest) {
  const response = await requestApi("api/TrafficDept", {
    method: "POST",
    body: JSON.stringify(request),
  });
  return mapTrafficDept(await readJson(response));
}

export async function updateTrafficDeptAgainstApi(code: number, request: TrafficDeptRequest) {
  const response = await requestApi(`api/TrafficDept/${encodeURIComponent(code)}`, {
    method: "PUT",
    body: JSON.stringify({ ...request, Traffic_dept_code: code }),
  });
  return mapTrafficDept(await readJson(response));
}

export async function deleteTrafficDeptAgainstApi(code: number) {
  await requestApi(`api/TrafficDept/${encodeURIComponent(code)}`, { method: "DELETE" });
}
