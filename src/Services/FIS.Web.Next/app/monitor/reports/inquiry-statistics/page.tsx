import Link from "next/link";

import { MonitorNotice, MonitorShell } from "@/app/monitor/_components";
import {
  accessRestricted,
  getMonitorSession,
  hasReportsAccess,
  queryValue,
  sessionMessage,
} from "@/app/monitor/_page";
import { getInquiryStatistics, MonitorApiError } from "@/lib/api-monitor";

export default async function MonitorStatisticsPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getMonitorSession();
  const problem = sessionMessage(session, "/monitor/reports/inquiry-statistics");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasReportsAccess(session))
    return accessRestricted("Your profile does not include Reports access.");
  const query = await searchParams;
  const from = queryValue(query.from);
  const to = queryValue(query.to);
  let stats: Awaited<ReturnType<typeof getInquiryStatistics>> = [];
  let error = "";
  if (from && to) {
    if (to < from) error = "End date cannot be before start date.";
    else {
      try {
        stats = await getInquiryStatistics(from, to);
      } catch (caught) {
        error =
          caught instanceof MonitorApiError
            ? "The Monitor statistics service is temporarily unavailable."
            : "Statistics could not be loaded.";
      }
    }
  }
  return (
    <MonitorShell
      title="Inquiry Statistics"
      description="Count captured inquiries by type for a selected period."
    >
      <MonitorNotice query={query} />
      {error ? (
        <div className="notice notice-error" role="alert">
          {error}
        </div>
      ) : null}
      <form className="vehicle-status-maintenance-panel" method="get">
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="monitor-stats-from">
              From date
            </label>
            <input
              className="form-input"
              id="monitor-stats-from"
              name="from"
              type="date"
              defaultValue={from}
              required
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="monitor-stats-to">
              To date
            </label>
            <input
              className="form-input"
              id="monitor-stats-to"
              name="to"
              type="date"
              defaultValue={to}
              required
            />
          </div>
        </div>
        <button className="button button-primary" type="submit">
          Load statistics
        </button>
      </form>
      {from && to && !error ? (
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">Inquiry statistics</caption>
            <thead>
              <tr>
                <th scope="col">Inquiry type</th>
                <th scope="col">Count</th>
              </tr>
            </thead>
            <tbody>
              {stats.length ? (
                stats.map((item) => (
                  <tr key={item.inquiryType}>
                    <td>{item.inquiryType}</td>
                    <td>{item.count}</td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={2}>No statistics available.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      ) : (
        <p className="muted-copy">Choose a date range to load inquiry counts.</p>
      )}
      <Link className="button button-secondary" href="/monitor/reports">
        Report menu
      </Link>
    </MonitorShell>
  );
}
