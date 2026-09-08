"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  createNotifyList,
  deleteNotifyList,
  NotifyListApiError,
  updateNotifyList,
} from "@/lib/api-notify-list";
import { getSession } from "@/lib/session";

const NOTIFY_LIST_PATH = "/call-centre/notifications";
const CALL_CENTRE_ROLE = "Call Centre";

function getText(formData: FormData, ...keys: string[]) {
  for (const key of keys) {
    const value = formData.get(key);
    if (typeof value === "string") {
      return value.trim();
    }
  }

  return "";
}

function redirectWithError(message: string, searchTerm = ""): never {
  const params = new URLSearchParams({ error: message });
  if (searchTerm) {
    params.set("q", searchTerm);
  }

  redirect(`${NOTIFY_LIST_PATH}?${params.toString()}`);
}

async function authorizeCallCentre(searchTerm: string) {
  const session = await getSession();
  if (session.status === "unavailable") {
    redirectWithError(
      "The sign-in service is temporarily unavailable. Please try again.",
      searchTerm,
    );
  }

  if (session.status !== "authenticated") {
    redirectWithError("Your session has expired. Sign in again before continuing.", searchTerm);
  }

  if (
    !session.roles.some(
      (role) => role.localeCompare(CALL_CENTRE_ROLE, undefined, { sensitivity: "accent" }) === 0,
    )
  ) {
    redirectWithError("You do not have permission to maintain notification sections.", searchTerm);
  }
}

function getCode(formData: FormData) {
  const rawCode = getText(formData, "code", "Acode");
  if (!rawCode) {
    return null;
  }

  const code = Number(rawCode);
  if (!Number.isInteger(code) || code <= 0) {
    throw new Error("The notification section is invalid.");
  }

  return code;
}

function getRequest(formData: FormData) {
  const description = getText(formData, "description", "Aname");
  const email = getText(formData, "email", "Aemail1");

  if (!description) {
    throw new Error("Notification section name is required.");
  }

  if (description.length > 40) {
    throw new Error("Notification section name must be 40 characters or fewer.");
  }

  if (email.length > 240) {
    throw new Error("Email address must be 240 characters or fewer.");
  }

  return {
    Notify_list_desc: description,
    Notify_email1: email || null,
  };
}

function apiErrorMessage(error: unknown) {
  if (error instanceof NotifyListApiError) {
    if (error.reason === "unauthorized") {
      return "Your session has expired. Sign in again before continuing.";
    }

    if (error.reason === "not-found") {
      return "The notification section was not found.";
    }

    if (error.reason === "unavailable") {
      return "The notification service is temporarily unavailable. Please try again.";
    }
  }

  return "Notification section operation failed. Please try again.";
}

export async function saveNotifyListAction(formData: FormData) {
  const searchTerm = getText(formData, "search", "q", "xName");
  await authorizeCallCentre(searchTerm);

  let code: number | null = null;
  try {
    const request = getRequest(formData);
    code = getCode(formData);
    if (code === null) {
      await createNotifyList(request);
    } else {
      await updateNotifyList(code, request);
    }
  } catch (error) {
    if (error instanceof Error && !(error instanceof NotifyListApiError)) {
      redirectWithError(error.message, searchTerm);
    }

    redirectWithError(apiErrorMessage(error), searchTerm);
  }

  revalidatePath(NOTIFY_LIST_PATH);
  const params = new URLSearchParams({ [code === null ? "saved" : "updated"]: "1" });
  if (searchTerm) {
    params.set("q", searchTerm);
  }

  redirect(`${NOTIFY_LIST_PATH}?${params.toString()}`);
}

export async function deleteNotifyListAction(formData: FormData) {
  const searchTerm = getText(formData, "search", "q", "xName");
  await authorizeCallCentre(searchTerm);

  const code = getCode(formData);
  if (code === null) {
    redirectWithError("The notification section is invalid.", searchTerm);
  }

  try {
    await deleteNotifyList(code);
  } catch (error) {
    redirectWithError(apiErrorMessage(error), searchTerm);
  }

  revalidatePath(NOTIFY_LIST_PATH);
  const params = new URLSearchParams({ deleted: "1" });
  if (searchTerm) {
    params.set("q", searchTerm);
  }

  redirect(`${NOTIFY_LIST_PATH}?${params.toString()}`);
}
