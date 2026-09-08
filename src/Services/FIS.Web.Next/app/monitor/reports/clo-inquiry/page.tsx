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

export default async function MonitorCloReportPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getMonitorSession();
  const problem = sessionMessage(session, "/monitor/reports/clo-inquiry");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasReportsAccess(session))
    return accessRestricted("Your profile does not include Reports access.");
  const query = await searchParams;
  const inquiryType = queryValue(query.inquiryType).toLocaleLowerCase();
  const records = await getMonitors();
  const rows = records
    .filter(
      (record) =>
        !record.isDeleted &&
        (!inquiryType || record.inquiryType?.toLocaleLowerCase() === inquiryType),
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
      title="Client Liaison Officer (CLO) Inquiry Report"
      description="Review Monitor inquiries by inquiry type."
    >
      <MonitorNotice query={query} />
      <form className="vehicle-status-maintenance-panel" method="get">
        <label className="form-label" htmlFor="monitor-clo-type">
          Inquiry type
        </label>
        <div className="vehicle-search-row">
          <input
            className="vehicle-search"
            id="monitor-clo-type"
            name="inquiryType"
            defaultValue={queryValue(query.inquiryType)}
            placeholder="Leave blank for all types"
          />
          <button className="button button-primary" type="submit">
            Submit
          </button>
        </div>
      </form>
      <ReportTable rows={rows} />
      <Link className="button button-secondary" href="/monitor/reports">
        Report menu
      </Link>
    </MonitorShell>
  );
}
