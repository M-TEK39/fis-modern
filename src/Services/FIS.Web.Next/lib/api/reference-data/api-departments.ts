import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type DepartmentRecord = {
  departmentCode: number;
  companyCode: number;
  description: string | null;
  responsiblePerson: string | null;
  address1: string | null;
  address2: string | null;
  address3: string | null;
  postalCode: string | null;
  telephone: string | null;
  fax: string | null;
  netAddress: string | null;
  departmentNumber: string | null;
  cellNumber: string | null;
  notes: string | null;
  departmentAbbr: string | null;
  basInstallationCode: string | null;
  deptActive: boolean;
  cloEmail: string | null;
  telephone2: string | null;
  fax2: string | null;
  financialSystemCode: number | null;
  financialSystemActive: boolean | null;
  financialSystemActivateDate: string | null;
  defaultSite: number | null;
  exportIsActive: boolean | null;
  dateLastExported: string | null;
  serviceKilometres: number;
  serviceYears: number;
  overheadPercentage: number;
  dateCreated: string | null;
  dateUpdated: string | null;
  userAccessCode: number | null;
  modifiedByUserCode: number | null;
  comments: string | null;
};

export type DepartmentInput = Omit<
  DepartmentRecord,
  "departmentCode" | "dateCreated" | "dateUpdated" | "modifiedByUserCode"
> & {
  departmentCode?: number;
};

export type DepartmentDeleteCheck = {
  siteCount: number;
  logsheetCount: number;
  canDelete: boolean;
};

export type DepartmentApiErrorReason = "unauthorized" | "unavailable" | "invalid-response";

export class DepartmentApiError extends Error {
  constructor(
    public readonly reason: DepartmentApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "DepartmentApiError";
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

function asBoolean(value: unknown) {
  if (typeof value === "boolean") return value;
  if (typeof value === "number") return value !== 0;
  return ["true", "1", "yes", "y"].includes(String(value).trim().toLowerCase());
}

function mapDepartment(value: unknown): DepartmentRecord | null {
  if (!isRecord(value)) return null;

  const departmentCode = asNumber(
    getValue(value, "departmentCode", "DepartmentCode", "department_code"),
  );
  if (departmentCode === null) return null;

  return {
    departmentCode,
    companyCode: asNumber(getValue(value, "companyCode", "CompanyCode", "company_code")) ?? 0,
    description: asString(getValue(value, "description", "Description")),
    responsiblePerson: asString(
      getValue(value, "responsiblePerson", "ResponsiblePerson", "res_person"),
    ),
    address1: asString(getValue(value, "address1", "Address1")),
    address2: asString(getValue(value, "address2", "Address2")),
    address3: asString(getValue(value, "address3", "Address3")),
    postalCode: asString(getValue(value, "postalCode", "PostalCode", "postal_code")),
    telephone: asString(getValue(value, "telephone", "Telephone")),
    fax: asString(getValue(value, "fax", "Fax")),
    netAddress: asString(getValue(value, "netAddress", "NetAddress", "net_address")),
    departmentNumber: asString(
      getValue(value, "departmentNumber", "DepartmentNumber", "Department_number"),
    ),
    cellNumber: asString(getValue(value, "cellNumber", "CellNumber", "cell_number")),
    notes: asString(getValue(value, "notes", "Notes")),
    departmentAbbr: asString(
      getValue(value, "departmentAbbr", "DepartmentAbbr", "department_abbr"),
    ),
    basInstallationCode: asString(
      getValue(value, "basInstallationCode", "BasInstallationCode", "bas_installation_code"),
    ),
    deptActive: asBoolean(getValue(value, "deptActive", "DeptActive", "dept_active")),
    cloEmail: asString(getValue(value, "cloEmail", "CloEmail", "clo_email")),
    telephone2: asString(getValue(value, "telephone2", "Telephone2")),
    fax2: asString(getValue(value, "fax2", "Fax2")),
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
    defaultSite: asNumber(getValue(value, "defaultSite", "DefaultSite", "default_site")),
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
    dateCreated: asString(getValue(value, "dateCreated", "DateCreated", "date_created")),
    dateUpdated: asString(getValue(value, "dateUpdated", "DateUpdated", "date_updated")),
    userAccessCode: asNumber(
      getValue(value, "userAccessCode", "UserAccessCode", "user_access_code"),
    ),
    modifiedByUserCode: asNumber(
      getValue(value, "modifiedByUserCode", "ModifiedByUserCode", "modified_by_user_code"),
    ),
    comments: asString(getValue(value, "comments", "Comments")),
  };
}

async function requestApi(path: string, init: RequestInit = {}) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader)
    throw new DepartmentApiError("unauthorized", "No FIS access cookie is available.");

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
      throw new DepartmentApiError(
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

      throw new DepartmentApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message,
        response.status,
      );
    }

    return response;
  } catch (error) {
    if (error instanceof DepartmentApiError) throw error;
    throw new DepartmentApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new DepartmentApiError("invalid-response", "The FIS API returned invalid JSON.");
  }
}

export async function getDepartments() {
  const payload = await readJson(await requestApi("api/department"));
  if (!Array.isArray(payload))
    throw new DepartmentApiError("invalid-response", "The department response was not a list.");
  return payload
    .map(mapDepartment)
    .filter((department): department is DepartmentRecord => department !== null);
}

export async function getDepartment(departmentCode: number) {
  const payload = await readJson(await requestApi(`api/department/${departmentCode}`));
  return mapDepartment(payload);
}

export async function getDepartmentDeleteCheck(departmentCode: number) {
  const payload = await readJson(await requestApi(`api/department/${departmentCode}/delete-check`));
  if (!isRecord(payload))
    throw new DepartmentApiError(
      "invalid-response",
      "The department dependency response was invalid.",
    );

  return {
    siteCount: asNumber(getValue(payload, "siteCount", "SiteCount")) ?? 0,
    logsheetCount: asNumber(getValue(payload, "logsheetCount", "LogsheetCount")) ?? 0,
    canDelete: asBoolean(getValue(payload, "canDelete", "CanDelete")),
  } satisfies DepartmentDeleteCheck;
}

export async function createDepartment(department: DepartmentInput) {
  const payload = await readJson(
    await requestApi("api/department", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(department),
    }),
  );
  return mapDepartment(payload);
}

export async function updateDepartment(departmentCode: number, department: DepartmentInput) {
  const payload = await readJson(
    await requestApi(`api/department/${departmentCode}`, {
      method: "PUT",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(department),
    }),
  );
  return mapDepartment(payload);
}

export async function deleteDepartment(departmentCode: number) {
  await requestApi(`api/department/${departmentCode}`, { method: "DELETE" });
}
