import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { AccessRestricted, hasReportsRole, ReportsFrame } from "@/app/reports/_components";
import { getSession } from "@/lib/session";

import { submitAdditionalReportRequestAction } from "./actions";

type RequestPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function queryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] ?? "" : value ?? "";
}

export default async function ReportRequestPage({ searchParams }: RequestPageProps) {
  await connection();
  const [session, query] = await Promise.all([getSession(), searchParams]);
  if (session.status === "anonymous") redirect("/login");
  if (session.status !== "authenticated") {
    return <ReportsFrame title="Request Additional Reports" description="Capture a report change request for implementation follow-up."><p className="notice notice-error">The sign-in service is temporarily unavailable. Please try again.</p></ReportsFrame>;
  }
  if (!hasReportsRole(session.roles)) {
    return <ReportsFrame title="Request Additional Reports" description="Legacy report access is enforced on the server."><AccessRestricted /></ReportsFrame>;
  }

  const result = queryValue(query.result);
  const message = queryValue(query.message);
  const hasError = result === "invalid" || result === "error" || result === "unavailable" || result === "forbidden";
  const hasSuccess = result === "success";

  return (
    <ReportsFrame title="Request Additional Reports" description="Capture a report change request for implementation follow-up.">
      {message ? <div className={`notice ${hasError ? "notice-error" : hasSuccess ? "notice-success" : "notice-info"}`} role="status">{message}</div> : null}
      <section className="vehicle-status-maintenance-panel">
        <div className="vehicle-page-header">
          <div><p className="eyebrow">Report request</p><h2>Report Request Form</h2></div>
          <Link className="button button-secondary" href="/reports/fis-report">FIS Report Menu</Link>
        </div>
        <form action={submitAdditionalReportRequestAction} className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="report-request-category">Request Category</label>
            <select className="form-select" id="report-request-category" name="category" defaultValue="New Report">
              <option>New Report</option><option>Change Existing Report</option><option>Access/Permission</option><option>Data Correction</option>
            </select>
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="report-request-priority">Priority</label>
            <select className="form-select" id="report-request-priority" name="priority" defaultValue="Medium">
              <option>Low</option><option>Medium</option><option>High</option><option>Critical</option>
            </select>
          </div>
          <div className="form-field"><label className="form-label" htmlFor="report-request-by">Requested By</label><input className="form-input" id="report-request-by" name="requestedBy" /></div>
          <div className="form-field"><label className="form-label" htmlFor="report-request-email">Contact Email</label><input className="form-input" id="report-request-email" name="email" type="email" /></div>
          <div className="form-field"><label className="form-label" htmlFor="report-request-subject">Subject</label><input className="form-input" id="report-request-subject" name="subject" required /></div>
          <div className="form-field"><label className="form-label" htmlFor="report-request-module">Module</label><input className="form-input" id="report-request-module" name="module" placeholder="Reports / Contracts / Vehicles / etc" /></div>
          <div className="form-field form-field-wide"><label className="form-label" htmlFor="report-request-details">Detailed Request</label><textarea className="form-textarea" id="report-request-details" name="details" rows={6} required /></div>
          <div className="button-row form-field-wide"><button className="button button-primary" type="submit">Submit Request</button><Link className="button button-secondary" href="/reports/request">Reset</Link></div>
        </form>
      </section>
    </ReportsFrame>
  );
}
