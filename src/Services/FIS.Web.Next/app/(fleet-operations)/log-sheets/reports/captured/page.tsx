import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";

import {
  LogsheetReportTable,
  LogsheetShell,
} from "@/app/(fleet-operations)/log-sheets/_components";
import {
  accessRestricted,
  getLogsheetSession,
  hasLogsheetAccess,
  queryValue,
  sessionMessage,
} from "@/app/(fleet-operations)/log-sheets/_page";
import {
  getLogsheets,
  LogsheetApiError,
  type LogsheetRecord,
} from "@/lib/api/fleet-operations/api-logsheets";

function dateTime(value: string, fallback: string) {
  const candidate = value.trim() || fallback;
  const parsed = new Date(candidate);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

function capturedAt(record: LogsheetRecord) {
  return dateTime(record.dateCreated ?? "", `${record.month.slice(0, 10)}T00:00:00.000Z`);
}

function filterReport(
  records: readonly LogsheetRecord[],
  query: Record<string, string | string[] | undefined>,
) {
  const report = queryValue(query.report);
  if (!report) return { records: [], error: "Choose a report and submit its criteria." };
  if (report === "date-time") {
    const day = queryValue(query.date);
    const from = dateTime(`${day}T${queryValue(query.fromTime)}`, `${day}T00:00:00`);
    const to = dateTime(`${day}T${queryValue(query.toTime)}`, `${day}T23:59:59`);
    if (!day || !from || !to || to < from)
      return { records: [], error: "Enter a valid date and time range." };
    return {
      records: records.filter((record) => {
        const captured = capturedAt(record);
        return captured !== null && captured >= from && captured <= to;
      }),
      error: null,
    };
  }
  if (report === "user") {
    const user = queryValue(query.user).toLocaleLowerCase();
    if (!user) return { records: [], error: "User name or access code is required." };
    const interval = queryValue(query.interval) === "month" ? "month" : "week";
    const start = new Date();
    start.setDate(start.getDate() - (interval === "month" ? 30 : 7));
    return {
      records: records.filter((record) => {
        const captured = capturedAt(record);
        const userMatches =
          String(record.createdByUserCode ?? "")
            .toLocaleLowerCase()
            .includes(user) || (record.requisitionNumber ?? "").toLocaleLowerCase().includes(user);
        return captured !== null && captured >= start && userMatches;
      }),
      error: null,
    };
  }
  if (report === "date") {
    const from = dateTime(queryValue(query.fromDate), "");
    const to = dateTime(`${queryValue(query.toDate)}T23:59:59`, "");
    if (!from || !to || to < from)
      return { records: [], error: "Enter a valid begin and end date." };
    return {
      records: records.filter((record) => {
        const captured = capturedAt(record);
        return captured !== null && captured >= from && captured <= to;
      }),
      error: null,
    };
  }
  return { records: [], error: "Unknown report type." };
}

async function LogsheetCapturedReportPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getLogsheetSession();
  const problem = sessionMessage(session, "/log-sheets/reports/captured");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasLogsheetAccess(session))
    return accessRestricted("Your profile does not include Reports access.");

  const query = await searchParams;
  try {
    const report = queryValue(query.report);
    const result = report
      ? filterReport(await getLogsheets(), query)
      : { records: [], error: null };
    return (
      <LogsheetShell
        title="Report: Logsheets Captured"
        description="Review logsheet capture activity by date, time, or user."
      >
        <section className="vehicle-status-maintenance-panel">
          <form className="form-grid" method="get">
            <div className="form-field form-group-full">
              <h2>Report by date and time</h2>
              <input name="report" type="hidden" value="date-time" />
              <label className="form-label" htmlFor="captured-date">
                Date
              </label>
              <input
                className="form-input"
                id="captured-date"
                name="date"
                type="date"
                defaultValue={queryValue(query.date)}
              />
              <label className="form-label" htmlFor="captured-from-time">
                From
              </label>
              <input
                className="form-input"
                id="captured-from-time"
                name="fromTime"
                type="time"
                step="1"
                defaultValue={queryValue(query.fromTime) || "00:00:00"}
              />
              <label className="form-label" htmlFor="captured-to-time">
                To
              </label>
              <input
                className="form-input"
                id="captured-to-time"
                name="toTime"
                type="time"
                step="1"
                defaultValue={queryValue(query.toTime) || "23:59:59"}
              />
              <button className="button button-primary" type="submit">
                Submit
              </button>
            </div>
          </form>
          <form className="form-grid" method="get">
            <div className="form-field form-group-full">
              <h2>Report by user</h2>
              <input name="report" type="hidden" value="user" />
              <label className="form-label" htmlFor="captured-user">
                User name or access code
              </label>
              <input
                className="form-input"
                id="captured-user"
                name="user"
                defaultValue={queryValue(query.user)}
              />
              <label className="form-label" htmlFor="captured-interval">
                Last
              </label>
              <select
                className="form-select"
                id="captured-interval"
                name="interval"
                defaultValue={queryValue(query.interval) || "week"}
              >
                <option value="week">Week</option>
                <option value="month">Month</option>
              </select>
              <button className="button button-primary" type="submit">
                Submit
              </button>
            </div>
          </form>
          <form className="form-grid" method="get">
            <div className="form-field form-group-full">
              <h2>Report by date</h2>
              <input name="report" type="hidden" value="date" />
              <label className="form-label" htmlFor="captured-from-date">
                Begin date
              </label>
              <input
                className="form-input"
                id="captured-from-date"
                name="fromDate"
                type="date"
                defaultValue={queryValue(query.fromDate)}
              />
              <label className="form-label" htmlFor="captured-to-date">
                End date
              </label>
              <input
                className="form-input"
                id="captured-to-date"
                name="toDate"
                type="date"
                defaultValue={queryValue(query.toDate)}
              />
              <button className="button button-primary" type="submit">
                Submit
              </button>
            </div>
          </form>
        </section>
        {result.error ? (
          <p className="alert alert-error" role="alert">
            {result.error}
          </p>
        ) : report ? (
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="captured-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">
                  {result.records.length} record{result.records.length === 1 ? "" : "s"}
                </p>
                <h2 id="captured-results-title">Captured logsheets</h2>
              </div>
            </div>
            <LogsheetReportTable records={result.records} />
          </section>
        ) : null}
        <div className="button-row">
          <Link className="button button-secondary" href="/log-sheets">
            Main menu
          </Link>
        </div>
      </LogsheetShell>
    );
  } catch (error) {
    return (
      <LogsheetShell
        title="Report: Logsheets Captured"
        description="Review logsheet capture activity by date, time, or user."
      >
        <section className="vehicle-status-card" role="alert">
          <h2>
            {error instanceof LogsheetApiError
              ? "The Logsheet service is temporarily unavailable."
              : "The report could not be generated."}
          </h2>
          <Link className="button button-secondary" href="/log-sheets/reports/captured">
            Try again
          </Link>
        </section>
      </LogsheetShell>
    );
  }
}

export default function LogsheetCapturedReportPage(
  props: Parameters<typeof LogsheetCapturedReportPageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <LogsheetCapturedReportPageContent {...props} />
    </Suspense>
  );
}
