import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";

import { logoutAction } from "@/app/actions/auth";
import { deleteNoticeScheduleAction } from "@/app/notice-management/actions";
import SessionRecovery from "@/app/home/session-recovery";
import { getQueryValue, hasNoticeManagementPermission } from "@/app/notice-management/access";
import { getNoticeSchedules, NoticeApiError, type NoticeSchedule } from "@/lib/api-notices";
import { getSession } from "@/lib/session";

const PAGE_SIZE = 12;
const LIST_ROUTES = ["/notice-management", "/Admin/NoticeManagement.aspx"] as const;

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export type NoticeManagementPageProps = {
  searchParams: SearchParams;
  routePath?: (typeof LIST_ROUTES)[number];
};

function formatDate(value: string | null) {
  return value?.slice(0, 10).replaceAll("-", "/") || "-";
}

function dateKey(value: string | null) {
  return value?.slice(0, 10) || "";
}

function todayKey() {
  return new Date().toISOString().slice(0, 10);
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function matchesSearch(schedule: NoticeSchedule, search: string) {
  if (!search) return true;
  const values = [
    schedule.titleField,
    schedule.createdBy,
    formatDate(schedule.startDate),
    formatDate(schedule.endDate),
    schedule.sortOrder === null ? null : String(schedule.sortOrder),
  ];
  return values.some((value) => value?.toLocaleLowerCase().includes(search.toLocaleLowerCase()));
}

function matchesStatus(schedule: NoticeSchedule, status: string, today: string) {
  const start = dateKey(schedule.startDate);
  const end = dateKey(schedule.endDate);
  switch (status) {
    case "active":
      return Boolean(start) && start <= today && (!end || end >= today);
    case "upcoming":
      return Boolean(start) && start > today;
    case "expired":
      return Boolean(end) && end < today;
    case "sorted":
      return schedule.sortOrder !== null;
    default:
      return true;
  }
}

function listHref(routePath: string, search: string, status: string, page: number) {
  const query = new URLSearchParams();
  if (search) query.set("search", search);
  if (status) query.set("status", status);
  if (page > 1) query.set("page", String(page));
  const value = query.toString();
  return value ? `${routePath}?${value}` : routePath;
}

function detailHref(routePath: string, schedule: NoticeSchedule) {
  const detailPath = routePath === "/Admin/NoticeManagement.aspx" ? "/Admin/NoticeDetailManagement.aspx" : "/notice-management/detail";
  const query = new URLSearchParams({
    noticeid: String(schedule.noticeId),
    scheduleid: String(schedule.noticeScheduleId),
  });
  if (schedule.createdDate) query.set("createdate", formatDate(schedule.createdDate));
  return `${detailPath}?${query.toString()}`;
}

function StatusCard({ title, message, routePath }: Readonly<{ title: string; message: string; routePath: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">{title}</p>
      <h2>{message}</h2>
      <p className="muted-copy">The application is still running. Retry when the FIS API is available.</p>
      <div className="button-row">
        <Link className="button button-primary" href={routePath}>Try again</Link>
        <Link className="button button-secondary" href="/home">Home</Link>
      </div>
    </section>
  );
}

function actionMessage(value: string | undefined) {
  switch (value) {
    case "saved": return "Notice schedule saved successfully.";
    case "unauthorized": return "Your session has expired. Sign in again before continuing.";
    case "forbidden": return "You do not have permission to manage notices.";
    case "unavailable": return "The notice service is temporarily unavailable. Please try again.";
    case "not-found": return "The selected notice schedule no longer exists.";
    case "delete": return "The notice schedule could not be deleted. Please try again.";
    default: return value ? "The notice action could not be completed." : null;
  }
}

export default async function NoticeManagementPage({ searchParams, routePath = "/notice-management" }: NoticeManagementPageProps) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><StatusCard title="API unavailable" message="Notice schedules could not be loaded." routePath={routePath} /></main>;
  if (!hasNoticeManagementPermission(session.accessLevel)) return <main className="page-shell vehicle-page-shell"><StatusCard title="Access restricted" message="You do not have permission to manage notices." routePath="/home" /></main>;

  const query = await searchParams;
  const search = (getQueryValue(query.search) ?? "").trim();
  const status = getQueryValue(query.status) ?? "";
  const requestedPage = Number.parseInt(getQueryValue(query.page) ?? "1", 10);
  const requestedPageNumber = Number.isFinite(requestedPage) ? Math.max(requestedPage, 1) : 1;
  const resultMessage = actionMessage(getQueryValue(query.saved) === "1" ? "saved" : getQueryValue(query.error));

  let schedules: NoticeSchedule[];
  try {
    schedules = await getNoticeSchedules();
  } catch (error) {
    if (error instanceof NoticeApiError && error.reason === "unauthorized") {
      return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
    }

    console.error("FIS notice schedule request failed", error instanceof Error ? error.message : "unknown error");
    return <main className="page-shell vehicle-page-shell"><StatusCard title="API unavailable" message="Notice schedules could not be loaded." routePath={routePath} /></main>;
  }

  const today = todayKey();
  const filtered = schedules.filter((schedule) => matchesSearch(schedule, search)).filter((schedule) => matchesStatus(schedule, status, today));
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const page = Math.min(requestedPageNumber, totalPages);
  const pageItems = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
  const detailPath = routePath === "/Admin/NoticeManagement.aspx" ? "/Admin/NoticeDetailManagement.aspx" : "/notice-management/detail";

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
            <Link className="button button-primary" href={detailPath}>Create new Notice</Link>
            <Link className="button button-secondary" href="/home">Home</Link>
            <form action={logoutAction}><button className="button button-secondary" type="submit">Sign out</button></form>
          </div>
        </header>

        {resultMessage ? (
          <div className={getQueryValue(query.saved) === "1" ? "notice notice-success" : "notice notice-error"} role={getQueryValue(query.saved) === "1" ? "status" : "alert"}>
            <span aria-hidden="true">{getQueryValue(query.saved) === "1" ? "✓" : "!"}</span>
            <span>{resultMessage}</span>
          </div>
        ) : null}

        <form className="vehicle-create-form" method="get" action={routePath}>
          <div className="vehicle-create-grid">
            <div className="field">
              <label htmlFor="notice-search">Search schedules</label>
              <input id="notice-search" name="search" type="search" defaultValue={search} placeholder="Title, creator, dates, sort order..." />
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
          <div className="button-row"><button className="button button-primary" type="submit">Apply filters</button>{search || status ? <Link className="button button-secondary" href={routePath}>Clear</Link> : null}</div>
        </form>

        {schedules.length === 0 ? (
          <div className="vehicle-empty-state"><p className="eyebrow">No notices to display</p><p>Please select Create new Notice to create one.</p></div>
        ) : filtered.length === 0 ? (
          <div className="vehicle-empty-state"><p className="eyebrow">No matching schedules</p><p>Try a different search or status filter.</p></div>
        ) : (
          <>
            <div className="vehicle-table-wrapper">
              <table className="vehicle-table">
                <caption className="sr-only">Notice schedule list</caption>
                <thead><tr><th scope="col">Notice title</th><th scope="col">Display start date</th><th scope="col">Display end date</th><th scope="col">Created by</th><th scope="col">Created on</th><th scope="col">Sort order</th><th scope="col">Actions</th></tr></thead>
                <tbody>{pageItems.map((schedule) => <tr key={schedule.noticeScheduleId}><td>{valueOrDash(schedule.titleField)}</td><td>{formatDate(schedule.startDate)}</td><td>{formatDate(schedule.endDate)}</td><td>{valueOrDash(schedule.createdBy)}</td><td>{formatDate(schedule.createdDate)}</td><td>{valueOrDash(schedule.sortOrder)}</td><td><div className="button-row"><Link className="button button-secondary button-small" href={detailHref(routePath, schedule)}>Edit Notice</Link><form action={deleteNoticeScheduleAction}><input name="scheduleId" type="hidden" value={schedule.noticeScheduleId} readOnly /><input name="returnPath" type="hidden" value={routePath} readOnly /><button className="button button-danger button-small" type="submit">Delete</button></form></div></td></tr>)}</tbody>
              </table>
            </div>
            <nav className="pagination-controls" aria-label="Notice schedule pagination">
              {page <= 1 ? <span className="button button-secondary" aria-disabled="true">Previous</span> : <Link className="button button-secondary" href={listHref(routePath, search, status, page - 1)}>Previous</Link>}
              <span aria-live="polite">Page {page} of {totalPages}</span>
              {page >= totalPages ? <span className="button button-secondary" aria-disabled="true">Next</span> : <Link className="button button-secondary" href={listHref(routePath, search, status, page + 1)}>Next</Link>}
            </nav>
          </>
        )}
      </section>
    </main>
  );
}
