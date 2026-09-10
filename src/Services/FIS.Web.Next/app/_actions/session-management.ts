"use server";

import {
  getSessionManagement,
  SessionManagementApiError,
  revokeSessions,
  type SessionManagementStatus,
  type SessionRevokeInput,
  type SessionRevokeScope,
} from "@/lib/api/administration/api-session-management";
import { getSession } from "@/lib/auth/session";

type SessionManagementActionResult =
  { ok: true; message?: string } | { ok: false; message: string };

function canManageSessions(session: Awaited<ReturnType<typeof getSession>>) {
  return session.status === "authenticated" && session.roles.includes("User Administration");
}

function userFacingError(error: unknown) {
  if (error instanceof SessionManagementApiError) return error.message;
  return "Session management is temporarily unavailable. Please try again.";
}

function isScope(value: unknown): value is SessionRevokeScope {
  return value === "current" || value === "user" || value === "all";
}

function validateRevokeInput(value: unknown): value is SessionRevokeInput {
  if (typeof value !== "object" || value === null || Array.isArray(value)) return false;
  const input = value as { scope?: unknown; userAccessCode?: unknown };
  if (!isScope(input.scope)) return false;
  if (input.scope === "user")
    return (
      typeof input.userAccessCode === "number" &&
      Number.isInteger(input.userAccessCode) &&
      input.userAccessCode > 0
    );
  return input.userAccessCode === undefined;
}

export async function getSessionManagementAction(): Promise<
  { ok: true; status: SessionManagementStatus } | { ok: false; message: string }
> {
  const session = await getSession();
  if (!canManageSessions(session))
    return { ok: false, message: "You do not have permission to manage sessions." };

  try {
    return { ok: true, status: await getSessionManagement() };
  } catch (error) {
    return { ok: false, message: userFacingError(error) };
  }
}

export async function revokeSessionsAction(
  input: SessionRevokeInput,
): Promise<SessionManagementActionResult> {
  const session = await getSession();
  if (!canManageSessions(session))
    return { ok: false, message: "You do not have permission to manage sessions." };
  if (!validateRevokeInput(input))
    return { ok: false, message: "Choose a valid session revocation scope." };

  try {
    return { ok: true, message: await revokeSessions(input) };
  } catch (error) {
    return { ok: false, message: userFacingError(error) };
  }
}
