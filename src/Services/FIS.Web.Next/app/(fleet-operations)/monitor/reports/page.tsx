import Link from "next/link";

import { MonitorNotice, MonitorShell } from "@/app/(fleet-operations)/monitor/_components";
import {
  accessRestricted,
  getMonitorSession,
  hasReportsAccess,
  queryValue,
  sessionMessage,
} from "@/app/(fleet-operations)/monitor/_page";

export default async function MonitorReportsPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getMonitorSession();
  const problem = sessionMessage(session, "/monitor/reports");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasReportsAccess(session))
    return accessRestricted("Your profile does not include Reports access.");
  const query = await searchParams;
  return (
    <MonitorShell
      title="Monitor Inquiry Reports Menu"
      description="Run the Monitor inquiry reports while preserving the legacy report menu."
    >
      <MonitorNotice query={query} />
      <div className="vehicle-menu-tiles">
        <section className="vehicle-menu-tile">
          <h2 className="vehicle-menu-header">One INQUIRY Reports</h2>
          <div className="vehicle-menu-body">
            <Link className="vehicle-menu-link" href="/monitor/reports/one-reference-number">
              1) Inquiry Report on ONE Reference Number
            </Link>
            <Link className="vehicle-menu-link" href="/monitor/reports/one-vehicle">
              2) Inquiry Report on ONE Vehicle
            </Link>
            <Link className="vehicle-menu-link" href="/monitor/reports/reprint">
              3) Reprint an Inquiry Report
            </Link>
          </div>
        </section>
        <section className="vehicle-menu-tile">
          <h2 className="vehicle-menu-header">Other Inquiry Reports</h2>
          <div className="vehicle-menu-body">
            <Link className="vehicle-menu-link" href="/monitor/reports/dept-site-period">
              4) Inquiry Info, for a Dept / Site, for a period
            </Link>
            <Link className="vehicle-menu-link" href="/monitor/reports/clo-inquiry">
              5) Client Liaison Officer (CLO) Inquiry Report
            </Link>
            <Link className="vehicle-menu-link" href="/monitor/reports/inquiry-statistics">
              6) Inquiry Statistics
            </Link>
            <Link className="vehicle-menu-link" href="/monitor">
              Return To Main Page
            </Link>
          </div>
        </section>
      </div>
    </MonitorShell>
  );
}
