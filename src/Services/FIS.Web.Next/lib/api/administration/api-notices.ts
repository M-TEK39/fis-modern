import "server-only";

import { getForwardedAuthCookieHeader } from "@/lib/auth/api-auth";

const API_TIMEOUT_MS = 8_000;
export const DEFAULT_NOTICE_SCHEDULE_PAGE_SIZE = 24;
type JsonRecord = Record<string, unknown>;

export type NoticeSchedule = {
  noticeScheduleId: number;
  noticeId: number;
  titleField: string;
  startDate: string | null;
  endDate: string | null;
  createdBy: string | null;
  createdDate: string | null;
  sortOrder: number | null;
};

export type Notice = {
  noticeId: number;
  noticeDate: string | null;
  createdDate: string | null;
  noticeFrom: string;
  noticeTitle: string;
  noticeBody: string;
  noticePerson: string;
  noticePersonTitle: string;
};

export type NoticeInput = Omit<Notice, "noticeId" | "createdDate">;

export type NoticeScheduleInput = Omit<
  NoticeSchedule,
  "noticeScheduleId" | "createdBy" | "createdDate"
>;

export type NoticeSchedulePage = {
  items: NoticeSchedule[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
};

export type NoticeApiErrorReason =
  "unauthorized" | "unavailable" | "invalid-response" | "not-found" | "conflict";

export class NoticeApiError extends Error {
  constructor(
    public readonly reason: NoticeApiErrorReason,
    message: string,
  ) {
    super(message);
    this.name = "NoticeApiError";
  }
}

function getApiBaseUrl() {
  const value = process.env.API_BASE_URL?.trim() || "http://localhost:5010";
  return `${value.replace(/\/$/, "")}/`;
}

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function getValue(record: JsonRecord, ...keys: string[]) {
  for (const key of keys) {
    if (key in record) return record[key];
  }

  return undefined;
}

function asString(value: unknown) {
  if (typeof value === "string") return value;
  if (typeof value === "number" || typeof value === "bigint") return String(value);
  return "";
}

function asNullableString(value: unknown) {
  const result = asString(value).trim();
  return result || null;
}

function asNumber(value: unknown) {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  if (typeof value === "string" && value.trim()) {
    const result = Number(value);
    return Number.isFinite(result) ? result : null;
  }

  return null;
}

function collection(value: unknown) {
  if (Array.isArray(value)) return value;
  if (isRecord(value)) {
    const items = getValue(value, "items", "data", "results");
    return Array.isArray(items) ? items : [];
  }

  return [];
}

function readPageMetadata(payload: JsonRecord) {
  const page = asNumber(getValue(payload, "page", "Page"));
  const pageSize = asNumber(getValue(payload, "pageSize", "PageSize", "page_size"));
  const total = asNumber(getValue(payload, "total", "Total"));
  const totalPages = asNumber(getValue(payload, "totalPages", "TotalPages", "total_pages"));

  if (
    page === null ||
    pageSize === null ||
    total === null ||
    totalPages === null ||
    !Number.isInteger(page) ||
    !Number.isInteger(pageSize) ||
    !Number.isInteger(total) ||
    !Number.isInteger(totalPages) ||
    page < 1 ||
    pageSize < 1 ||
    total < 0 ||
    totalPages < 1
  ) {
    return null;
  }

  return { page, pageSize, total, totalPages };
}

function normalizePage(value: number | undefined) {
  return Number.isInteger(value) && (value ?? 0) > 0 ? (value ?? 1) : 1;
}

function normalizePageSize(value: number | undefined) {
  const pageSize =
    Number.isInteger(value) && (value ?? 0) > 0
      ? (value ?? DEFAULT_NOTICE_SCHEDULE_PAGE_SIZE)
      : DEFAULT_NOTICE_SCHEDULE_PAGE_SIZE;
  return Math.min(100, pageSize);
}

function mapSchedule(value: unknown): NoticeSchedule | null {
  if (!isRecord(value)) return null;

  const noticeScheduleId = asNumber(
    getValue(value, "noticeScheduleId", "NoticeScheduleId", "notice_schedule_id"),
  );
  const noticeId = asNumber(getValue(value, "noticeId", "NoticeId", "notice_id"));
  if (noticeScheduleId === null || noticeId === null) return null;

  return {
    noticeScheduleId,
    noticeId,
    titleField: asString(getValue(value, "titleField", "TitleField", "title_field")),
    startDate: asNullableString(getValue(value, "startDate", "StartDate", "start_date")),
    endDate: asNullableString(getValue(value, "endDate", "EndDate", "end_date")),
    createdBy: asNullableString(getValue(value, "createdBy", "CreatedBy", "created_by")),
    createdDate: asNullableString(
      getValue(value, "createdDate", "CreatedDate", "date_created", "created_date"),
    ),
    sortOrder: asNumber(getValue(value, "sortOrder", "SortOrder", "sort_order")),
  };
}

function mapNotice(value: unknown): Notice | null {
  if (!isRecord(value)) return null;

  const noticeId = asNumber(getValue(value, "noticeId", "NoticeId", "notice_id"));
  if (noticeId === null) return null;

  return {
    noticeId,
    noticeDate: asNullableString(getValue(value, "noticeDate", "NoticeDate", "notice_date")),
    createdDate: asNullableString(
      getValue(value, "createdDate", "CreatedDate", "date_created", "created_date"),
    ),
    noticeFrom: asString(getValue(value, "noticeFrom", "NoticeFrom", "notice_from")),
    noticeTitle: asString(getValue(value, "noticeTitle", "NoticeTitle", "notice_title")),
    noticeBody: asString(getValue(value, "noticeBody", "NoticeBody", "notice_body")),
    noticePerson: asString(getValue(value, "noticePerson", "NoticePerson", "notice_person")),
    noticePersonTitle: asString(
      getValue(value, "noticePersonTitle", "NoticePersonTitle", "notice_person_title"),
    ),
  };
}

async function errorMessage(response: Response, fallback: string) {
  try {
    const payload = (await response.json()) as unknown;
    if (isRecord(payload)) return asString(getValue(payload, "message", "error")) || fallback;
    return typeof payload === "string" && payload.trim() ? payload.trim() : fallback;
  } catch {
    return fallback;
  }
}

async function requestApi(path: string, init: RequestInit = {}, forwardAuthCookie = true) {
  const cookieHeader = forwardAuthCookie ? await getForwardedAuthCookieHeader() : null;
  if (forwardAuthCookie && !cookieHeader)
    throw new NoticeApiError("unauthorized", "No FIS access cookie is available.");

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), API_TIMEOUT_MS);

  try {
    const response = await fetch(new URL(path.replace(/^\//, ""), getApiBaseUrl()), {
      ...init,
      cache: "no-store",
      headers: {
        accept: "application/json",
        ...(init.body ? { "content-type": "application/json" } : {}),
        ...init.headers,
        ...(cookieHeader ? { cookie: cookieHeader } : {}),
      },
      signal: controller.signal,
    });

    if (response.status === 401 || response.status === 403) {
      throw new NoticeApiError("unauthorized", "The FIS access cookie was rejected.");
    }

    if (response.status === 404) {
      throw new NoticeApiError("not-found", "The requested notice was not found.");
    }

    if (response.status === 409) {
      throw new NoticeApiError(
        "conflict",
        await errorMessage(response, "The notice could not be saved."),
      );
    }

    if (!response.ok) {
      throw new NoticeApiError(
        response.status >= 500 ? "unavailable" : "invalid-response",
        await errorMessage(response, `FIS API returned HTTP ${response.status}.`),
      );
    }

    return response;
  } catch (error) {
    if (error instanceof NoticeApiError) throw error;
    throw new NoticeApiError("unavailable", "The FIS API could not be reached.");
  } finally {
    clearTimeout(timeout);
  }
}

async function readJson(response: Response) {
  try {
    return (await response.json()) as unknown;
  } catch {
    throw new NoticeApiError("invalid-response", "The FIS API returned invalid notice data.");
  }
}

export async function getNoticeSchedules() {
  const payload = await readJson(await requestApi("api/notice-management/notice-schedules"));
  return collection(payload)
    .map(mapSchedule)
    .filter((item): item is NoticeSchedule => item !== null);
}

export async function getNoticeSchedulesPage(
  options: {
    page?: number;
    pageSize?: number;
    search?: string;
    status?: string;
    today?: string;
  } = {},
): Promise<NoticeSchedulePage> {
  const params = new URLSearchParams({
    page: String(normalizePage(options.page)),
    pageSize: String(normalizePageSize(options.pageSize)),
  });
  const search = options.search?.trim();
  if (search) params.set("search", search);
  const status = options.status?.trim();
  if (status) params.set("status", status);
  const today = options.today?.trim();
  if (today) params.set("today", today);

  const payload = await readJson(
    await requestApi(`api/notice-management/notice-schedules/page?${params.toString()}`),
  );
  if (!isRecord(payload) || !Array.isArray(payload.items)) {
    throw new NoticeApiError(
      "invalid-response",
      "The FIS API returned an invalid notice schedule page.",
    );
  }

  const metadata = readPageMetadata(payload);
  if (!metadata) {
    throw new NoticeApiError(
      "invalid-response",
      "The FIS API returned incomplete notice schedule pagination metadata.",
    );
  }

  return {
    items: payload.items.map(mapSchedule).filter((item): item is NoticeSchedule => item !== null),
    ...metadata,
  };
}

export async function getActiveNotices() {
  const payload = await readJson(await requestApi("api/notices/active", {}, false));
  return collection(payload)
    .map(mapNotice)
    .filter((item): item is Notice => item !== null);
}

export async function getNoticeSchedule(noticeScheduleId: number) {
  const payload = await readJson(
    await requestApi(`api/notice-management/notice-schedules/${noticeScheduleId}`),
  );
  return mapSchedule(payload);
}

export async function getNotice(noticeId: number) {
  const payload = await readJson(await requestApi(`api/notice-management/notices/${noticeId}`));
  return mapNotice(payload);
}

export async function createNotice(input: NoticeInput) {
  const payload = await readJson(
    await requestApi("api/notice-management/notices", {
      method: "POST",
      body: JSON.stringify({
        noticeDate: input.noticeDate,
        noticeFrom: input.noticeFrom,
        noticeTitle: input.noticeTitle,
        noticeBody: input.noticeBody,
        noticePerson: input.noticePerson,
        noticePersonTitle: input.noticePersonTitle,
      }),
    }),
  );
  const notice = mapNotice(payload);
  if (!notice)
    throw new NoticeApiError("invalid-response", "The FIS API returned an invalid created notice.");
  return notice;
}

export async function updateNotice(noticeId: number, input: NoticeInput) {
  const payload = await readJson(
    await requestApi(`api/notice-management/notices/${noticeId}`, {
      method: "PUT",
      body: JSON.stringify({
        noticeId,
        noticeDate: input.noticeDate,
        noticeFrom: input.noticeFrom,
        noticeTitle: input.noticeTitle,
        noticeBody: input.noticeBody,
        noticePerson: input.noticePerson,
        noticePersonTitle: input.noticePersonTitle,
      }),
    }),
  );
  const notice = mapNotice(payload);
  if (!notice)
    throw new NoticeApiError("invalid-response", "The FIS API returned an invalid updated notice.");
  return notice;
}

export async function createNoticeSchedule(input: NoticeScheduleInput) {
  const payload = await readJson(
    await requestApi("api/notice-management/notice-schedules", {
      method: "POST",
      body: JSON.stringify({
        noticeId: input.noticeId,
        titleField: input.titleField,
        startDate: input.startDate,
        endDate: input.endDate,
        sortOrder: input.sortOrder,
      }),
    }),
  );
  const schedule = mapSchedule(payload);
  if (!schedule)
    throw new NoticeApiError(
      "invalid-response",
      "The FIS API returned an invalid created schedule.",
    );
  return schedule;
}

export async function updateNoticeSchedule(noticeScheduleId: number, input: NoticeScheduleInput) {
  const payload = await readJson(
    await requestApi(`api/notice-management/notice-schedules/${noticeScheduleId}`, {
      method: "PUT",
      body: JSON.stringify({
        noticeScheduleId,
        noticeId: input.noticeId,
        titleField: input.titleField,
        startDate: input.startDate,
        endDate: input.endDate,
        sortOrder: input.sortOrder,
      }),
    }),
  );
  const schedule = mapSchedule(payload);
  if (!schedule)
    throw new NoticeApiError(
      "invalid-response",
      "The FIS API returned an invalid updated schedule.",
    );
  return schedule;
}

export async function deleteNoticeSchedule(noticeScheduleId: number) {
  await requestApi(`api/notice-management/notice-schedules/${noticeScheduleId}`, {
    method: "DELETE",
  });
}
