"use server";

import {
  EmailConfigurationApiError,
  getEmailConfiguration,
  testEmailProvider,
  updateEmailConfiguration,
  type EmailConfigurationStatus,
  type EmailConfigurationUpdate,
  type EmailProvider,
} from "@/lib/api/administration/api-email-configuration";
import { getSession } from "@/lib/auth/session";

type EmailConfigurationActionResult =
  { ok: true; message?: string } | { ok: false; message: string };

function canManageEmailConfiguration(session: Awaited<ReturnType<typeof getSession>>) {
  return session.status === "authenticated" && session.roles.includes("User Administration");
}

function userFacingError(error: unknown) {
  if (error instanceof EmailConfigurationApiError) return error.message;
  return "Email configuration is temporarily unavailable. Please try again.";
}

export async function getEmailConfigurationAction(): Promise<
  { ok: true; status: EmailConfigurationStatus } | { ok: false; message: string }
> {
  const session = await getSession();
  if (!canManageEmailConfiguration(session))
    return { ok: false, message: "You do not have permission to manage email configuration." };
  try {
    return { ok: true, status: await getEmailConfiguration() };
  } catch (error) {
    return { ok: false, message: userFacingError(error) };
  }
}

export async function saveEmailConfigurationAction(
  input: EmailConfigurationUpdate,
): Promise<EmailConfigurationActionResult> {
  const session = await getSession();
  if (!canManageEmailConfiguration(session))
    return { ok: false, message: "You do not have permission to manage email configuration." };
  try {
    return { ok: true, message: await updateEmailConfiguration(input) };
  } catch (error) {
    return { ok: false, message: userFacingError(error) };
  }
}

export async function testEmailProviderAction(
  provider: EmailProvider,
  recipientAddress: string,
): Promise<EmailConfigurationActionResult> {
  const session = await getSession();
  if (!canManageEmailConfiguration(session))
    return { ok: false, message: "You do not have permission to manage email configuration." };
  try {
    return { ok: true, message: await testEmailProvider(provider, recipientAddress) };
  } catch (error) {
    return { ok: false, message: userFacingError(error) };
  }
}
