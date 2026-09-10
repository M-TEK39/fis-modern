import Link from "next/link";

import {
  MonitorNotice,
  MonitorShell,
  ReportTable,
} from "@/app/(fleet-operations)/monitor/_components";
import {
  accessRestricted,
  getMonitorSession,
  hasReportsAccess,
  parsePositiveInteger,
  queryValue,
  sessionMessage,
} from "@/app/(fleet-operations)/monitor/_page";
import {
  getMonitorReportByReference,
  MonitorApiError,
  type MonitorReportRow,
} from "@/lib/api/fleet-operations/api-monitor";

export default async function MonitorOneReferencePage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getMonitorSession();
  const problem = sessionMessage(session, "/monitor/reports/one-reference-number");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasReportsAccess(session))
    return accessRestricted("Your profile does not include Reports access.");
  const query = await searchParams;
  const reference = parsePositiveInteger(queryValue(query.referenceNumber));
  let rows: MonitorReportRow[] = [];
  let error = "";
  if (reference && reference <= 32767) {
    try {
      rows = await getMonitorReportByReference(reference);
    } catch (caught) {
      error =
        caught instanceof MonitorApiError
          ? "The Monitor report service is temporarily unavailable."
          : "The report could not be loaded.";
    }
  }
  return (
    <MonitorShell
      title="Inquiry Report on ONE Reference Number"
      description="View a detailed inquiry report for one reference number."
    >
      <MonitorNotice query={query} />
      {error ? (
        <div className="notice notice-error" role="alert">
          {error}
        </div>
      ) : null}
      <form className="vehicle-status-maintenance-panel" method="get">
        <label className="form-label" htmlFor="monitor-one-reference">
          Reference number
        </label>
        <div className="vehicle-search-row">
          <input
            className="vehicle-search"
            id="monitor-one-reference"
            name="referenceNumber"
            defaultValue={queryValue(query.referenceNumber)}
            inputMode="numeric"
            required
          />
          <button className="button button-primary" type="submit">
            Submit
          </button>
        </div>
      </form>
      {reference ? (
        <ReportTable rows={rows} />
      ) : (
        <p className="muted-copy">Enter a reference number to view details.</p>
      )}
      <Link className="button button-secondary" href="/monitor/reports">
        Report menu
      </Link>
    </MonitorShell>
  );
}
