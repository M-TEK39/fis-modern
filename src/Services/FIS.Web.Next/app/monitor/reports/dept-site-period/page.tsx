import Link from "next/link";

import { MonitorNotice, MonitorShell, ReportTable } from "@/app/monitor/_components";
import {
  accessRestricted,
  getMonitorSession,
  hasReportsAccess,
  queryValue,
  sessionMessage,
} from "@/app/monitor/_page";
import { getMonitors } from "@/lib/api-monitor";
import { getSites } from "@/lib/api-sites";

export default async function MonitorDeptSiteReportPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getMonitorSession();
  const problem = sessionMessage(session, "/monitor/reports/dept-site-period");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasReportsAccess(session))
    return accessRestricted("Your profile does not include Reports access.");
  const query = await searchParams;
  const siteCode = Number(queryValue(query.siteCode));
  const from = queryValue(query.from);
  const to = queryValue(query.to);
  const [sites, records] = await Promise.all([getSites(), getMonitors()]);
  const fromDate = from ? new Date(`${from}T00:00:00.000Z`) : null;
  const toDate = to ? new Date(`${to}T23:59:59.999Z`) : null;
  const rows = records
    .filter(
      (record) =>
        !record.isDeleted &&
        (!siteCode || record.driverSite === siteCode) &&
        (!fromDate || (record.captureDate && new Date(record.captureDate) >= fromDate)) &&
        (!toDate || (record.captureDate && new Date(record.captureDate) <= toDate)),
    )
    .map((record) => ({
      monitorCode: record.monitorCode,
      vmfCode: record.vmfCode,
      captureDate: record.captureDate,
      inquiryType: record.inquiryType ?? "",
      inquiryDescription: record.inquiryDescription ?? "",
      driverName: record.driverName ?? "",
      driverPersalNo: record.driverPersalNo ?? "",
      driverSite: record.driverSite,
    }));
  return (
    <MonitorShell
      title="Inquiry Info, for a Dept / Site, for a period"
      description="Filter monitor inquiries by site and capture period."
    >
      <MonitorNotice query={query} />
      <form className="vehicle-status-maintenance-panel" method="get">
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="monitor-report-site">
              Site
            </label>
            <select
              className="form-select"
              id="monitor-report-site"
              name="siteCode"
              defaultValue={siteCode > 0 ? siteCode : ""}
            >
              <option value="">All sites</option>
              {sites.map((site) => (
                <option key={site.siteCode} value={site.siteCode}>
                  {site.description || "Unnamed site"} ({site.siteCode})
                </option>
              ))}
            </select>
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="monitor-report-from">
              From date
            </label>
            <input
              className="form-input"
              id="monitor-report-from"
              name="from"
              type="date"
              defaultValue={from}
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="monitor-report-to">
              To date
            </label>
            <input
              className="form-input"
              id="monitor-report-to"
              name="to"
              type="date"
              defaultValue={to}
            />
          </div>
        </div>
        <button className="button button-primary" type="submit">
          Submit
        </button>
      </form>
      <ReportTable rows={rows} />
      <Link className="button button-secondary" href="/monitor/reports">
        Report menu
      </Link>
    </MonitorShell>
  );
}
