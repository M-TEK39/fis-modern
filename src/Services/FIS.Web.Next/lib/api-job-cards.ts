import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type JobCardRecord = {
  jobCardId: number;
  vmfCode: number;
  ggNumber: string | null;
  registrationNumber: string | null;
  extraCode: number;
  extraDescription: string | null;
  statusCode: number;
  statusText: string | null;
  priority: string | null;
  assignedTo: number | null;
  assignedToName: string | null;
  assignedDate: string | null;
  jcsComment: string | null;
  damages: string | null;
  comments: string | null;
  authorizer: number | null;
  authorizerName: string | null;
  reviewed: string | null;
  capturedByUserCode: number | null;
  authorizedByUserCode: number | null;
  dateCreated: string | null;
  dateUpdated: string | null;
  labourCost: number | null;
  partsCost: number | null;
  otherCost: number | null;
  totalCost: number | null;
  invoiceNumber: string | null;
  invoiceDate: string | null;
  serviceProvider: string | null;
};

export type JobCardCreateInput = {
  vmf_code: number;
  extra_code: number;
  jcs_comment?: string | null;
  damages?: string | null;
  priority?: string | null;
};

export type JobCardUpdateInput = {
  jcs_comment?: string | null;
  damages?: string | null;
  comments?: string | null;
  assigned_to?: number | null;
  assigned_date?: string | null;
  priority?: string | null;
};

export type JobCardCostInput = {
  labour_cost?: number | null;
  parts_cost?: number | null;
  other_cost?: number | null;
  invoice_number?: string | null;
  invoice_date?: string | null;
  service_provider?: string | null;
};

export type RepairCostReport = {
  filtersApplied: {
    vmfCode: number | null;
    siteCode: number | null;
    fromDate: string | null;
    toDate: string | null;
  };
  totalRecords: number;
  grandTotal: number;
  totalLabour: number;
  totalParts: number;
  totalOther: number;
  lineItems: RepairCostLine[];
};

export type RepairCostLine = {
  jobCardId: number;
  vmfCode: number;
  fleetNumber: string | null;
  registration: string | null;
  damages: string | null;
  serviceProvider: string | null;
  invoiceNumber: string | null;
  invoiceDate: string | null;
  labourCost: number;
  partsCost: number;
  otherCost: number;
  totalCost: number;
  closedDate: string | null;
};

export type JobCardApiErrorReason =
  "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class JobCardApiError extends Error {
  constructor(
    public readonly reason: JobCardApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "JobCardApiError";
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

function getCollection(value: unknown) {
  if (Array.isArray(value)) return value;
  if (isRecord(value)) {
    const collection = getValue(value, "data", "items", "results");
    return Array.isArray(collection) ? collection : [];
  }
  return [];
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader)
    throw new JobCardApiError("unauthorized", "No FIS access cookie is available.");

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
      throw new JobCardApiError("unauthorized", "The FIS access cookie was rejected.");
    }
    if (response.status === 404)
      throw new JobCardApiError("not-found", "The job card was not found.");
    if (!response.ok)
      throw new JobCardApiError("invalid-response", `FIS API returned HTTP ${response.status}.`);
    return response;
  } catch (error) {
    if (error instanceof JobCardApiError) throw error;
    throw new JobCardApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new JobCardApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapJobCard(value: unknown): JobCardRecord | null {
  if (!isRecord(value)) return null;
  const jobCardId = asNumber(getValue(value, "job_card_id", "jobCardId"));
  const vmfCode = asNumber(getValue(value, "vmf_code", "vmfCode"));
  if (jobCardId === null || vmfCode === null) return null;

  return {
    jobCardId,
    vmfCode,
    ggNumber: asString(getValue(value, "gg_number", "ggNumber")),
    registrationNumber: asString(getValue(value, "registration_number", "registrationNumber")),
    extraCode: asNumber(getValue(value, "extra_code", "extraCode")) ?? 0,
    extraDescription: asString(getValue(value, "extra_description", "extraDescription")),
    statusCode: asNumber(getValue(value, "status_code", "statusCode")) ?? 0,
    statusText: asString(getValue(value, "status_text", "statusText")),
    priority: asString(getValue(value, "priority", "Priority")),
    assignedTo: asNumber(getValue(value, "assigned_to", "assignedTo")),
    assignedToName: asString(getValue(value, "assigned_to_name", "assignedToName")),
    assignedDate: asString(getValue(value, "assigned_date", "assignedDate")),
    jcsComment: asString(getValue(value, "jcs_comment", "jcsComment")),
    damages: asString(getValue(value, "damages", "Damages")),
    comments: asString(getValue(value, "comments", "Comments")),
    authorizer: asNumber(getValue(value, "authorizer", "Authorizer")),
    authorizerName: asString(getValue(value, "authorizer_name", "authorizerName")),
    reviewed: asString(getValue(value, "reviewed", "Reviewed")),
    capturedByUserCode: asNumber(getValue(value, "captured_by_user_code", "capturedByUserCode")),
    authorizedByUserCode: asNumber(
      getValue(value, "authorized_by_user_code", "authorizedByUserCode"),
    ),
    dateCreated: asString(getValue(value, "date_created", "dateCreated")),
    dateUpdated: asString(getValue(value, "date_updated", "dateUpdated")),
    labourCost: asNumber(getValue(value, "labour_cost", "labourCost")),
    partsCost: asNumber(getValue(value, "parts_cost", "partsCost")),
    otherCost: asNumber(getValue(value, "other_cost", "otherCost")),
    totalCost: asNumber(getValue(value, "total_cost", "totalCost")),
    invoiceNumber: asString(getValue(value, "invoice_number", "invoiceNumber")),
    invoiceDate: asString(getValue(value, "invoice_date", "invoiceDate")),
    serviceProvider: asString(getValue(value, "service_provider", "serviceProvider")),
  };
}

async function readJobCard(response: Response) {
  const record = mapJobCard(await readJson(response));
  if (!record)
    throw new JobCardApiError("invalid-response", "The FIS API returned an invalid job card.");
  return record;
}

async function mutate(path: string, method: string, body?: unknown) {
  return requestApi(path, {
    method,
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
}

export async function getJobCards() {
  const payload = await readJson(await requestApi("api/jobcards"));
  return getCollection(payload)
    .map(mapJobCard)
    .filter((item): item is JobCardRecord => item !== null);
}

export async function getJobCard(jobCardId: number) {
  return readJobCard(await requestApi(`api/jobcards/${encodeURIComponent(jobCardId)}`));
}

export async function getJobCardsByVehicle(vehicleNumber: string) {
  const payload = await readJson(
    await requestApi(`api/jobcards/by-gg/${encodeURIComponent(vehicleNumber.trim())}`),
  );
  return getCollection(payload)
    .map(mapJobCard)
    .filter((item): item is JobCardRecord => item !== null);
}

export async function getPriorityUnassignedJobCards() {
  const payload = await readJson(await requestApi("api/jobcards/priority/unassigned"));
  return getCollection(payload)
    .map(mapJobCard)
    .filter((item): item is JobCardRecord => item !== null);
}

export async function createJobCard(input: JobCardCreateInput) {
  return readJobCard(await mutate("api/jobcards", "POST", input));
}

export async function updateJobCard(jobCardId: number, input: JobCardUpdateInput) {
  return readJobCard(await mutate(`api/jobcards/${encodeURIComponent(jobCardId)}`, "PUT", input));
}

export async function authorizeJobCard(jobCardId: number, comment: string | null) {
  return readJobCard(
    await mutate(`api/jobcards/${encodeURIComponent(jobCardId)}/authorize`, "POST", { comment }),
  );
}

export async function declineJobCard(jobCardId: number, declineReason: string) {
  return readJobCard(
    await mutate(`api/jobcards/${encodeURIComponent(jobCardId)}/decline`, "POST", {
      decline_reason: declineReason,
    }),
  );
}

export async function cancelJobCard(jobCardId: number, cancelReason: string | null) {
  return readJobCard(
    await mutate(`api/jobcards/${encodeURIComponent(jobCardId)}/cancel`, "POST", {
      cancel_reason: cancelReason,
    }),
  );
}

export async function closeJobCard(
  jobCardId: number,
  input: JobCardCostInput & { close_notes?: string | null },
) {
  return readJobCard(
    await mutate(`api/jobcards/${encodeURIComponent(jobCardId)}/close`, "POST", input),
  );
}

export async function updateJobCardCosts(jobCardId: number, input: JobCardCostInput) {
  return readJobCard(
    await mutate(`api/jobcards/${encodeURIComponent(jobCardId)}/costs`, "PATCH", input),
  );
}

export async function deleteJobCard(jobCardId: number) {
  await mutate(`api/jobcards/${encodeURIComponent(jobCardId)}`, "DELETE");
}

function mapRepairCostLine(value: unknown): RepairCostLine | null {
  if (!isRecord(value)) return null;
  const jobCardId = asNumber(getValue(value, "job_card_id", "jobCardId"));
  const vmfCode = asNumber(getValue(value, "vmf_code", "vmfCode"));
  if (jobCardId === null || vmfCode === null) return null;
  return {
    jobCardId,
    vmfCode,
    fleetNumber: asString(getValue(value, "fleet_number", "fleetNumber", "gg_number", "ggNumber")),
    registration: asString(
      getValue(value, "registration", "registration_number", "registrationNumber"),
    ),
    damages: asString(getValue(value, "damages", "Damages")),
    serviceProvider: asString(getValue(value, "service_provider", "serviceProvider")),
    invoiceNumber: asString(getValue(value, "invoice_number", "invoiceNumber")),
    invoiceDate: asString(getValue(value, "invoice_date", "invoiceDate")),
    labourCost: asNumber(getValue(value, "labour_cost", "labourCost")) ?? 0,
    partsCost: asNumber(getValue(value, "parts_cost", "partsCost")) ?? 0,
    otherCost: asNumber(getValue(value, "other_cost", "otherCost")) ?? 0,
    totalCost: asNumber(getValue(value, "total_cost", "totalCost")) ?? 0,
    closedDate: asString(getValue(value, "closed_date", "closedDate", "date_closed", "dateClosed")),
  };
}

export async function getRepairCostReport(filters: {
  vmfCode?: number;
  siteCode?: number;
  fromDate?: string;
  toDate?: string;
}): Promise<RepairCostReport> {
  const params = new URLSearchParams();
  if (filters.vmfCode) params.set("vmfCode", String(filters.vmfCode));
  if (filters.siteCode) params.set("siteCode", String(filters.siteCode));
  if (filters.fromDate) params.set("fromDate", filters.fromDate);
  if (filters.toDate) params.set("toDate", filters.toDate);
  const payload = await readJson(
    await requestApi(`api/jobcards/repair-cost-report?${params.toString()}`),
  );
  if (!isRecord(payload))
    throw new JobCardApiError("invalid-response", "The repair cost report response was invalid.");
  return {
    filtersApplied: {
      vmfCode: asNumber(getValue(payload, "vmf_code", "vmfCode")),
      siteCode: asNumber(getValue(payload, "site_code", "siteCode")),
      fromDate: asString(getValue(payload, "from_date", "fromDate")),
      toDate: asString(getValue(payload, "to_date", "toDate")),
    },
    totalRecords: asNumber(getValue(payload, "total_records", "totalRecords")) ?? 0,
    grandTotal: asNumber(getValue(payload, "grand_total", "grandTotal")) ?? 0,
    totalLabour: asNumber(getValue(payload, "total_labour", "totalLabour")) ?? 0,
    totalParts: asNumber(getValue(payload, "total_parts", "totalParts")) ?? 0,
    totalOther: asNumber(getValue(payload, "total_other", "totalOther")) ?? 0,
    lineItems: getCollection(getValue(payload, "line_items", "lineItems"))
      .map(mapRepairCostLine)
      .filter((item): item is RepairCostLine => item !== null),
  };
}
