import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;

export type SystemConfigurationStatus = {
  configurationManagementAvailable: boolean;
  managementDescription: string;
  session: {
    standardRefreshLifetimeMinutes: number;
    rememberRefreshLifetimeMinutes: number;
  };
  authentication: {
    entraEnabled: boolean;
    tenantId: string | null;
    clientId: string | null;
    clientSecretConfigured: boolean;
    passwordResetSigningKeyConfigured: boolean;
    restartRequired: boolean;
  };
};

export type SystemConfigurationUpdate = {
  session?: {
    standardRefreshLifetimeMinutes?: number;
    rememberRefreshLifetimeMinutes?: number;
  };
  authentication?: {
    entraEnabled?: boolean;
    tenantId?: string;
    clientId?: string;
    clientSecret?: string;
    passwordResetSigningKey?: string;
  };
};

export class SystemConfigurationApiError extends Error {
  constructor(
    public readonly reason: "unauthorized" | "unavailable" | "invalid-response",
    message: string,
  ) {
    super(message);
    this.name = "SystemConfigurationApiError";
  }
}

function apiBaseUrl() {
  const value = process.env.API_BASE_URL?.trim() || "http://localhost:5010";
  return `${value.replace(/\/$/, "")}/`;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function nullableString(value: unknown): string | null | undefined {
  if (value === null) return null;
  if (typeof value !== "string") return undefined;
  return value.trim() || null;
}

function finiteNumber(value: unknown) {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function mapStatus(payload: unknown): SystemConfigurationStatus | null {
  if (!isRecord(payload)) return null;
  if (
    typeof payload.configurationManagementAvailable !== "boolean" ||
    typeof payload.managementDescription !== "string" ||
    !isRecord(payload.session) ||
    !isRecord(payload.authentication)
  )
    return null;

  const standardRefreshLifetimeMinutes = finiteNumber(
    payload.session.standardRefreshLifetimeMinutes,
  );
  const rememberRefreshLifetimeMinutes = finiteNumber(
    payload.session.rememberRefreshLifetimeMinutes,
  );
  const tenantId = nullableString(payload.authentication.tenantId);
  const clientId = nullableString(payload.authentication.clientId);

  if (
    standardRefreshLifetimeMinutes === null ||
    rememberRefreshLifetimeMinutes === null ||
    tenantId === undefined ||
    clientId === undefined ||
    typeof payload.authentication.entraEnabled !== "boolean" ||
    typeof payload.authentication.clientSecretConfigured !== "boolean" ||
    typeof payload.authentication.passwordResetSigningKeyConfigured !== "boolean" ||
    typeof payload.authentication.restartRequired !== "boolean"
  )
    return null;

  return {
    configurationManagementAvailable: payload.configurationManagementAvailable,
    managementDescription: payload.managementDescription,
    session: {
      standardRefreshLifetimeMinutes,
      rememberRefreshLifetimeMinutes,
    },
    authentication: {
      entraEnabled: payload.authentication.entraEnabled,
      tenantId,
      clientId,
      clientSecretConfigured: payload.authentication.clientSecretConfigured,
      passwordResetSigningKeyConfigured: payload.authentication.passwordResetSigningKeyConfigured,
      restartRequired: payload.authentication.restartRequired,
    },
  };
}

async function request(path: string, init: RequestInit = {}) {
  const cookie = await getForwardedAuthCookieHeader();
  if (!cookie)
    throw new SystemConfigurationApiError(
      "unauthorized",
      "Your session has expired. Sign in again.",
    );

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
      throw new SystemConfigurationApiError(
        "unauthorized",
        "You do not have permission to manage system configuration.",
      );

    const payload = await response.json().catch(() => null);
    if (!response.ok) {
      const message = isRecord(payload) && typeof payload.title === "string" ? payload.title : null;
      throw new SystemConfigurationApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message ?? `The FIS API returned HTTP ${response.status}.`,
      );
    }

    return payload;
  } catch (error) {
    if (error instanceof SystemConfigurationApiError) throw error;
    throw new SystemConfigurationApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timer);
  }
}

export async function getSystemConfiguration() {
  const status = mapStatus(await request("api/system-configuration"));
  if (!status)
    throw new SystemConfigurationApiError(
      "invalid-response",
      "The FIS API returned an invalid system configuration status.",
    );
  return status;
}

export async function updateSystemConfiguration(input: SystemConfigurationUpdate) {
  const payload = await request("api/system-configuration", {
    method: "PUT",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(input),
  });

  if (!isRecord(payload) || typeof payload.description !== "string")
    throw new SystemConfigurationApiError(
      "invalid-response",
      "The FIS API returned an invalid system configuration update.",
    );
  return payload.description;
}
