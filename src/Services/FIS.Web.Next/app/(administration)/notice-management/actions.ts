"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  createNotice,
  createNoticeSchedule,
  deleteNoticeSchedule,
  NoticeApiError,
  type Notice,
  type NoticeSchedule,
  updateNotice,
  updateNoticeSchedule,
} from "@/lib/api/administration/api-notices";
import { getSession } from "@/lib/auth/session";

const NOTICE_MANAGEMENT_PERMISSION = 4;
const RETURN_PATHS = [
  "/notice-management",
  "/Admin/NoticeManagement.aspx",
  "/notice-management/detail",
  "/Admin/NoticeDetailManagement.aspx",
] as const;

export type NoticeActionState = {
  status: "idle" | "error";
  message?: string;
};

const initialState: NoticeActionState = { status: "idle" };

function getText(formData: FormData, name: string) {
  const value = formData.get(name);
  return typeof value === "string" ? value.trim() : "";
}

function getOptionalDate(formData: FormData, name: string) {
  const value = getText(formData, name);
  if (!value) return null;
  return /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : null;
}

function getPositiveInteger(formData: FormData, name: string) {
  const value = getText(formData, name);
  if (!value) return 0;
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}

function getSortOrder(formData: FormData) {
  const value = getText(formData, "sortOrder");
  if (!value) return 0;
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) ? parsed : null;
}

function hasNoticeManagementPermission(accessLevel?: string) {
  if (!accessLevel) return false;

  try {
    return (
      (BigInt(accessLevel) & BigInt(NOTICE_MANAGEMENT_PERMISSION)) ===
      BigInt(NOTICE_MANAGEMENT_PERMISSION)
    );
  } catch {
    return false;
  }
}

function returnPath(value: string) {
  return RETURN_PATHS.find((path) => path.toLowerCase() === value.toLowerCase()) ?? RETURN_PATHS[2];
}

function errorState(message: string): NoticeActionState {
  return { status: "error", message };
}

function apiErrorMessage(error: NoticeApiError, subject: string) {
  if (error.reason === "unauthorized")
    return "Your session has expired. Sign in again before continuing.";
  if (error.reason === "not-found")
    return `The selected ${subject} no longer exists. Reload the page and try again.`;
  if (error.reason === "unavailable")
    return `The notice ${subject} is temporarily unavailable. Please try again.`;
  return error.message;
}

export async function saveNoticeAction(
  _previousState: NoticeActionState = initialState,
  formData: FormData,
): Promise<NoticeActionState> {
  const noticeId = getPositiveInteger(formData, "noticeId");
  const scheduleId = getPositiveInteger(formData, "scheduleId");
  const noticeFrom = getText(formData, "noticeFrom");
  const noticeTitle = getText(formData, "noticeTitle");
  const noticePerson = getText(formData, "noticePerson");
  const noticeDate = getOptionalDate(formData, "noticeDate");
  const scheduleStart = getOptionalDate(formData, "scheduleStart");
  const scheduleEnd = getOptionalDate(formData, "scheduleEnd");
  const sortOrder = getSortOrder(formData);

  if (noticeId === null || scheduleId === null)
    return errorState("The selected notice is invalid.");
  if (!noticeFrom || !noticeTitle || !noticePerson) {
    return errorState("Please complete the required notice fields before saving.");
  }
  if (sortOrder === null) return errorState("Display order must be a whole number.");
  if (scheduleStart && scheduleEnd && scheduleEnd < scheduleStart) {
    return errorState("Ending display date cannot be before the starting display date.");
  }

  const session = await getSession();
  if (session.status === "unavailable")
    return errorState("The sign-in service is temporarily unavailable. Please try again.");
  if (session.status !== "authenticated")
    return errorState("Your session has expired. Sign in again before continuing.");
  if (!hasNoticeManagementPermission(session.accessLevel))
    return errorState("You do not have permission to manage notices.");

  let savedNotice: Notice;
  let savedSchedule: NoticeSchedule;
  try {
    savedNotice =
      noticeId > 0
        ? await updateNotice(noticeId, {
            noticeDate,
            noticeFrom,
            noticeTitle,
            noticeBody: getText(formData, "noticeBody"),
            noticePerson,
            noticePersonTitle: getText(formData, "noticePersonTitle"),
          })
        : await createNotice({
            noticeDate,
            noticeFrom,
            noticeTitle,
            noticeBody: getText(formData, "noticeBody"),
            noticePerson,
            noticePersonTitle: getText(formData, "noticePersonTitle"),
          });

    const scheduleInput = {
      noticeId: savedNotice.noticeId,
      titleField: getText(formData, "titleField"),
      startDate: scheduleStart,
      endDate: scheduleEnd,
      sortOrder,
    };
    savedSchedule =
      scheduleId > 0
        ? await updateNoticeSchedule(scheduleId, scheduleInput)
        : await createNoticeSchedule(scheduleInput);
  } catch (error) {
    if (error instanceof NoticeApiError) return errorState(apiErrorMessage(error, "record"));
    console.error(
      "FIS notice save failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return errorState("The notice could not be saved. Please try again.");
  }

  const destination = returnPath(getText(formData, "returnPath"));
  const query = new URLSearchParams({
    noticeid: String(savedNotice.noticeId),
    scheduleid: String(savedSchedule.noticeScheduleId),
    saved: "1",
  });
  revalidatePath("/notice-management");
  revalidatePath("/Admin/NoticeManagement.aspx");
  redirect(`${destination}?${query.toString()}`);
}

export async function deleteNoticeScheduleAction(formData: FormData) {
  const scheduleId = getPositiveInteger(formData, "scheduleId");
  const destination = returnPath(getText(formData, "returnPath"));
  const query = new URLSearchParams({ error: "delete" });

  if (!scheduleId) redirect(`${destination}?${query.toString()}`);

  const session = await getSession();
  if (session.status !== "authenticated" || !hasNoticeManagementPermission(session.accessLevel)) {
    query.set("error", session.status === "authenticated" ? "forbidden" : "session");
    redirect(`${destination}?${query.toString()}`);
  }

  try {
    await deleteNoticeSchedule(scheduleId);
    query.delete("error");
    query.set("saved", "1");
  } catch (error) {
    if (error instanceof NoticeApiError) query.set("error", error.reason);
    else {
      console.error(
        "FIS notice schedule delete failed",
        error instanceof Error ? error.message : "unknown error",
      );
      query.set("error", "delete");
    }
  }

  redirect(`${destination}?${query.toString()}`);
}
