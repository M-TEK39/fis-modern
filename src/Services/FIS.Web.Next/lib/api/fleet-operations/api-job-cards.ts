import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";
import type { VehicleOption } from "@/lib/api/vehicles/api-vehicles";

const API_TIMEOUT_MS = 8_000;
export const DEFAULT_JOB_CARD_PAGE_SIZE = 24;
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

export type JobCardPage = {
  items: JobCardRecord[];
  page: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
};

export type JobCardAuthorizerGgStats = {
  ggNumber: string;
  jobcards: number | null;
  pending: number | null;
  awaitingAuthorisation: number | null;
  authorised: number | null;
  inProgress: number | null;
  canceled: number | null;
  failed: number | null;
  completed: number | null;
};

export type JobCardAuthorizerGgStatsPage = {
  items: JobCardAuthorizerGgStats[];
  page: number;
  pageSize: number;
  totalRecords: number;
  totalPages: number;
};

export type JobCardCaptureVehicleSummary = {
  vmfCode: number | null;
  ggNumber: string;
  registrationNumber: string | null;
  classDescription: string | null;
  modelDescription: string | null;
  odoReading: string | null;
  vinNumber: string | null;
  engineNumber: string | null;
  yearModel: string | null;
  purchasedFrom: string | null;
  hireType: string | null;
  hiredFrom: string | null;
  location: string | null;
};

export type JobCardCaptureExtra = {
  extraCode: number;
  description: string | null;
};

export type JobCardCaptureContext = {
  summary: { overlay: true; item: JobCardCaptureVehicleSummary | null } | { overlay: false };
  extras: { overlay: true; items: JobCardCaptureExtra[] } | { overlay: false };
  fittedExtras: { overlay: true; items: string[] } | { overlay: false };
  jobcardsOnStatus: { overlay: true; items: string[] } | { overlay: false };
};

export type JobCardAuthorizerDetails = {
  jobCardId: number | null;
  jcNumber: string | null;
  ggNumber: string | null;
  extraDescription: string | null;
  initialCapturedDate: string | null;
  initialCapturer: string | null;
  barcode: string | null;
  capturedDate: string | null;
  jobCardsCapturer: string | null;
  handoverName: string | null;
  handoverDate: string | null;
  damages: string | null;
  comments: string | null;
  statusDescription: string | null;
  priority: string | null;
  authorizer: string | null;
  authorizedDate: string | null;
  authorizerComments: string | null;
};

export type JobCardAuthorizerStatus = {
  statusCode: number;
  description: string | null;
};

export type JobCardCapturerDetails = {
  jobCardId: number | null;
  jcNumber: string | null;
  ggNumber: string | null;
  extraDescription: string | null;
  barcode: string | null;
  initialCapturer: string | null;
  initialCapturedDate: string | null;
  jobCardsCapturer: string | null;
  capturedDate: string | null;
  handoverName: string | null;
  handoverDate: string | null;
  damages: string | null;
  comments: string | null;
  statusDescription: string | null;
  jobcardComment: string | null;
  authorizer: string | null;
  authorizerDate: string | null;
  authorizerComments: string | null;
};

export type JobCardCapturerStatus = {
  statusCode: number;
  description: string | null;
};

export type JobCardCloseDetails = {
  jobCardId: number | null;
  ggNumber: string | null;
  jcNumber: string | null;
  extraDescription: string | null;
  jobCardsCapturer: string | null;
  capturedDate: string | null;
  handoverName: string | null;
  handoverDate: string | null;
  authorizer: string | null;
  authorizedDate: string | null;
  authorizerComments: string | null;
  statusDescription: string | null;
  dateClosed: string | null;
  barcode: string | null;
  jobcardComment: string | null;
  damages: string | null;
  comments: string | null;
};

export type JobCardPrintSummary = {
  jobcardNumber: string;
  ggNumber: string;
  registrationNumber: string | null;
  jobcardDescription: string | null;
};

export type JobCardPrintSnapshot = {
  ggNumber: string | null;
  registrationNumber: string | null;
  dateDelivered: string | null;
  odoReading: string | null;
  vinNumber: string | null;
  engineNumber: string | null;
  modelDescription: string | null;
  yearModel: string | null;
  classDescription: string | null;
  hireType: string | null;
  hiredFrom: string | null;
  location: string | null;
  capturedDate: string | null;
  receivedBy: string | null;
  status: string | null;
  statusDate: string | null;
  purchasedFrom: string | null;
  purchasedDate: string | null;
  jobcardNumber: string | null;
  jobDescription: string | null;
  jobcardStatus: string | null;
  capturedBy: string | null;
  jcsDate: string | null;
  assignedTo: string | null;
  assignedDate: string | null;
};

export type JobCardPageOptions = {
  page?: number;
  pageSize?: number;
  search?: string;
  searchType?: "GG" | "GP";
  statusCodes?: readonly number[];
  list?: "close" | "cancel";
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

export type RepairCostReportPage = RepairCostReport & {
  page: number;
  pageSize: number;
  totalPages: number;
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

async function readJobCardPage(response: Response): Promise<JobCardPage> {
  const payload = await readJson(response);
  if (!isRecord(payload) || !Array.isArray(payload.items))
    throw new JobCardApiError("invalid-response", "The FIS API returned an invalid job card page.");

  const metadata = readPageMetadata(payload, ["totalRecords", "total_records"]);
  if (!metadata)
    throw new JobCardApiError(
      "invalid-response",
      "The FIS API returned incomplete job card pagination metadata.",
    );

  return {
    items: payload.items.map(mapJobCard).filter((item): item is JobCardRecord => item !== null),
    ...metadata,
  };
}

function readPageMetadata(payload: JsonRecord, totalKeys: readonly string[]) {
  const page = asNumber(getValue(payload, "page"));
  const pageSize = asNumber(getValue(payload, "pageSize", "page_size"));
  const totalRecords = asNumber(getValue(payload, ...totalKeys));
  const totalPages = asNumber(getValue(payload, "totalPages", "total_pages"));
  if (
    page === null ||
    pageSize === null ||
    totalRecords === null ||
    totalPages === null ||
    !Number.isInteger(page) ||
    !Number.isInteger(pageSize) ||
    !Number.isInteger(totalRecords) ||
    !Number.isInteger(totalPages) ||
    page < 1 ||
    pageSize < 1 ||
    totalRecords < 0 ||
    totalPages < 1
  ) {
    return null;
  }
  return { page, pageSize, totalRecords, totalPages };
}

function normalizePage(value: number | undefined) {
  return Number.isInteger(value) && (value ?? 0) > 0 ? (value ?? 1) : 1;
}

function normalizePageSize(value: number | undefined) {
  const pageSize =
    Number.isInteger(value) && (value ?? 0) > 0
      ? (value ?? DEFAULT_JOB_CARD_PAGE_SIZE)
      : DEFAULT_JOB_CARD_PAGE_SIZE;
  return Math.min(100, pageSize);
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

export async function getJobCardCaptureVehicles(): Promise<VehicleOption[]> {
  const payload = await readJson(await requestApi("api/jobcards/vehicles-available"));
  return getCollection(payload).flatMap((item) => {
    if (!isRecord(item)) {
      return [];
    }

    const vmfCode = asNumber(getValue(item, "vmf_code", "vmfCode"));
    if (vmfCode === null) {
      return [];
    }

    return [
      {
        vmfCode,
        fleetNumber: asString(getValue(item, "fleet_number", "fleetNumber")),
        registrationNumber: asString(getValue(item, "registration_number", "registrationNumber")),
            modelCode: asNumber(getValue(item, "model_code", "modelCode")),
          },
        ];
      });
}

function readOverlayFlag(value: unknown): boolean | null {
  if (!isRecord(value) || typeof value.overlay !== "boolean") {
    return null;
  }
  return value.overlay;
}

function readStringList(value: unknown): string[] {
  if (!isRecord(value) || !Array.isArray(value.items)) {
    return [];
  }
  return value.items.flatMap((item) => {
    const text = asString(item);
    return text ? [text] : [];
  });
}

export async function getJobCardCaptureContext(
  ggNumber: string,
): Promise<JobCardCaptureContext> {
  const params = new URLSearchParams({ ggNumber });
  const payload = await readJson(
    await requestApi(`api/jobcards/capture-context?${params.toString()}`),
  );
  if (!isRecord(payload)) {
    throw new JobCardApiError(
      "invalid-response",
      "The FIS API returned an invalid job-card capture context.",
    );
  }

  const summaryOverlay = readOverlayFlag(payload.summary);
  const extrasOverlay = readOverlayFlag(payload.extras);
  const fittedOverlay = readOverlayFlag(payload.fittedExtras);
  const statusOverlay = readOverlayFlag(payload.jobcardsOnStatus);
  if (
    summaryOverlay === null ||
    extrasOverlay === null ||
    fittedOverlay === null ||
    statusOverlay === null
  ) {
    throw new JobCardApiError(
      "invalid-response",
      "The FIS API returned an invalid job-card capture context.",
    );
  }

  let summary: JobCardCaptureContext["summary"] = { overlay: false };
  if (summaryOverlay) {
    const summaryPayload = isRecord(payload.summary) ? payload.summary : null;
    const itemPayload = summaryPayload ? getValue(summaryPayload, "item") : null;
    summary = {
      overlay: true,
      item: isRecord(itemPayload)
        ? {
            vmfCode: asNumber(getValue(itemPayload, "vmf_code", "vmfCode")),
            ggNumber:
              asString(getValue(itemPayload, "ggNumber", "GGNumber", "fleet_number")) ?? ggNumber,
            registrationNumber: asString(
              getValue(itemPayload, "registrationNumber", "RegistrationNumber"),
            ),
            classDescription: asString(
              getValue(itemPayload, "classDescription", "ClassDescription"),
            ),
            modelDescription: asString(
              getValue(itemPayload, "modelDescription", "ModelDescription"),
            ),
            odoReading: asString(getValue(itemPayload, "odoReading", "OdoReading")),
            vinNumber: asString(getValue(itemPayload, "vinNumber", "VINNumber")),
            engineNumber: asString(getValue(itemPayload, "engineNumber", "EngineNumber")),
            yearModel: asString(getValue(itemPayload, "yearModel", "YearModel")),
            purchasedFrom: asString(getValue(itemPayload, "purchasedFrom", "PurchasedFrom")),
            hireType: asString(getValue(itemPayload, "hireType", "HireType")),
            hiredFrom: asString(getValue(itemPayload, "hiredFrom", "HiredFrom")),
            location: asString(getValue(itemPayload, "location", "Location")),
          }
        : null,
    };
  }

  let extras: JobCardCaptureContext["extras"] = { overlay: false };
  if (extrasOverlay) {
    const extrasPayload = isRecord(payload.extras) ? payload.extras : null;
    extras = {
      overlay: true,
      items: Array.isArray(extrasPayload?.items)
        ? extrasPayload.items.flatMap((item) => {
            if (!isRecord(item)) {
              return [];
            }
            const extraCode = asNumber(getValue(item, "extraCode", "extra_code"));
            if (extraCode === null) {
              return [];
            }
            return [
              {
                extraCode,
                description: asString(getValue(item, "description", "extra_description")),
              },
            ];
          })
        : [],
    };
  }

  return {
    summary,
    extras,
    fittedExtras: fittedOverlay
      ? { overlay: true, items: readStringList(payload.fittedExtras) }
      : { overlay: false },
    jobcardsOnStatus: statusOverlay
      ? { overlay: true, items: readStringList(payload.jobcardsOnStatus) }
      : { overlay: false },
  };
}

export async function getJobCardsPage(options: JobCardPageOptions = {}): Promise<JobCardPage> {
  const requestedPage = normalizePage(options.page);
  const pageSize = normalizePageSize(options.pageSize);
  const searchType = options.searchType === "GP" ? "GP" : "GG";
  const params = new URLSearchParams({
    page: String(requestedPage),
    pageSize: String(pageSize),
    searchType,
  });
  const search = options.search?.trim();
  if (search) params.set("search", search);
  if (options.statusCodes && options.statusCodes.length > 0) {
    params.set("statusCodes", options.statusCodes.join(","));
  }
  if (options.list) params.set("list", options.list);

  return readJobCardPage(await requestApi(`api/jobcards/page?${params.toString()}`));
}

export async function getAuthorizerJobCardsPage(options: JobCardPageOptions = {}): Promise<JobCardPage> {
  const requestedPage = normalizePage(options.page);
  const pageSize = normalizePageSize(options.pageSize);
  const searchType = options.searchType === "GP" ? "GP" : "GG";
  const params = new URLSearchParams({
    page: String(requestedPage),
    pageSize: String(pageSize),
    searchType,
  });
  const search = options.search?.trim();
  if (search) params.set("search", search);
  if (options.statusCodes && options.statusCodes.length > 0) {
    params.set("statusCodes", options.statusCodes.join(","));
  }

  return readJobCardPage(await requestApi(`api/jobcards/authorizer/page?${params.toString()}`));
}

export async function getAuthorizerGgStatsPage(options: {
  page?: number;
  pageSize?: number;
  ggNumber?: string;
} = {}): Promise<JobCardAuthorizerGgStatsPage | null> {
  const params = new URLSearchParams({
    page: String(normalizePage(options.page)),
    pageSize: String(normalizePageSize(options.pageSize)),
  });
  const ggNumber = options.ggNumber?.trim();
  if (ggNumber) params.set("ggNumber", ggNumber);

  const payload = await readJson(
    await requestApi(`api/jobcards/authorizer/gg-stats?${params.toString()}`),
  );
  if (!isRecord(payload)) {
    throw new JobCardApiError(
      "invalid-response",
      "The FIS API returned an invalid authorizer GG stats page.",
    );
  }
  if (payload.overlay === false) {
    return null;
  }
  if (payload.overlay !== true || !Array.isArray(payload.items)) {
    throw new JobCardApiError(
      "invalid-response",
      "The FIS API returned an invalid authorizer GG stats page.",
    );
  }

  const metadata = readPageMetadata(payload, ["totalRecords", "total_records"]);
  if (!metadata) {
    throw new JobCardApiError(
      "invalid-response",
      "The FIS API returned incomplete authorizer GG stats pagination metadata.",
    );
  }

  return {
    items: payload.items.flatMap((item) => {
      if (!isRecord(item)) {
        return [];
      }
      const fleetNumber = asString(getValue(item, "ggNumber", "GGNumber", "fleet_number"));
      if (!fleetNumber) {
        return [];
      }
      return [
        {
          ggNumber: fleetNumber,
          jobcards: asNumber(getValue(item, "jobcards", "Jobcards")),
          pending: asNumber(getValue(item, "pending", "Pending")),
          awaitingAuthorisation: asNumber(
            getValue(item, "awaitingAuthorisation", "AwaitingAuthorisation"),
          ),
          authorised: asNumber(getValue(item, "authorised", "Authorised")),
          inProgress: asNumber(getValue(item, "inProgress", "Inprogress")),
          canceled: asNumber(getValue(item, "canceled", "Canceled")),
          failed: asNumber(getValue(item, "failed", "Failed")),
          completed: asNumber(getValue(item, "completed", "Completed")),
        },
      ];
    }),
    ...metadata,
  };
}

export async function getAuthorizerJobCardDetails(
  jobCardId: number,
): Promise<JobCardAuthorizerDetails | null> {
  const params = new URLSearchParams({ jobCardId: String(jobCardId) });
  const payload = await readJson(
    await requestApi(`api/jobcards/authorizer/details?${params.toString()}`),
  );
  if (!isRecord(payload) || typeof payload.overlay !== "boolean") {
    throw new JobCardApiError(
      "invalid-response",
      "The FIS API returned invalid authorizer job-card details.",
    );
  }
  if (payload.overlay === false) {
    return null;
  }
  const item = getValue(payload, "item");
  if (item === null || item === undefined) {
    return null;
  }
  if (!isRecord(item)) {
    throw new JobCardApiError(
      "invalid-response",
      "The FIS API returned invalid authorizer job-card details.",
    );
  }
  return {
    jobCardId: asNumber(getValue(item, "jobCardId", "job_card_id")),
    jcNumber: asString(getValue(item, "jcNumber", "jc_number")),
    ggNumber: asString(getValue(item, "ggNumber", "GGNumber")),
    extraDescription: asString(getValue(item, "extraDescription", "extra_description")),
    initialCapturedDate: asString(getValue(item, "initialCapturedDate", "InitialCapturedDate")),
    initialCapturer: asString(getValue(item, "initialCapturer", "InitialCapturer")),
    barcode: asString(getValue(item, "barcode")),
    capturedDate: asString(getValue(item, "capturedDate")),
    jobCardsCapturer: asString(getValue(item, "jobCardsCapturer", "JobCardsCapturer")),
    handoverName: asString(getValue(item, "handoverName")),
    handoverDate: asString(getValue(item, "handoverDate")),
    damages: asString(getValue(item, "damages", "Damages")),
    comments: asString(getValue(item, "comments")),
    statusDescription: asString(getValue(item, "statusDescription", "status_code_description")),
    priority: asString(getValue(item, "priority")),
    authorizer: asString(getValue(item, "authorizer", "Authorizer")),
    authorizedDate: asString(getValue(item, "authorizedDate", "AuthorizedDate")),
    authorizerComments: asString(getValue(item, "authorizerComments", "AuthorizerComments")),
  };
}

export async function getCapturerJobCardDetails(
  jobCardId: number,
): Promise<JobCardCapturerDetails | null> {
  const params = new URLSearchParams({ jobCardId: String(jobCardId) });
  const payload = await readJson(
    await requestApi(`api/jobcards/capturer/details?${params.toString()}`),
  );
  if (!isRecord(payload) || typeof payload.overlay !== "boolean") {
    throw new JobCardApiError(
      "invalid-response",
      "The FIS API returned invalid capturer job-card details.",
    );
  }
  if (payload.overlay === false) {
    return null;
  }
  const item = getValue(payload, "item");
  if (item === null || item === undefined) {
    return null;
  }
  if (!isRecord(item)) {
    throw new JobCardApiError(
      "invalid-response",
      "The FIS API returned invalid capturer job-card details.",
    );
  }
  return {
    jobCardId: asNumber(getValue(item, "jobCardId", "job_card_id")),
    jcNumber: asString(getValue(item, "jcNumber", "jc_number")),
    ggNumber: asString(getValue(item, "ggNumber", "GGNumber")),
    extraDescription: asString(getValue(item, "extraDescription", "extra_description")),
    barcode: asString(getValue(item, "barcode")),
    initialCapturer: asString(getValue(item, "initialCapturer", "InitialCapturer")),
    initialCapturedDate: asString(getValue(item, "initialCapturedDate", "initialCapturedDate")),
    jobCardsCapturer: asString(getValue(item, "jobCardsCapturer", "JobCardsCapturer")),
    capturedDate: asString(getValue(item, "capturedDate")),
    handoverName: asString(getValue(item, "handoverName")),
    handoverDate: asString(getValue(item, "handoverDate")),
    damages: asString(getValue(item, "damages", "Damages")),
    comments: asString(getValue(item, "comments")),
    statusDescription: asString(getValue(item, "statusDescription", "status_code_description")),
    jobcardComment: asString(getValue(item, "jobcardComment")),
    authorizer: asString(getValue(item, "authorizer", "Authorizer")),
    authorizerDate: asString(getValue(item, "authorizerDate", "AuthorizerDate")),
    authorizerComments: asString(getValue(item, "authorizerComments", "AuthorizerComments")),
  };
}

export async function getCloseJobCardDetails(
  jobCardId: number,
): Promise<{ overlay: boolean; item: JobCardCloseDetails | null }> {
  const params = new URLSearchParams({ jobCardId: String(jobCardId) });
  const payload = await readJson(
    await requestApi(`api/jobcards/close/details?${params.toString()}`),
  );
  if (!isRecord(payload) || typeof payload.overlay !== "boolean") {
    throw new JobCardApiError(
      "invalid-response",
      "The FIS API returned invalid close job-card details.",
    );
  }
  if (payload.overlay === false) {
    return { overlay: false, item: null };
  }
  const item = getValue(payload, "item");
  if (item === null || item === undefined) {
    return { overlay: true, item: null };
  }
  if (!isRecord(item)) {
    throw new JobCardApiError(
      "invalid-response",
      "The FIS API returned invalid close job-card details.",
    );
  }
  return {
    overlay: true,
    item: {
      jobCardId: asNumber(getValue(item, "jobCardId", "job_card_id")),
      ggNumber: asString(getValue(item, "ggNumber", "GGNumber")),
      jcNumber: asString(getValue(item, "jcNumber", "jc_number")),
      extraDescription: asString(getValue(item, "extraDescription", "extra_description")),
      jobCardsCapturer: asString(getValue(item, "jobCardsCapturer", "JobCardsCapturer")),
      capturedDate: asString(getValue(item, "capturedDate")),
      handoverName: asString(getValue(item, "handoverName")),
      handoverDate: asString(getValue(item, "handoverDate")),
      authorizer: asString(getValue(item, "authorizer", "Authorizer")),
      authorizedDate: asString(getValue(item, "authorizedDate", "AuthorizedDate")),
      authorizerComments: asString(getValue(item, "authorizerComments", "AuthorizerComments")),
      statusDescription: asString(getValue(item, "statusDescription", "status_code_description")),
      dateClosed: asString(getValue(item, "dateClosed", "DateClosed")),
      barcode: asString(getValue(item, "barcode")),
      jobcardComment: asString(getValue(item, "jobcardComment")),
      damages: asString(getValue(item, "damages", "Damages")),
      comments: asString(getValue(item, "comments")),
    },
  };
}

function mapPrintSnapshot(item: JsonRecord): JobCardPrintSnapshot {
  return {
    ggNumber: asString(getValue(item, "ggNumber", "GG Number")),
    registrationNumber: asString(getValue(item, "registrationNumber", "Registration Number")),
    dateDelivered: asString(getValue(item, "dateDelivered", "Date Delivered")),
    odoReading: asString(getValue(item, "odoReading", "Odo Reading")),
    vinNumber: asString(getValue(item, "vinNumber", "VIN Number")),
    engineNumber: asString(getValue(item, "engineNumber", "Engine Number")),
    modelDescription: asString(getValue(item, "modelDescription", "Model Description")),
    yearModel: asString(getValue(item, "yearModel", "Year Model")),
    classDescription: asString(getValue(item, "classDescription", "Class Description")),
    hireType: asString(getValue(item, "hireType", "Hire Type")),
    hiredFrom: asString(getValue(item, "hiredFrom", "Hired From")),
    location: asString(getValue(item, "location", "Location")),
    capturedDate: asString(getValue(item, "capturedDate", "captured_date")),
    receivedBy: asString(getValue(item, "receivedBy", "ReceivedBy")),
    status: asString(getValue(item, "status", "Status")),
    statusDate: asString(getValue(item, "statusDate", "Status Date")),
    purchasedFrom: asString(getValue(item, "purchasedFrom", "Purchased From")),
    purchasedDate: asString(getValue(item, "purchasedDate", "Purchased Date")),
    jobcardNumber: asString(getValue(item, "jobcardNumber", "Jobcard Number")),
    jobDescription: asString(getValue(item, "jobDescription", "Job Description")),
    jobcardStatus: asString(getValue(item, "jobcardStatus", "Jobcard Status")),
    capturedBy: asString(getValue(item, "capturedBy", "CapturedBy")),
    jcsDate: asString(getValue(item, "jcsDate", "jcs_date")),
    assignedTo: asString(getValue(item, "assignedTo", "AssignedTo")),
    assignedDate: asString(getValue(item, "assignedDate", "AssignedDate")),
  };
}

export async function getPrintableJobCards(
  ggNumber: string,
): Promise<{ overlay: true; items: JobCardPrintSummary[] } | { overlay: false }> {
  const params = new URLSearchParams({ ggNumber });
  const payload = await readJson(
    await requestApi(`api/jobcards/print/summary?${params.toString()}`),
  );
  if (!isRecord(payload) || typeof payload.overlay !== "boolean") {
    throw new JobCardApiError(
      "invalid-response",
      "The FIS API returned invalid printable job cards.",
    );
  }
  if (payload.overlay === false) {
    return { overlay: false };
  }
  const items = getValue(payload, "items");
  if (!Array.isArray(items)) {
    throw new JobCardApiError(
      "invalid-response",
      "The FIS API returned invalid printable job cards.",
    );
  }
  return {
    overlay: true,
    items: items.flatMap((item) => {
      if (!isRecord(item)) return [];
      const jobcardNumber = asString(getValue(item, "jobcardNumber", "Jobcard Number"));
      const fleetNumber = asString(getValue(item, "ggNumber", "GG Number"));
      if (!jobcardNumber || !fleetNumber) return [];
      return [
        {
          jobcardNumber,
          ggNumber: fleetNumber,
          registrationNumber: asString(getValue(item, "registrationNumber", "Registration Number")),
          jobcardDescription: asString(getValue(item, "jobcardDescription", "Jobcard Description")),
        },
      ];
    }),
  };
}

export async function getPrintJobCardSnapshot(
  ggNumber: string,
  jcNumber?: string,
): Promise<{ overlay: true; items: JobCardPrintSnapshot[] } | { overlay: false }> {
  const params = new URLSearchParams({ ggNumber });
  if (jcNumber && jcNumber.trim().length > 0) params.set("jcNumber", jcNumber.trim());
  const payload = await readJson(
    await requestApi(`api/jobcards/print/snapshot?${params.toString()}`),
  );
  if (!isRecord(payload) || typeof payload.overlay !== "boolean") {
    throw new JobCardApiError(
      "invalid-response",
      "The FIS API returned invalid print job-card snapshots.",
    );
  }
  if (payload.overlay === false) {
    return { overlay: false };
  }
  const items = getValue(payload, "items");
  if (!Array.isArray(items)) {
    throw new JobCardApiError(
      "invalid-response",
      "The FIS API returned invalid print job-card snapshots.",
    );
  }
  return {
    overlay: true,
    items: items.flatMap((item) => (isRecord(item) ? [mapPrintSnapshot(item)] : [])),
  };
}

export async function getPriorityUnassignedJobCardsPage(
  options: {
    page?: number;
    pageSize?: number;
  } = {},
): Promise<JobCardPage> {
  const params = new URLSearchParams({
    page: String(normalizePage(options.page)),
    pageSize: String(normalizePageSize(options.pageSize)),
  });
  const payload = await readJson(
    await requestApi(`api/jobcards/priority/unassigned/page?${params.toString()}`),
  );
  if (!isRecord(payload) || !Array.isArray(payload.items))
    throw new JobCardApiError(
      "invalid-response",
      "The FIS API returned an invalid priority job card page.",
    );
  const metadata = readPageMetadata(payload, ["totalRecords", "total_records", "total"]);
  if (!metadata)
    throw new JobCardApiError(
      "invalid-response",
      "The FIS API returned incomplete priority job card pagination metadata.",
    );
  return {
    items: payload.items.map(mapJobCard).filter((item): item is JobCardRecord => item !== null),
    ...metadata,
  };
}

export async function getAssignedPriorityJobCardsPage(
  options: {
    page?: number;
    pageSize?: number;
  } = {},
): Promise<JobCardPage> {
  const params = new URLSearchParams({
    page: String(normalizePage(options.page)),
    pageSize: String(normalizePageSize(options.pageSize)),
  });
  const payload = await readJson(
    await requestApi(`api/jobcards/priority/assigned/page?${params.toString()}`),
  );
  if (!isRecord(payload) || !Array.isArray(payload.items))
    throw new JobCardApiError(
      "invalid-response",
      "The FIS API returned an invalid assigned priority job card page.",
    );
  const metadata = readPageMetadata(payload, ["totalRecords", "total_records", "total"]);
  if (!metadata)
    throw new JobCardApiError(
      "invalid-response",
      "The FIS API returned incomplete assigned priority job card pagination metadata.",
    );
  return {
    items: payload.items.map(mapJobCard).filter((item): item is JobCardRecord => item !== null),
    ...metadata,
  };
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
  input: JobCardCostInput & {
    close_notes?: string | null;
    damages?: string | null;
    damage_comment?: string | null;
    barcode?: string | null;
    close_date?: string | null;
  },
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
  return readRepairCostReport(payload);
}

export async function getRepairCostReportPage(filters: {
  vmfCode?: number;
  siteCode?: number;
  fromDate?: string;
  toDate?: string;
  page?: number;
  pageSize?: number;
}): Promise<RepairCostReportPage> {
  const params = new URLSearchParams({
    page: String(normalizePage(filters.page)),
    pageSize: String(normalizePageSize(filters.pageSize)),
  });
  if (filters.vmfCode) params.set("vmfCode", String(filters.vmfCode));
  if (filters.siteCode) params.set("siteCode", String(filters.siteCode));
  if (filters.fromDate) params.set("fromDate", filters.fromDate);
  if (filters.toDate) params.set("toDate", filters.toDate);
  const payload = await readJson(
    await requestApi(`api/jobcards/repair-cost-report/page?${params.toString()}`),
  );
  if (!isRecord(payload))
    throw new JobCardApiError("invalid-response", "The repair cost report response was invalid.");
  const metadata = readPageMetadata(payload, ["totalRecords", "total_records"]);
  if (!metadata)
    throw new JobCardApiError(
      "invalid-response",
      "The FIS API returned incomplete repair cost pagination metadata.",
    );
  return { ...readRepairCostReport(payload), ...metadata };
}

function readRepairCostReport(payload: unknown): RepairCostReport {
  if (!isRecord(payload))
    throw new JobCardApiError("invalid-response", "The repair cost report response was invalid.");
  const filters = getValue(payload, "filters_applied", "filtersApplied");
  const filterRecord = isRecord(filters) ? filters : payload;
  return {
    filtersApplied: {
      vmfCode: asNumber(getValue(filterRecord, "vmf_code", "vmfCode")),
      siteCode: asNumber(getValue(filterRecord, "site_code", "siteCode")),
      fromDate: asString(getValue(filterRecord, "from_date", "fromDate")),
      toDate: asString(getValue(filterRecord, "to_date", "toDate")),
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
