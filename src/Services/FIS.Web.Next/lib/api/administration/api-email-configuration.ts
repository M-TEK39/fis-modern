import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;

export type EmailProvider = "Graph" | "Smtp" | "SendGrid";

export type EmailProviderStatus = {
  provider: EmailProvider;
  enabled: boolean;
  configured: boolean;
  requiresActivation: boolean;
  available: boolean;
  health: string;
  configurationSource: string;
  supportsAttachments: boolean;
  description: string;
  consecutiveFailureCount: number;
  circuitOpenUntilUtc: string | null;
  lastSuccessfulSendUtc: string | null;
  lastSuccessfulTestUtc: string | null;
  recentSuccessfulTestUntilUtc: string | null;
};

export type EmailConfigurationStatus = {
  providerOrder: EmailProvider[];
  providers: EmailProviderStatus[];
  configurationManagementAvailable: boolean;
  managementDescription: string;
  timeoutSeconds: number;
  maxTotalAttachmentBytes: number;
  graph: {
    endpoint: string;
    authentication: "ManagedIdentity" | "ClientSecret";
    tenantId: string | null;
    clientId: string | null;
    managedIdentityClientId: string | null;
    senderUserPrincipalName: string | null;
    fromAddress: string | null;
    fromName: string | null;
    clientSecretConfigured: boolean;
  };
  smtp: {
    host: string | null;
    port: number;
    securityMode: "StartTls" | "SslOnConnect";
    authentication: "None" | "Password" | "GoogleOAuth2";
    username: string | null;
    fromAddress: string | null;
    fromName: string | null;
    passwordConfigured: boolean;
    googleOAuthClientId: string | null;
    googleOAuthClientSecretConfigured: boolean;
    googleOAuthRefreshTokenConfigured: boolean;
  };
  sendGrid: { fromEmail: string | null; fromName: string | null; apiKeyConfigured: boolean };
};

export type EmailConfigurationUpdate = {
  providerOrder?: EmailProvider[];
  timeoutSeconds?: number;
  maxTotalAttachmentBytes?: number;
  graph?: {
    endpoint?: string;
    authentication?: "ManagedIdentity" | "ClientSecret";
    tenantId?: string;
    clientId?: string;
    managedIdentityClientId?: string;
    senderUserPrincipalName?: string;
    fromAddress?: string;
    fromName?: string;
    clientSecret?: string;
  };
  smtp?: {
    host?: string;
    port?: number;
    securityMode?: "StartTls" | "SslOnConnect";
    authentication?: "None" | "Password" | "GoogleOAuth2";
    username?: string;
    password?: string;
    googleOAuthClientId?: string;
    googleOAuthClientSecret?: string;
    googleOAuthRefreshToken?: string;
    fromAddress?: string;
    fromName?: string;
  };
  sendGrid?: { fromEmail?: string; fromName?: string; apiKey?: string };
};

export class EmailConfigurationApiError extends Error {
  constructor(
    public readonly reason: "unauthorized" | "unavailable" | "invalid-response",
    message: string,
  ) {
    super(message);
    this.name = "EmailConfigurationApiError";
  }
}

function apiBaseUrl() {
  const value = process.env.API_BASE_URL?.trim() || "http://localhost:5010";
  return `${value.replace(/\/$/, "")}/`;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function stringValue(value: unknown) {
  return typeof value === "string" ? value : null;
}

function booleanValue(value: unknown) {
  return typeof value === "boolean" ? value : false;
}

function numberValue(value: unknown) {
  return typeof value === "number" && Number.isFinite(value) ? value : 0;
}

function nullableString(value: unknown) {
  return typeof value === "string" && value.trim() ? value : null;
}

function provider(value: unknown): EmailProvider | null {
  return value === "Graph" || value === "Smtp" || value === "SendGrid" ? value : null;
}

function mapStatus(payload: unknown): EmailConfigurationStatus | null {
  if (!isRecord(payload)) return null;
  const graph = payload.graph;
  const smtp = payload.smtp;
  const sendGrid = payload.sendGrid;
  if (!isRecord(graph) || !isRecord(smtp) || !isRecord(sendGrid)) return null;
  const providerOrder = Array.isArray(payload.providerOrder)
    ? payload.providerOrder.map(provider).filter((item): item is EmailProvider => item !== null)
    : [];
  const providers = Array.isArray(payload.providers)
    ? payload.providers.flatMap((item): EmailProviderStatus[] => {
        if (!isRecord(item)) return [];
        const itemProvider = provider(item.provider);
        if (!itemProvider) return [];
        return [
          {
            provider: itemProvider,
            enabled: booleanValue(item.enabled),
            configured: booleanValue(item.configured),
            requiresActivation: booleanValue(item.requiresActivation),
            available: booleanValue(item.available),
            health: stringValue(item.health) ?? "Unknown",
            configurationSource: stringValue(item.configurationSource) ?? "Unavailable",
            supportsAttachments: booleanValue(item.supportsAttachments),
            description: stringValue(item.description) ?? "No provider status is available.",
            consecutiveFailureCount: numberValue(item.consecutiveFailureCount),
            circuitOpenUntilUtc: nullableString(item.circuitOpenUntilUtc),
            lastSuccessfulSendUtc: nullableString(item.lastSuccessfulSendUtc),
            lastSuccessfulTestUtc: nullableString(item.lastSuccessfulTestUtc),
            recentSuccessfulTestUntilUtc: nullableString(item.recentSuccessfulTestUntilUtc),
          },
        ];
      })
    : [];
  const graphAuthentication = graph.authentication;
  const smtpSecurityMode = smtp.securityMode;
  const smtpAuthentication = smtp.authentication;
  if (
    (graphAuthentication !== "ManagedIdentity" && graphAuthentication !== "ClientSecret") ||
    (smtpSecurityMode !== "StartTls" && smtpSecurityMode !== "SslOnConnect") ||
    (smtpAuthentication !== "None" &&
      smtpAuthentication !== "Password" &&
      smtpAuthentication !== "GoogleOAuth2")
  )
    return null;
  return {
    providerOrder,
    providers,
    configurationManagementAvailable: booleanValue(payload.configurationManagementAvailable),
    managementDescription:
      stringValue(payload.managementDescription) ?? "Configuration status is unavailable.",
    timeoutSeconds: numberValue(payload.timeoutSeconds),
    maxTotalAttachmentBytes: numberValue(payload.maxTotalAttachmentBytes),
    graph: {
      endpoint: stringValue(graph.endpoint) ?? "https://graph.microsoft.com/v1.0",
      authentication: graphAuthentication,
      tenantId: nullableString(graph.tenantId),
      clientId: nullableString(graph.clientId),
      managedIdentityClientId: nullableString(graph.managedIdentityClientId),
      senderUserPrincipalName: nullableString(graph.senderUserPrincipalName),
      fromAddress: nullableString(graph.fromAddress),
      fromName: nullableString(graph.fromName),
      clientSecretConfigured: booleanValue(graph.clientSecretConfigured),
    },
    smtp: {
      host: nullableString(smtp.host),
      port: numberValue(smtp.port),
      securityMode: smtpSecurityMode,
      authentication: smtpAuthentication,
      username: nullableString(smtp.username),
      fromAddress: nullableString(smtp.fromAddress),
      fromName: nullableString(smtp.fromName),
      passwordConfigured: booleanValue(smtp.passwordConfigured),
      googleOAuthClientId: nullableString(smtp.googleOAuthClientId),
      googleOAuthClientSecretConfigured: booleanValue(smtp.googleOAuthClientSecretConfigured),
      googleOAuthRefreshTokenConfigured: booleanValue(smtp.googleOAuthRefreshTokenConfigured),
    },
    sendGrid: {
      fromEmail: nullableString(sendGrid.fromEmail),
      fromName: nullableString(sendGrid.fromName),
      apiKeyConfigured: booleanValue(sendGrid.apiKeyConfigured),
    },
  };
}

async function request(path: string, init: RequestInit = {}) {
  const cookie = await getForwardedAuthCookieHeader();
  if (!cookie)
    throw new EmailConfigurationApiError(
      "unauthorized",
      "Your session has expired. Sign in again.",
    );
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), API_TIMEOUT_MS);
  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), apiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers: { accept: "application/json", cookie, ...init.headers },
      signal: controller.signal,
    });
    if (response.status === 401 || response.status === 403)
      throw new EmailConfigurationApiError(
        "unauthorized",
        "You do not have permission to manage email configuration.",
      );
    const payload = await response.json().catch(() => null);
    if (!response.ok) {
      const message = isRecord(payload) ? stringValue(payload.title) : null;
      throw new EmailConfigurationApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        message ?? `The FIS API returned HTTP ${response.status}.`,
      );
    }
    return payload;
  } catch (error) {
    if (error instanceof EmailConfigurationApiError) throw error;
    throw new EmailConfigurationApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timer);
  }
}

export async function getEmailConfiguration() {
  const status = mapStatus(await request("api/email-configuration"));
  if (!status)
    throw new EmailConfigurationApiError(
      "invalid-response",
      "The FIS API returned an invalid email configuration status.",
    );
  return status;
}

export async function updateEmailConfiguration(input: EmailConfigurationUpdate) {
  const payload = await request("api/email-configuration", {
    method: "PUT",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(input),
  });
  if (!isRecord(payload) || typeof payload.description !== "string")
    throw new EmailConfigurationApiError(
      "invalid-response",
      "The FIS API returned an invalid configuration update.",
    );
  return payload.description;
}

export async function testEmailProvider(providerName: EmailProvider, recipientAddress: string) {
  const payload = await request(`api/email-configuration/${providerName}/test`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ recipientAddress }),
  });
  if (!isRecord(payload) || typeof payload.description !== "string")
    throw new EmailConfigurationApiError(
      "invalid-response",
      "The FIS API returned an invalid email test result.",
    );
  return payload.description;
}
