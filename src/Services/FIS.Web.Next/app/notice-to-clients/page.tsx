import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { getActiveNotices, NoticeApiError, type Notice } from "@/lib/api-notices";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function noticeIndex(value: string | undefined, count: number) {
  const parsed = Number.parseInt(value ?? "", 10);
  if (!Number.isSafeInteger(parsed) || parsed < 1) return 0;
  return Math.min(parsed - 1, count - 1);
}

function formatNoticeDate(value: string | null) {
  if (!value || value.startsWith("0001-01-01")) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value.slice(0, 10);
  return new Intl.DateTimeFormat("en-ZA", {
    day: "2-digit",
    month: "long",
    year: "numeric",
  }).format(date);
}

function formatCreatedDate(value: string | null) {
  if (!value || value.startsWith("0001-01-01")) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value.slice(0, 10).replaceAll("-", "/");
  return new Intl.DateTimeFormat("en-ZA", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  }).format(date);
}

function NoticeBody({ value }: Readonly<{ value: string }>) {
  return <div className="notice-public-body">{value}</div>;
}

function PublicNotice({
  notice,
  index,
  total,
}: Readonly<{ notice: Notice; index: number; total: number }>) {
  const nextPath = index + 1 < total ? `/Notice_To_Clients.aspx?count=${index + 2}` : "/login";
  return (
    <article className="public-notice" aria-labelledby="public-notice-title">
      <dl className="public-notice-meta">
        <div>
          <dt>Date</dt>
          <dd>{formatNoticeDate(notice.noticeDate)}</dd>
        </div>
        <div>
          <dt>From</dt>
          <dd>{notice.noticeFrom || "-"}</dd>
        </div>
      </dl>
      <h1 id="public-notice-title">{notice.noticeTitle || "Client communication"}</h1>
      <NoticeBody value={notice.noticeBody} />
      <div className="public-notice-signoff">
        <p>Yours sincerely,</p>
        <p className="public-notice-person">{notice.noticePerson || "-"}</p>
        {notice.noticePersonTitle ? (
          <p className="public-notice-person-title">{notice.noticePersonTitle}</p>
        ) : null}
        <p className="public-notice-signature">g-Fleet Management</p>
        <p className="public-notice-created">Date: {formatCreatedDate(notice.createdDate)}</p>
      </div>
      <div className="public-notice-actions">
        <Link className="button button-primary" href={nextPath}>
          {index + 1 < total ? "View next notice" : "Login to F.I.S"}
        </Link>
      </div>
    </article>
  );
}

function ErrorCard() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Service unavailable</p>
      <h2>Client notices could not be loaded.</h2>
      <p className="muted-copy">Please try again later or continue to sign in.</p>
      <div className="button-row">
        <Link className="button button-primary" href="/Notice_To_Clients.aspx">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

export default async function NoticeToClientsPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  let notices: Notice[];
  try {
    notices = await getActiveNotices();
  } catch (error) {
    if (!(error instanceof NoticeApiError)) {
      console.error(
        "FIS public notice request failed",
        error instanceof Error ? error.message : "unknown error",
      );
    }
    return (
      <main className="public-notice-shell">
        <ErrorCard />
      </main>
    );
  }

  if (notices.length === 0) redirect("/login");

  const query = await searchParams;
  const index = noticeIndex(queryValue(query.count), notices.length);
  const notice = notices[index];

  return (
    <main className="public-notice-shell">
      <section className="public-notice-card" aria-labelledby="public-notice-heading">
        <header className="public-notice-header">
          <div className="public-notice-brand" aria-label="Fleet Information System">
            FIS
          </div>
          <div>
            <p className="public-notice-brand-name">Fleet Information System</p>
            <p className="public-notice-brand-caption">Gauteng Provincial Government</p>
          </div>
        </header>
        <div className="public-notice-heading">
          <h2 id="public-notice-heading">Client Communication</h2>
        </div>
        <PublicNotice notice={notice} index={index} total={notices.length} />
        <footer className="public-notice-footer">
          16 Boeing Road East, Bedfordview, Private Bag X1, Bedfordview 2008. Tel: +27 (0)11 372
          8600. Fax: +27 (0)86 669 6926
        </footer>
      </section>
    </main>
  );
}
