import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;

export type ManagedSession = {
  userAccessCode: number;
  createdAtUtc: string;
  expiresAtUtc: string;
  rememberMe: boolean;
  isCurrent: boolean;
};

export type SessionManagementStatus = {
  durableSessionManagementAvailable: boolean;
  description: string;
  accessTokenLifetimeMinutes: number;
  standardRefreshLifetimeMinutes: number;
  rememberRefreshLifetimeMinutes: number;
  sessions: ManagedSession[];
};

export type SessionRevokeScope = "current" | "user" | "all";

export type SessionRevokeInput = {
  scope: SessionRevokeScope;
  userAccessCode?: number;
};

export class SessionManagementApiError extends Error {
  constructor(
    public readonly reason: "unauthorized" | "unavailable" | "invalid-response",
    message: string,
  ) {
    super(message);
    this.name = "SessionManagementApiError";
  }
}

function apiBaseUrl() {
  const value = process.env.API_BASE_URL?.trim() || "http://localhost:5010";
  return `${value.replace(/\/$/, "")}/`;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function requiredString(value: unknown) {
  return typeof value === "string" && value.trim() ? value : null;
}

function finiteNumber(value: unknown) {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function mapSession(value: unknown): ManagedSession | null {
  if (!isRecord(value)) return null;
  const userAccessCode = finiteNumber(value.userAccessCode);
  const createdAtUtc = requiredString(value.createdAtUtc);
  const expiresAtUtc = requiredString(value.expiresAtUtc);

  if (
    userAccessCode === null ||
    !Number.isInteger(userAccessCode) ||
    userAccessCode <= 0 ||
    !createdAtUtc ||
    !expiresAtUtc ||
    typeof value.rememberMe !== "boolean" ||
    typeof value.isCurrent !== "boolean"
  )
    return null;

  return {
    userAccessCode,
    createdAtUtc,
    expiresAtUtc,
    rememberMe: value.rememberMe,
    isCurrent: value.isCurrent,
  };
}

function mapStatus(payload: unknown): SessionManagementStatus | null {
  if (!isRecord(payload) || !Array.isArray(payload.sessions)) return null;

  const sessions: ManagedSession[] = [];
  for (const item of payload.sessions) {
    const session = mapSession(item);
    if (!session) return null;
    sessions.push(session);
  }

  const accessTokenLifetimeMinutes = finiteNumber(payload.accessTokenLifetimeMinutes);
  const standardRefreshLifetimeMinutes = finiteNumber(payload.standardRefreshLifetimeMinutes);
  const rememberRefreshLifetimeMinutes = finiteNumber(payload.rememberRefreshLifetimeMinutes);
  if (
    typeof payload.durableSessionManagementAvailable !== "boolean" ||
    typeof payload.description !== "string" ||
    accessTokenLifetimeMinutes === null ||
    standardRefreshLifetimeMinutes === null ||
    rememberRefreshLifetimeMinutes === null
  )
    return null;

  return {
    durableSessionManagementAvailable: payload.durableSessionManagementAvailable,
    description: payload.description,
    accessTokenLifetimeMinutes,
    standardRefreshLifetimeMinutes,
    rememberRefreshLifetimeMinutes,
    sessions,
  };
}

async function request(path: string, init: RequestInit = {}) {
  const cookie = await getForwardedAuthCookieHeader();
  if (!cookie)
    throw new SessionManagementApiError("unauthorized", "Your session has expired. Sign in again.");

  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  const headers = new Headers(init.headers);
  headers.set("accept", "application/json");
  headers.set("cookie", cookie);

  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), apiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers,
      signal: controller.signal,
    });

    if (response.status === 401 || response.status === 403)
      throw new SessionManagementApiError(
        "unauthorized",
        "You do not have permission to manage sessions.",
      );

    const payload = await response.json().catch(() => null);
    if (!response.ok) {
      const message = isRecord(payload) && typeof payload.title === "string" ? payload.title : null;
      throw new SessionManagementApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message ?? `The FIS API returned HTTP ${response.status}.`,
      );
    }

    return payload;
  } catch (error) {
    if (error instanceof SessionManagementApiError) throw error;
    throw new SessionManagementApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timer);
  }
}

export async function getSessionManagement() {
  const status = mapStatus(await request("api/session-management"));
  if (!status)
    throw new SessionManagementApiError(
      "invalid-response",
      "The FIS API returned an invalid session management status.",
    );
  return status;
}

export async function revokeSessions(input: SessionRevokeInput) {
  if (input.scope === "user" && (!input.userAccessCode || input.userAccessCode <= 0))
    throw new SessionManagementApiError(
      "invalid-response",
      "A user access code is required to revoke that user's sessions.",
    );

  const body: SessionRevokeInput =
    input.scope === "user"
      ? { scope: "user", userAccessCode: input.userAccessCode }
      : { scope: input.scope };
  const payload = await request("api/session-management/revoke", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(body),
  });

  if (!isRecord(payload) || typeof payload.description !== "string")
    throw new SessionManagementApiError(
      "invalid-response",
      "The FIS API returned an invalid session revocation result.",
    );
  return payload.description;
}
