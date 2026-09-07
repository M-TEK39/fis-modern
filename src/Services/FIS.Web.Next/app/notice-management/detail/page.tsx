import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { getQueryValue, hasNoticeManagementPermission } from "@/app/notice-management/access";
import { saveNoticeAction } from "@/app/notice-management/actions";
import NoticeDetailForm from "@/app/notice-management/notice-detail-form";
import SessionRecovery from "@/app/home/session-recovery";
import { getNotice, getNoticeSchedule, NoticeApiError, type Notice, type NoticeSchedule } from "@/lib/api-notices";
import { getSession } from "@/lib/session";

const DETAIL_ROUTES = ["/notice-management/detail", "/Admin/NoticeDetailManagement.aspx"] as const;
type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export type NoticeDetailPageProps = {
  searchParams: SearchParams;
  routePath?: (typeof DETAIL_ROUTES)[number];
};

function positiveInteger(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isSafeInteger(parsed) && parsed > 0 ? parsed : 0;
}

function dateInputValue(value: string | null | undefined, fallback: string) {
  return value?.slice(0, 10) || fallback;
}

function todayInputValue() {
  return new Date().toISOString().slice(0, 10);
}

function StatusCard({ title, message, routePath }: Readonly<{ title: string; message: string; routePath: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">{title}</p>
      <h2>{message}</h2>
      <p className="muted-copy">The application is still running. Retry when the FIS API is available.</p>
      <div className="button-row"><Link className="button button-primary" href={routePath}>Try again</Link><Link className="button button-secondary" href="/notice-management">Notice Management</Link></div>
    </section>
  );
}

function errorMessage(value: string | undefined) {
  switch (value) {
    case "unauthorized": return "Your session has expired. Sign in again before continuing.";
    case "forbidden": return "You do not have permission to manage notices.";
    case "unavailable": return "The notice service is temporarily unavailable. Please try again.";
    case "not-found": return "The selected notice no longer exists.";
    default: return value ? "The notice action could not be completed." : null;
  }
}

export default async function NoticeDetailPage({ searchParams, routePath = "/notice-management/detail" }: NoticeDetailPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><StatusCard title="API unavailable" message="Notice details could not be loaded." routePath={routePath} /></main>;
  if (!hasNoticeManagementPermission(session.accessLevel)) return <main className="page-shell vehicle-page-shell"><StatusCard title="Access restricted" message="You do not have permission to manage notices." routePath="/home" /></main>;

  const query = await searchParams;
  const requestedNoticeId = positiveInteger(getQueryValue(query.noticeid) ?? getQueryValue(query.noticeId));
  const requestedScheduleId = positiveInteger(getQueryValue(query.scheduleid) ?? getQueryValue(query.scheduleId));
  const saved = getQueryValue(query.saved) === "1";
  const queryError = errorMessage(getQueryValue(query.error));
  let notice: Notice | null = null;
  let schedule: NoticeSchedule | null = null;
  let noticeId = requestedNoticeId;
  let scheduleId = requestedScheduleId;

  try {
    if (scheduleId > 0) {
      schedule = await getNoticeSchedule(scheduleId);
      if (!schedule) return <main className="page-shell vehicle-page-shell"><StatusCard title="Notice not found" message="The selected notice schedule could not be found." routePath={routePath} /></main>;
      if (!noticeId) noticeId = schedule.noticeId;
    }
    if (noticeId > 0) {
      notice = await getNotice(noticeId);
      if (!notice) return <main className="page-shell vehicle-page-shell"><StatusCard title="Notice not found" message="The selected notice could not be found." routePath={routePath} /></main>;
    }
  } catch (error) {
    if (error instanceof NoticeApiError && error.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
    if (error instanceof NoticeApiError && error.reason === "not-found") return <main className="page-shell vehicle-page-shell"><StatusCard title="Notice not found" message="The selected notice could not be found." routePath={routePath} /></main>;
    console.error("FIS notice detail request failed", error instanceof Error ? error.message : "unknown error");
    return <main className="page-shell vehicle-page-shell"><StatusCard title="API unavailable" message="Notice details could not be loaded." routePath={routePath} /></main>;
  }

  const today = todayInputValue();
  const createdDate = dateInputValue(schedule?.createdDate, getQueryValue(query.createdate)?.replaceAll("/", "-") || today);
  const backPath = routePath === "/Admin/NoticeDetailManagement.aspx" ? "/Admin/NoticeManagement.aspx" : "/notice-management";

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="notice-detail-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Administration</p>
            <h1 id="notice-detail-title">Notice Detail Management</h1>
            <p>{notice ? "Update the notice schedule and client communication." : "Create a notice schedule and client communication."}</p>
          </div>
          <Link className="button button-secondary" href={backPath}>Notice Management</Link>
        </header>
        {queryError ? <div className="notice notice-error" role="alert"><span aria-hidden="true">!</span><span>{queryError}</span></div> : null}
        <NoticeDetailForm
          action={saveNoticeAction}
          returnPath={routePath}
          noticeId={noticeId}
          scheduleId={scheduleId}
          titleField={schedule?.titleField ?? ""}
          noticeDate={dateInputValue(notice?.noticeDate, today)}
          noticeFrom={notice?.noticeFrom ?? ""}
          noticeTitle={notice?.noticeTitle ?? ""}
          noticeBody={notice?.noticeBody ?? ""}
          noticePerson={notice?.noticePerson ?? ""}
          noticePersonTitle={notice?.noticePersonTitle ?? ""}
          scheduleStart={dateInputValue(schedule?.startDate, today)}
          scheduleEnd={dateInputValue(schedule?.endDate, today)}
          sortOrder={schedule?.sortOrder ?? 0}
          createdDate={createdDate}
          saved={saved}
        />
      </section>
    </main>
  );
}
