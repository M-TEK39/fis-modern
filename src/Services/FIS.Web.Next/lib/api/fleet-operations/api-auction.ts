import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type AuctionSearchType = "GG" | "GP";

export type AuctionRecord = {
  auctionCode: number;
  vmfCode: number;
  auctionNumber: string | null;
  camp: string | null;
  lot: number | null;
  auctionGarage: number | null;
  authNumber: string | null;
  authDate: string | null;
  auctionKm: number | null;
  garageOwner: string | null;
  reasonSold: string | null;
  estimateAmount: number | null;
  reserveAmount: number | null;
  soldId: string | null;
  remark: string | null;
  barcode: string | null;
  soldTo: string | null;
  soldDate: string | null;
  soldAmount: number | null;
  fleetNumber: string | null;
  registrationNumber: string | null;
};

export type AuctionPage = {
  items: AuctionRecord[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
};

export type AuctionMaintenanceRequest = {
  auction_code: number;
  vmf_code: number;
  auction_number: string | null;
  camp: string | null;
  lot: number | null;
  auction_garage: number | null;
  auth_number: string | null;
  auth_date: string | null;
  auction_km: number | null;
  garage_owner: string | null;
  reason_sold: string | null;
  estimate_amount: number | null;
  reserve_amount: number | null;
  sold_id: string | null;
  remark: string | null;
  barcode: string | null;
  sold_to: string | null;
  sold_date: string | null;
  sold_amount: number | null;
};

export type AuctionReport = {
  reportType: string;
  data: AuctionRecord[];
};

export type AuctionApiErrorReason =
  "unauthorized" | "unavailable" | "invalid-response" | "not-found";

export class AuctionApiError extends Error {
  constructor(
    public readonly reason: AuctionApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "AuctionApiError";
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
    const value = getValue(payload, "data", "items", "results");
    return Array.isArray(value) ? value : [];
  }

  return [];
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) {
    throw new AuctionApiError("unauthorized", "No FIS access cookie is available.");
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
      throw new AuctionApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (response.status === 404) {
      throw new AuctionApiError("not-found", "The requested auction record was not found.");
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

      throw new AuctionApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
      );
    }

    return response;
  } catch (error) {
    if (error instanceof AuctionApiError) {
      throw error;
    }

    throw new AuctionApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new AuctionApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapAuction(value: unknown): AuctionRecord | null {
  if (!isRecord(value)) {
    return null;
  }

  const auctionCode = asNumber(getValue(value, "auction_code", "auctionCode"));
  const vmfCode = asNumber(getValue(value, "vmf_code", "vmfCode"));
  if (auctionCode === null || vmfCode === null) {
    return null;
  }

  return {
    auctionCode,
    vmfCode,
    auctionNumber: asString(getValue(value, "auction_number", "auctionNumber")),
    camp: asString(getValue(value, "camp")),
    lot: asNumber(getValue(value, "lot")),
    auctionGarage: asNumber(getValue(value, "auction_garage", "auctionGarage")),
    authNumber: asString(getValue(value, "auth_number", "authNumber")),
    authDate: asString(getValue(value, "auth_date", "authDate")),
    auctionKm: asNumber(getValue(value, "auction_km", "auctionKm")),
    garageOwner: asString(getValue(value, "garage_owner", "garageOwner")),
    reasonSold: asString(getValue(value, "reason_sold", "reasonSold")),
    estimateAmount: asNumber(getValue(value, "estimate_amount", "estimateAmount")),
    reserveAmount: asNumber(getValue(value, "reserve_amount", "reserveAmount")),
    soldId: asString(getValue(value, "sold_id", "soldId")),
    remark: asString(getValue(value, "remark")),
    barcode: asString(getValue(value, "barcode")),
    soldTo: asString(getValue(value, "sold_to", "soldTo")),
    soldDate: asString(getValue(value, "sold_date", "soldDate")),
    soldAmount: asNumber(getValue(value, "sold_amount", "soldAmount")),
    fleetNumber: asString(getValue(value, "fleet_number", "fleetNumber")),
    registrationNumber: asString(getValue(value, "registration_number", "registrationNumber")),
  };
}

function pageNumber(value: unknown, fallback: number) {
  const parsed = asNumber(value);
  return parsed !== null && Number.isSafeInteger(parsed) && parsed > 0 ? parsed : fallback;
}

function mapReport(value: unknown): AuctionReport {
  if (!isRecord(value)) {
    throw new AuctionApiError(
      "invalid-response",
      "The FIS API returned an invalid auction report.",
    );
  }

  return {
    reportType: asString(getValue(value, "ReportType", "reportType")) ?? "Auction",
    data: getCollection(getValue(value, "Data", "data", "items"))
      .map(mapAuction)
      .filter((auction): auction is AuctionRecord => auction !== null),
  };
}

export async function getAuctions() {
  const response = await requestApi("api/Auction");
  return getCollection(await readJson(response))
    .map(mapAuction)
    .filter((auction): auction is AuctionRecord => auction !== null)
    .sort(
      (left, right) =>
        (right.authDate ?? "").localeCompare(left.authDate ?? "") ||
        right.auctionCode - left.auctionCode,
    );
}

export async function getAuctionPage(
  searchType: AuctionSearchType,
  searchQuery: string,
  page = 1,
  pageSize = 24,
): Promise<AuctionPage> {
  const params = new URLSearchParams({
    searchType,
    searchQuery,
    page: String(page),
    pageSize: String(pageSize),
  });
  const payload = await readJson(await requestApi(`api/Auction/page?${params.toString()}`));
  if (!isRecord(payload)) {
    throw new AuctionApiError("invalid-response", "The FIS API returned an invalid auction page.");
  }

  const items = getCollection(payload)
    .map(mapAuction)
    .filter((auction): auction is AuctionRecord => auction !== null);
  const resolvedPageSize = pageNumber(getValue(payload, "pageSize", "PageSize"), pageSize);
  const total = Math.max(0, asNumber(getValue(payload, "total", "Total")) ?? items.length);
  return {
    items,
    page: pageNumber(getValue(payload, "page", "Page"), 1),
    pageSize: resolvedPageSize,
    total,
    totalPages: pageNumber(
      getValue(payload, "totalPages", "TotalPages"),
      Math.max(1, Math.ceil(total / resolvedPageSize)),
    ),
  };
}

export async function getAuction(auctionCode: number) {
  const response = await requestApi(`api/Auction/${encodeURIComponent(auctionCode)}`);
  const auction = mapAuction(await readJson(response));
  if (!auction) {
    throw new AuctionApiError(
      "invalid-response",
      "The FIS API returned an invalid auction record.",
    );
  }

  return auction;
}

export async function updateAuctionMaintenanceAgainstApi(
  auctionCode: number,
  request: AuctionMaintenanceRequest,
) {
  const response = await requestApi(`api/Auction/${encodeURIComponent(auctionCode)}/maintenance`, {
    method: "PUT",
    body: JSON.stringify(request),
  });
  return mapAuction(await readJson(response));
}

export async function deleteAuctionAgainstApi(auctionCode: number) {
  await requestApi(`api/Auction/${encodeURIComponent(auctionCode)}`, { method: "DELETE" });
}

async function postReport(path: string, body: object) {
  const response = await requestApi(`api/Auction/reports/${path}`, {
    method: "POST",
    body: JSON.stringify(body),
  });
  return mapReport(await readJson(response));
}

export function getAuctionOneVehicleReport(vmfCode: number) {
  return postReport("one-vehicle", { VmfCode: vmfCode });
}

export function getAuctionAllVehiclesReport(auctionNumber: string, garage: string) {
  return postReport("all-vehicles", {
    StartDate: "1900-01-01T00:00:00.000Z",
    EndDate: "2100-12-31T00:00:00.000Z",
    AuctionNumber: auctionNumber,
    Garage: garage,
  });
}

export function getAuctionSaleToNameReport(buyerName: string) {
  return postReport("sale-to-name", {
    BuyerName: buyerName,
    StartDate: "1900-01-01T00:00:00.000Z",
    EndDate: "2100-12-31T00:00:00.000Z",
  });
}

export function getAuctionByNumberReport(auctionNumber: string, garage: string) {
  return postReport("auction-gg", { AuctionNumber: auctionNumber, Garage: garage });
}

export function getAuctionByLotReport(auctionNumber: string, garage: string) {
  return postReport("auction-lot", { AuctionNumber: auctionNumber, Garage: garage });
}
