"use server";

import {
  getSystemConfiguration,
  SystemConfigurationApiError,
  updateSystemConfiguration,
  type SystemConfigurationStatus,
  type SystemConfigurationUpdate,
} from "@/lib/api/administration/api-system-configuration";
import { getSession } from "@/lib/auth/session";

const MIN_REFRESH_LIFETIME_MINUTES = 15;
const MAX_REFRESH_LIFETIME_MINUTES = 30 * 24 * 60;

type SystemConfigurationActionResult =
  { ok: true; message?: string } | { ok: false; message: string };

function canManageSystemConfiguration(session: Awaited<ReturnType<typeof getSession>>) {
  return session.status === "authenticated" && session.roles.includes("User Administration");
}

function userFacingError(error: unknown) {
  if (error instanceof SystemConfigurationApiError) return error.message;
  return "System configuration is temporarily unavailable. Please try again.";
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function isOptionalLifetime(value: unknown) {
  return (
    value === undefined ||
    (typeof value === "number" &&
      Number.isInteger(value) &&
      value >= MIN_REFRESH_LIFETIME_MINUTES &&
      value <= MAX_REFRESH_LIFETIME_MINUTES)
  );
}

function isOptionalString(value: unknown) {
  return value === undefined || typeof value === "string";
}

function isOptionalSecret(value: unknown) {
  return value === undefined || (typeof value === "string" && value.trim().length > 0);
}

function isSystemConfigurationUpdate(value: unknown): value is SystemConfigurationUpdate {
  if (!isRecord(value)) return false;
  const session = value.session;
  const authentication = value.authentication;
  if (session !== undefined) {
    if (!isRecord(session)) return false;
    if (!isOptionalLifetime(session.standardRefreshLifetimeMinutes)) return false;
    if (!isOptionalLifetime(session.rememberRefreshLifetimeMinutes)) return false;
  }
  if (authentication !== undefined) {
    if (!isRecord(authentication)) return false;
    if (
      authentication.entraEnabled !== undefined &&
      typeof authentication.entraEnabled !== "boolean"
    )
      return false;
    if (!isOptionalString(authentication.tenantId)) return false;
    if (!isOptionalString(authentication.clientId)) return false;
    if (!isOptionalSecret(authentication.clientSecret)) return false;
    if (!isOptionalSecret(authentication.passwordResetSigningKey)) return false;
  }
  return session !== undefined || authentication !== undefined;
}

export async function getSystemConfigurationAction(): Promise<
  { ok: true; status: SystemConfigurationStatus } | { ok: false; message: string }
> {
  const session = await getSession();
  if (!canManageSystemConfiguration(session))
    return { ok: false, message: "You do not have permission to manage system configuration." };

  try {
    return { ok: true, status: await getSystemConfiguration() };
  } catch (error) {
    return { ok: false, message: userFacingError(error) };
  }
}

export async function saveSystemConfigurationAction(
  input: SystemConfigurationUpdate,
): Promise<SystemConfigurationActionResult> {
  const session = await getSession();
  if (!canManageSystemConfiguration(session))
    return { ok: false, message: "You do not have permission to manage system configuration." };
  if (!isSystemConfigurationUpdate(input))
    return { ok: false, message: "Enter valid system configuration values before saving." };

  try {
    return { ok: true, message: await updateSystemConfiguration(input) };
  } catch (error) {
    return { ok: false, message: userFacingError(error) };
  }
}
