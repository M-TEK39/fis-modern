"use server";

import {
  getUserMessageInbox,
  markUserMessageRead,
  UserMessageApiError,
  type UserMessage,
  type UserMessageInbox,
} from "@/lib/api/administration/api-user-messages";
import { getSession } from "@/lib/auth/session";

type UserMessagesActionResult<T> = { ok: true; data: T } | { ok: false; message: string };

function userFacingError(error: unknown) {
  if (error instanceof UserMessageApiError) {
    if (error.reason === "unauthorized")
      return "Your session has expired. Sign in again to continue.";
    if (error.reason === "not-found") return "This notification is no longer available.";
    return error.message;
  }

  return "Notifications are temporarily unavailable. Please try again.";
}

async function ensureAuthenticated() {
  const session = await getSession();
  if (session.status === "unavailable") {
    return "The sign-in service is temporarily unavailable. Please try again.";
  }
  if (session.status !== "authenticated") {
    return "Your session has expired. Sign in again to continue.";
  }

  return null;
}

export async function getUserMessagesAction(): Promise<UserMessagesActionResult<UserMessageInbox>> {
  const sessionError = await ensureAuthenticated();
  if (sessionError) return { ok: false, message: sessionError };

  try {
    return { ok: true, data: await getUserMessageInbox() };
  } catch (error) {
    return { ok: false, message: userFacingError(error) };
  }
}

export async function markUserMessageReadAction(
  id: number,
): Promise<UserMessagesActionResult<UserMessage>> {
  if (!Number.isSafeInteger(id) || id <= 0) {
    return { ok: false, message: "The selected notification is invalid." };
  }

  const sessionError = await ensureAuthenticated();
  if (sessionError) return { ok: false, message: sessionError };

  try {
    return { ok: true, data: await markUserMessageRead(id) };
  } catch (error) {
    return { ok: false, message: userFacingError(error) };
  }
}
