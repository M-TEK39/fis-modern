import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";
import { getDepartments, type DepartmentRecord } from "@/lib/api/reference-data/api-departments";

const API_TIMEOUT_MS = 8_000;
export const DEFAULT_SITE_PAGE_SIZE = 24;
type JsonRecord = Record<string, unknown>;

export type SiteRecord = {
  siteCode: number;
  departmentCode: number | null;
  description: string | null;
  responsiblePerson: string | null;
  address1: string | null;
  address2: string | null;
  address3: string | null;
  postalCode: string | null;
  telephone: string | null;
  telephone2: string | null;
  fax: string | null;
  fax1: string | null;
  netAddress: string | null;
  departmentNumber: string | null;
  mapReference: string | null;
  mapDescription: string | null;
  cellNumber: string | null;
  siteActive: boolean;
  financialSystemCode: number | null;
  financialSystemActive: boolean | null;
  financialSystemActivateDate: string | null;
  exportIsActive: boolean | null;
  dateLastExported: string | null;
  serviceKilometres: number;
  serviceYears: number;
  overheadPercentage: number;
  provinceCode: string | null;
  notes: string | null;
  userAccessCode: number | null;
  modifiedByUserCode: number | null;
  dateCreated: string | null;
  dateUpdated: string | null;
};

export type SiteWriteInput = Omit<
  SiteRecord,
  "siteCode" | "modifiedByUserCode" | "dateCreated" | "dateUpdated"
>;
export type SiteOption = { code: string; description: string };
export type SiteReferenceData = { departments: DepartmentRecord[]; provinces: SiteOption[] };
export type SiteDeleteCheck = { contractCount: number; canDelete: boolean };
export type SitePage = {
  items: SiteRecord[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
};
export type SiteApiErrorReason = "unauthorized" | "unavailable" | "invalid-response";

export class SiteApiError extends Error {
  constructor(
    public readonly reason: SiteApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "SiteApiError";
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

function asBoolean(value: unknown) {
  if (typeof value === "boolean") return value;
  if (typeof value === "number") return value !== 0;
  return ["true", "1", "yes", "y"].includes(String(value).trim().toLowerCase());
}

function getCollection(payload: unknown) {
  if (Array.isArray(payload)) return payload;
  if (isRecord(payload)) {
    const collection = getValue(payload, "data", "items", "results");
    return Array.isArray(collection) ? collection : [];
  }
  return [];
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

function normalizePage(value: number | undefined) {
  return Number.isInteger(value) && (value ?? 0) > 0 ? (value ?? 1) : 1;
}

function normalizePageSize(value: number | undefined) {
  const pageSize =
    Number.isInteger(value) && (value ?? 0) > 0
      ? (value ?? DEFAULT_SITE_PAGE_SIZE)
      : DEFAULT_SITE_PAGE_SIZE;
  return Math.min(100, pageSize);
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) throw new SiteApiError("unauthorized", "No FIS access cookie is available.");

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
      throw new SiteApiError(
        "unauthorized",
        "The FIS access cookie was rejected.",
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
      throw new SiteApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
        response.status,
      );
    }
    return response;
  } catch (error) {
    if (error instanceof SiteApiError) throw error;
    throw new SiteApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new SiteApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

function mapSite(value: unknown): SiteRecord | null {
  if (!isRecord(value)) return null;
  const siteCode = asNumber(getValue(value, "siteCode", "SiteCode", "Site_code"));
  if (siteCode === null) return null;
  return {
    siteCode,
    departmentCode: asNumber(
      getValue(value, "departmentCode", "DepartmentCode", "Depatrment_code", "depatrment_code"),
    ),
    description: asString(getValue(value, "description", "Description")),
    responsiblePerson: asString(
      getValue(value, "responsiblePerson", "ResponsiblePerson", "res_person"),
    ),
    address1: asString(getValue(value, "address1", "Address1")),
    address2: asString(getValue(value, "address2", "Address2")),
    address3: asString(getValue(value, "address3", "Address3")),
    postalCode: asString(getValue(value, "postalCode", "PostalCode", "postal_code")),
    telephone: asString(getValue(value, "telephone", "Telephone")),
    telephone2: asString(getValue(value, "telephone2", "Telephone2")),
    fax: asString(getValue(value, "fax", "Fax")),
    fax1: asString(getValue(value, "fax1", "Fax1")),
    netAddress: asString(getValue(value, "netAddress", "NetAddress", "net_address")),
    departmentNumber: asString(
      getValue(value, "departmentNumber", "DepartmentNumber", "Department_number"),
    ),
    mapReference: asString(getValue(value, "mapReference", "MapReference", "Map_reference")),
    mapDescription: asString(
      getValue(value, "mapDescription", "MapDescription", "Map_description"),
    ),
    cellNumber: asString(getValue(value, "cellNumber", "CellNumber", "cell_number")),
    siteActive: asBoolean(getValue(value, "siteActive", "SiteActive", "site_active")),
    financialSystemCode: asNumber(
      getValue(value, "financialSystemCode", "FinancialSystemCode", "financial_system_code"),
    ),
    financialSystemActive:
      getValue(
        value,
        "financialSystemActive",
        "FinancialSystemActive",
        "financial_system_active",
      ) === undefined
        ? null
        : asBoolean(
            getValue(
              value,
              "financialSystemActive",
              "FinancialSystemActive",
              "financial_system_active",
            ),
          ),
    financialSystemActivateDate: asString(
      getValue(
        value,
        "financialSystemActivateDate",
        "FinancialSystemActivateDate",
        "financial_system_activate_date",
      ),
    ),
    exportIsActive:
      getValue(value, "exportIsActive", "ExportIsActive", "export_is_active") === undefined
        ? null
        : asBoolean(getValue(value, "exportIsActive", "ExportIsActive", "export_is_active")),
    dateLastExported: asString(
      getValue(value, "dateLastExported", "DateLastExported", "date_last_exported"),
    ),
    serviceKilometres:
      asNumber(getValue(value, "serviceKilometres", "ServiceKilometres", "Service_Kilometres")) ??
      0,
    serviceYears: asNumber(getValue(value, "serviceYears", "ServiceYears", "Service_Years")) ?? 0,
    overheadPercentage:
      asNumber(
        getValue(value, "overheadPercentage", "OverheadPercentage", "Overhead_Percentage"),
      ) ?? 0,
    provinceCode: asString(getValue(value, "provinceCode", "ProvinceCode", "province_code")),
    notes: asString(getValue(value, "notes", "Notes")),
    userAccessCode: asNumber(
      getValue(value, "userAccessCode", "UserAccessCode", "user_access_code"),
    ),
    modifiedByUserCode: asNumber(
      getValue(value, "modifiedByUserCode", "ModifiedByUserCode", "modified_by_user_code"),
    ),
    dateCreated: asString(getValue(value, "dateCreated", "DateCreated", "date_created")),
    dateUpdated: asString(getValue(value, "dateUpdated", "DateUpdated", "date_updated")),
  };
}

function mapOption(
  value: unknown,
  codeKeys: string[],
  descriptionKeys: string[],
): SiteOption | null {
  if (!isRecord(value)) return null;
  const code = asString(getValue(value, ...codeKeys));
  const description = asString(getValue(value, ...descriptionKeys));
  return code && description ? { code, description } : null;
}

export async function getSites() {
  const payload = await readJson(await requestApi("api/site"));
  if (!Array.isArray(payload))
    throw new SiteApiError("invalid-response", "The site response was not a list.");
  return payload.map(mapSite).filter((site): site is SiteRecord => site !== null);
}

export async function getSitesPage(
  options: { page?: number; pageSize?: number } = {},
): Promise<SitePage> {
  const params = new URLSearchParams({
    page: String(normalizePage(options.page)),
    pageSize: String(normalizePageSize(options.pageSize)),
  });
  const payload = await readJson(await requestApi(`api/site/page?${params.toString()}`));

  if (!isRecord(payload) || !Array.isArray(payload.items)) {
    throw new SiteApiError("invalid-response", "The FIS API returned an invalid site page.");
  }

  const metadata = readPageMetadata(payload);
  if (!metadata) {
    throw new SiteApiError(
      "invalid-response",
      "The FIS API returned incomplete site pagination metadata.",
    );
  }

  return {
    items: payload.items.map(mapSite).filter((site): site is SiteRecord => site !== null),
    ...metadata,
  };
}

export async function getSite(siteCode: number) {
  return mapSite(await readJson(await requestApi(`api/site/${encodeURIComponent(siteCode)}`)));
}

export async function getSiteDeleteCheck(siteCode: number): Promise<SiteDeleteCheck> {
  const payload = await readJson(
    await requestApi(`api/site/${encodeURIComponent(siteCode)}/delete-check`),
  );
  if (!isRecord(payload))
    throw new SiteApiError("invalid-response", "The site dependency response was invalid.");
  return {
    contractCount: asNumber(getValue(payload, "contractCount", "ContractCount")) ?? 0,
    canDelete: asBoolean(getValue(payload, "canDelete", "CanDelete")),
  };
}

export async function getSiteReferenceData(): Promise<SiteReferenceData> {
  const [departments, provinceResponse] = await Promise.all([
    getDepartments(),
    requestApi("api/province"),
  ]);
  const provinces = getCollection(await readJson(provinceResponse))
    .map((item) =>
      mapOption(
        item,
        ["provinceCode", "ProvinceCode", "province_code"],
        ["provinceName", "ProvinceName", "province_name"],
      ),
    )
    .filter((item): item is SiteOption => item !== null);
  return { departments, provinces };
}

function toRequest(input: SiteWriteInput) {
  return {
    departmentCode: input.departmentCode,
    description: input.description,
    responsiblePerson: input.responsiblePerson,
    address1: input.address1,
    address2: input.address2,
    address3: input.address3,
    postalCode: input.postalCode,
    telephone: input.telephone,
    fax: input.fax,
    netAddress: input.netAddress,
    departmentNumber: input.departmentNumber,
    mapReference: input.mapReference,
    mapDescription: input.mapDescription,
    cellNumber: input.cellNumber,
    siteActive: input.siteActive,
    telephone2: input.telephone2,
    fax1: input.fax1,
    financialSystemCode: input.financialSystemCode,
    financialSystemActive: input.financialSystemActive,
    financialSystemActivateDate: input.financialSystemActivateDate,
    exportIsActive: input.exportIsActive,
    dateLastExported: input.dateLastExported,
    serviceKilometres: input.serviceKilometres,
    serviceYears: input.serviceYears,
    overheadPercentage: input.overheadPercentage,
    provinceCode: input.provinceCode,
    notes: input.notes,
    userAccessCode: input.userAccessCode,
  };
}

export async function createSite(input: SiteWriteInput) {
  return mapSite(
    await readJson(
      await requestApi("api/site", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(toRequest(input)),
      }),
    ),
  );
}

export async function updateSite(siteCode: number, input: SiteWriteInput) {
  return mapSite(
    await readJson(
      await requestApi(`api/site/${encodeURIComponent(siteCode)}`, {
        method: "PUT",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(toRequest(input)),
      }),
    ),
  );
}

export async function deleteSite(siteCode: number) {
  await requestApi(`api/site/${encodeURIComponent(siteCode)}`, { method: "DELETE" });
}
