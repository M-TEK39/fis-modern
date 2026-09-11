import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import { deleteNoticeScheduleAction } from "@/app/(administration)/notice-management/actions";
import RouteLoading from "@/components/app-shell/route-loading";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  getQueryValue,
  hasNoticeManagementPermission,
} from "@/app/(administration)/notice-management/access";
import {
  DEFAULT_NOTICE_SCHEDULE_PAGE_SIZE,
  getNoticeSchedulesPage,
  NoticeApiError,
  type NoticeSchedule,
  type NoticeSchedulePage,
} from "@/lib/api/administration/api-notices";
import { getSession } from "@/lib/auth/session";

const LIST_ROUTES = ["/notice-management", "/Admin/NoticeManagement.aspx"] as const;

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export type NoticeManagementPageProps = {
  searchParams: SearchParams;
  routePath?: (typeof LIST_ROUTES)[number];
};

function formatDate(value: string | null) {
  return value?.slice(0, 10).replaceAll("-", "/") || "-";
}

function todayKey() {
  return new Date().toISOString().slice(0, 10);
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function listHref(
  routePath: string,
  query: Record<string, string | string[] | undefined>,
  page: number,
) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (key === "page" || value === undefined) continue;
    for (const item of Array.isArray(value) ? value : [value]) params.append(key, item);
  }
  if (page > 1) params.set("page", String(page));
  const queryString = params.toString();
  return queryString ? `${routePath}?${queryString}` : routePath;
}

function detailHref(routePath: string, schedule: NoticeSchedule) {
  const detailPath =
    routePath === "/Admin/NoticeManagement.aspx"
      ? "/Admin/NoticeDetailManagement.aspx"
      : "/notice-management/detail";
  const query = new URLSearchParams({
    noticeid: String(schedule.noticeId),
    scheduleid: String(schedule.noticeScheduleId),
  });
  if (schedule.createdDate) query.set("createdate", formatDate(schedule.createdDate));
  return `${detailPath}?${query.toString()}`;
}

function StatusCard({
  title,
  message,
  routePath,
}: Readonly<{ title: string; message: string; routePath: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">{title}</p>
      <h2>{message}</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href={routePath}>
          Try again
        </Link>
        <Link className="button button-secondary" href="/home">
          Home
        </Link>
      </div>
    </section>
  );
}

function actionMessage(value: string | undefined) {
  switch (value) {
    case "saved":
      return "Notice schedule saved successfully.";
    case "unauthorized":
      return "Your session has expired. Sign in again before continuing.";
    case "forbidden":
      return "You do not have permission to manage notices.";
    case "unavailable":
      return "The notice service is temporarily unavailable. Please try again.";
    case "not-found":
      return "The selected notice schedule no longer exists.";
    case "delete":
      return "The notice schedule could not be deleted. Please try again.";
    default:
      return value ? "The notice action could not be completed." : null;
  }
}

async function NoticeManagementContent({
  searchParams,
  routePath = "/notice-management",
}: NoticeManagementPageProps) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="API unavailable"
          message="Notice schedules could not be loaded."
          routePath={routePath}
        />
      </main>
    );
  if (!hasNoticeManagementPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="Access restricted"
          message="You do not have permission to manage notices."
          routePath="/home"
        />
      </main>
    );

  const query = await searchParams;
  const search = (getQueryValue(query.search) ?? "").trim();
  const status = getQueryValue(query.status) ?? "";
  const today = (getQueryValue(query.today) ?? todayKey()).trim();
  const requestedPage = Number.parseInt(getQueryValue(query.page) ?? "1", 10);
  const requestedPageNumber = Number.isFinite(requestedPage) ? Math.max(requestedPage, 1) : 1;
  const resultMessage = actionMessage(
    getQueryValue(query.saved) === "1" ? "saved" : getQueryValue(query.error),
  );

  let schedulePage: NoticeSchedulePage;
  try {
    schedulePage = await getNoticeSchedulesPage({
      page: requestedPageNumber,
      pageSize: DEFAULT_NOTICE_SCHEDULE_PAGE_SIZE,
      search,
      status,
      today,
    });
  } catch (error) {
    if (error instanceof NoticeApiError && error.reason === "unauthorized") {
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    }

    console.error(
      "FIS notice schedule request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="API unavailable"
          message="Notice schedules could not be loaded."
          routePath={routePath}
        />
      </main>
    );
  }

  const schedules = schedulePage.items;
  const page = schedulePage.page;
  const totalPages = schedulePage.totalPages;
  const detailPath =
    routePath === "/Admin/NoticeManagement.aspx"
      ? "/Admin/NoticeDetailManagement.aspx"
      : "/notice-management/detail";

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="notice-management-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Administration</p>
            <h1 id="notice-management-title">Notice Management</h1>
            <p>Manage the notices displayed to FIS clients.</p>
          </div>
          <div className="button-row">
            <Link className="button button-primary" href={detailPath}>
              Create new Notice
            </Link>
            <Link className="button button-secondary" href="/home">
              Home
            </Link>
            <form action={logoutAction}>
              <button className="button button-secondary" type="submit">
                Sign out
              </button>
            </form>
          </div>
        </header>

        {resultMessage ? (
          <div
            className={
              getQueryValue(query.saved) === "1" ? "notice notice-success" : "notice notice-error"
            }
            role={getQueryValue(query.saved) === "1" ? "status" : "alert"}
          >
            <span aria-hidden="true">{getQueryValue(query.saved) === "1" ? "✓" : "!"}</span>
            <span>{resultMessage}</span>
          </div>
        ) : null}

        <form className="vehicle-create-form" method="get" action={routePath}>
          <input type="hidden" name="today" value={today} />
          <div className="vehicle-create-grid">
            <div className="field">
              <label htmlFor="notice-search">Search schedules</label>
              <input
                id="notice-search"
                name="search"
                type="search"
                defaultValue={search}
                placeholder="Title, creator, dates, sort order..."
              />
            </div>
            <div className="field">
              <label htmlFor="notice-status">Status</label>
              <select id="notice-status" name="status" defaultValue={status}>
                <option value="">All notices</option>
                <option value="active">Currently active</option>
                <option value="upcoming">Upcoming notices</option>
                <option value="expired">Expired notices</option>
                <option value="sorted">Sort order captured</option>
              </select>
            </div>
          </div>
          <div className="button-row">
            <button className="button button-primary" type="submit">
              Apply filters
            </button>
            {search || status ? (
              <Link className="button button-secondary" href={routePath}>
                Clear
              </Link>
            ) : null}
          </div>
        </form>

        {schedulePage.total === 0 ? (
          <div className="vehicle-empty-state">
            <p className="eyebrow">
              {search || status ? "No matching schedules" : "No notices to display"}
            </p>
            <p>
              {search || status
                ? "Try a different search or status filter."
                : "Please select Create new Notice to create one."}
            </p>
          </div>
        ) : (
          <>
            <div className="vehicle-table-wrapper">
              <table className="vehicle-table">
                <caption className="sr-only">Notice schedule list</caption>
                <thead>
                  <tr>
                    <th scope="col">Notice title</th>
                    <th scope="col">Display start date</th>
                    <th scope="col">Display end date</th>
                    <th scope="col">Created by</th>
                    <th scope="col">Created on</th>
                    <th scope="col">Sort order</th>
                    <th scope="col">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {schedules.map((schedule) => (
                    <tr key={schedule.noticeScheduleId}>
                      <td>{valueOrDash(schedule.titleField)}</td>
                      <td>{formatDate(schedule.startDate)}</td>
                      <td>{formatDate(schedule.endDate)}</td>
                      <td>{valueOrDash(schedule.createdBy)}</td>
                      <td>{formatDate(schedule.createdDate)}</td>
                      <td>{valueOrDash(schedule.sortOrder)}</td>
                      <td>
                        <div className="button-row">
                          <Link
                            className="button button-secondary button-small"
                            href={detailHref(routePath, schedule)}
                          >
                            Edit Notice
                          </Link>
                          <form action={deleteNoticeScheduleAction}>
                            <input
                              name="scheduleId"
                              type="hidden"
                              value={schedule.noticeScheduleId}
                              readOnly
                            />
                            <input name="returnPath" type="hidden" value={routePath} readOnly />
                            <button className="button button-danger button-small" type="submit">
                              Delete
                            </button>
                          </form>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <nav className="pagination-controls" aria-label="Notice schedule pagination">
              {page <= 1 ? (
                <span className="button button-secondary" aria-disabled="true">
                  Previous
                </span>
              ) : (
                <Link
                  className="button button-secondary"
                  href={listHref(routePath, query, page - 1)}
                >
                  Previous
                </Link>
              )}
              <span aria-live="polite">
                Page {page} of {totalPages}
              </span>
              {page >= totalPages ? (
                <span className="button button-secondary" aria-disabled="true">
                  Next
                </span>
              ) : (
                <Link
                  className="button button-secondary"
                  href={listHref(routePath, query, page + 1)}
                >
                  Next
                </Link>
              )}
            </nav>
          </>
        )}
      </section>
    </main>
  );
}

type NoticeManagementRouteProps = Pick<NoticeManagementPageProps, "searchParams">;

export default function NoticeManagementRoute({
  searchParams,
}: Readonly<NoticeManagementRouteProps>) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <NoticeManagementContent searchParams={searchParams} />
    </Suspense>
  );
}

export function createLegacyNoticeManagementPage(routePath: (typeof LIST_ROUTES)[number]) {
  return function LegacyNoticeManagementPage({
    searchParams,
  }: Readonly<NoticeManagementRouteProps>) {
    return (
      <Suspense fallback={<RouteLoading />}>
        <NoticeManagementContent routePath={routePath} searchParams={searchParams} />
      </Suspense>
    );
  };
}
