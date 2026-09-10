import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
type JsonRecord = Record<string, unknown>;

export type AuditApiErrorReason = "unauthorized" | "unavailable" | "invalid-response";

export class AuditApiError extends Error {
  constructor(
    public readonly reason: AuditApiErrorReason,
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = "AuditApiError";
  }
}

export type AuditItem = {
  auditId: number;
  action: string | null;
  tableName: string | null;
  primaryKey: string | null;
  changes: string | null;
  actionedBy: string | null;
  createdByUserCode: number | null;
  changedAt: string;
};

export type AuditPagedResult = {
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  items: AuditItem[];
};

export type UserStatusItem = {
  statusHistoryId: number;
  userAccessCode: number;
  newStatus: string | null;
  previousStatus: string | null;
  changedByUserCode: number | null;
  changedAt: string;
  reason: string | null;
};

export type UserStatusResult = {
  totalCount: number;
  totalPages: number;
  items: UserStatusItem[];
};

export type PasswordHistoryItem = {
  userAccessCode: number;
  lastPasswordChange: string;
  passwordExpiryDate: string | null;
  changedByUserCode: number | null;
  failedLoginAttempts: number;
  accountLockedUntil: string | null;
  isExpired: boolean;
};

export type PasswordHistoryResult = {
  totalCount: number;
  totalPages: number;
  items: PasswordHistoryItem[];
};

export type AuditTrailFilters = {
  tableName?: string;
  action?: string;
  fromDate?: string;
  toDate?: string;
  userId?: number;
  primaryKey?: string;
  pageNumber?: number;
  pageSize?: number;
};

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
  return ["true", "1", "yes", "y"].includes(String(value).trim().toLowerCase());
}

async function requestApi(path: string) {
  const cookieHeader = await getForwardedAuthCookieHeader();
  if (!cookieHeader) throw new AuditApiError("unauthorized", "No FIS access cookie is available.");

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      cache: "no-store",
      headers: { accept: "application/json", cookie: cookieHeader },
      signal: controller.signal,
    });

    if (response.status === 401 || response.status === 403)
      throw new AuditApiError(
        "unauthorized",
        "The FIS access cookie was rejected.",
        response.status,
      );
    if (!response.ok)
      throw new AuditApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        `FIS API returned HTTP ${response.status}.`,
        response.status,
      );

    try {
      return (await response.json()) as unknown;
    } catch {
      throw new AuditApiError("invalid-response", "The FIS API returned invalid JSON.");
    }
  } catch (error) {
    if (error instanceof AuditApiError) throw error;
    throw new AuditApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

function queryString(filters: Record<string, string | number | undefined>) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(filters)) {
    if (value !== undefined && String(value).trim() !== "") params.set(key, String(value));
  }
  return params.size > 0 ? `?${params.toString()}` : "";
}

function collection(value: unknown) {
  if (!isRecord(value)) return [];
  const items = getValue(value, "items", "Items");
  return Array.isArray(items) ? items : [];
}

function mapAuditItem(value: unknown): AuditItem | null {
  if (!isRecord(value)) return null;
  const auditId = asNumber(getValue(value, "auditID", "auditId", "AuditID"));
  const changedAt = asString(getValue(value, "changedAt", "ChangedAt"));
  if (auditId === null || !changedAt) return null;
  return {
    auditId,
    action: asString(getValue(value, "action", "Action")),
    tableName: asString(getValue(value, "tableName", "TableName")),
    primaryKey: asString(getValue(value, "primaryKey", "PrimaryKey")),
    changes: asString(getValue(value, "changes", "Changes")),
    actionedBy: asString(getValue(value, "actionedBy", "ActionedBy")),
    createdByUserCode: asNumber(getValue(value, "created_by_user_code", "createdByUserCode")),
    changedAt,
  };
}

function mapUserStatusItem(value: unknown): UserStatusItem | null {
  if (!isRecord(value)) return null;
  const statusHistoryId = asNumber(getValue(value, "status_history_id", "statusHistoryId"));
  const userAccessCode = asNumber(getValue(value, "user_access_code", "userAccessCode"));
  const changedAt = asString(getValue(value, "changed_at", "changedAt"));
  if (statusHistoryId === null || userAccessCode === null || !changedAt) return null;
  return {
    statusHistoryId,
    userAccessCode,
    newStatus: asString(getValue(value, "new_status", "newStatus")),
    previousStatus: asString(getValue(value, "previous_status", "previousStatus")),
    changedByUserCode: asNumber(getValue(value, "changed_by_user_code", "changedByUserCode")),
    changedAt,
    reason: asString(getValue(value, "reason", "Reason")),
  };
}

function mapPasswordHistoryItem(value: unknown): PasswordHistoryItem | null {
  if (!isRecord(value)) return null;
  const userAccessCode = asNumber(getValue(value, "user_access_code", "userAccessCode"));
  const lastPasswordChange = asString(
    getValue(value, "last_password_change", "lastPasswordChange"),
  );
  if (userAccessCode === null || !lastPasswordChange) return null;
  return {
    userAccessCode,
    lastPasswordChange,
    passwordExpiryDate: asString(getValue(value, "password_expiry_date", "passwordExpiryDate")),
    changedByUserCode: asNumber(getValue(value, "changed_by_user_code", "changedByUserCode")),
    failedLoginAttempts:
      asNumber(getValue(value, "failed_login_attempts", "failedLoginAttempts")) ?? 0,
    accountLockedUntil: asString(getValue(value, "account_locked_until", "accountLockedUntil")),
    isExpired: asBoolean(getValue(value, "isExpired", "IsExpired")),
  };
}

export async function getAuditTrail(filters: AuditTrailFilters = {}): Promise<AuditPagedResult> {
  const payload = await requestApi(`api/audit${queryString(filters)}`);
  if (!isRecord(payload))
    throw new AuditApiError("invalid-response", "The FIS API returned an invalid audit result.");
  const items = collection(payload)
    .map(mapAuditItem)
    .filter((item): item is AuditItem => item !== null);
  return {
    totalCount: asNumber(getValue(payload, "totalCount", "TotalCount")) ?? items.length,
    pageNumber: asNumber(getValue(payload, "pageNumber", "PageNumber")) ?? filters.pageNumber ?? 1,
    pageSize: asNumber(getValue(payload, "pageSize", "PageSize")) ?? filters.pageSize ?? 24,
    totalPages:
      asNumber(getValue(payload, "totalPages", "TotalPages")) ?? (items.length > 0 ? 1 : 0),
    items,
  };
}

export async function getUserStatusHistory(
  filters: {
    userAccessCode?: number;
    fromDate?: string;
    toDate?: string;
    pageNumber?: number;
    pageSize?: number;
  } = {},
): Promise<UserStatusResult> {
  const payload = await requestApi(`api/audit/user-status-history${queryString(filters)}`);
  if (!isRecord(payload))
    throw new AuditApiError(
      "invalid-response",
      "The FIS API returned an invalid user status result.",
    );
  const items = collection(payload)
    .map(mapUserStatusItem)
    .filter((item): item is UserStatusItem => item !== null);
  return {
    totalCount: asNumber(getValue(payload, "totalCount", "TotalCount")) ?? items.length,
    totalPages:
      asNumber(getValue(payload, "totalPages", "TotalPages")) ?? (items.length > 0 ? 1 : 0),
    items,
  };
}

export async function getPasswordHistory(
  filters: { userAccessCode?: number; pageNumber?: number; pageSize?: number } = {},
): Promise<PasswordHistoryResult> {
  const payload = await requestApi(`api/audit/password-history${queryString(filters)}`);
  if (!isRecord(payload))
    throw new AuditApiError(
      "invalid-response",
      "The FIS API returned an invalid password history result.",
    );
  const items = collection(payload)
    .map(mapPasswordHistoryItem)
    .filter((item): item is PasswordHistoryItem => item !== null);
  return {
    totalCount: asNumber(getValue(payload, "totalCount", "TotalCount")) ?? items.length,
    totalPages:
      asNumber(getValue(payload, "totalPages", "TotalPages")) ?? (items.length > 0 ? 1 : 0),
    items,
  };
}
