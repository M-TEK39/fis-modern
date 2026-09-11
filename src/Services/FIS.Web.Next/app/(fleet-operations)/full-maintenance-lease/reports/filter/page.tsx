import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import {
  AccessRestricted,
  ApiUnavailable,
  FmlFrame,
  hasFmlPermission,
} from "@/app/(fleet-operations)/full-maintenance-lease/_components";
import { getSession } from "@/lib/auth/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function reportTitle(report: string) {
  return report === "maintenance-history"
    ? "FML Vehicle Maintenance History"
    : report === "over-utilized"
      ? "Over-utilized FML Vehicles"
      : "FML Report Filter";
}

async function FmlReportFilterPageContent({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/full-maintenance-lease/reports/filter" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable message="The FML report filter could not be opened." />
      </main>
    );
  if (!hasFmlPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );

  const query = await searchParams;
  const report = first(query.report) ?? "";
  const title = reportTitle(report);
  const target =
    report === "maintenance-history"
      ? "/full-maintenance-lease/reports/maintenance-history"
      : report === "over-utilized"
        ? "/full-maintenance-lease/reports/over-utilized"
        : "/full-maintenance-lease/reports";

  return (
    <FmlFrame
      title={title}
      description="Choose the date range, financial year, or vehicle number used by the legacy report."
    >
      {target === "/full-maintenance-lease/reports" ? (
        <section className="vehicle-status-card" role="alert">
          <h2>Report filter not recognized</h2>
          <p className="muted-copy">Return to the FML reports menu and choose a report.</p>
        </section>
      ) : (
        <form action={target} method="get" className="form-card">
          <div className="form-card-header">
            <h2>Report filters</h2>
            <p>Leave a field blank when it should not limit the report.</p>
          </div>
          <div className="form-card-body">
            <div className="form-grid">
              <div className="form-field">
                <label className="form-label" htmlFor="fml-report-start">
                  Start date
                </label>
                <input className="form-input" id="fml-report-start" name="startDate" type="date" />
              </div>
              <div className="form-field">
                <label className="form-label" htmlFor="fml-report-end">
                  End date
                </label>
                <input className="form-input" id="fml-report-end" name="endDate" type="date" />
              </div>
              <div className="form-field">
                <label className="form-label" htmlFor="fml-report-year">
                  Financial year
                </label>
                <input
                  className="form-input"
                  id="fml-report-year"
                  name="finYear"
                  inputMode="numeric"
                  placeholder="e.g. 2025"
                />
              </div>
              <div className="form-field">
                <label className="form-label" htmlFor="fml-report-vehicle">
                  GG or GP number
                </label>
                <input
                  className="form-input"
                  id="fml-report-vehicle"
                  name="ggNum"
                  placeholder="Vehicle number"
                />
              </div>
            </div>
            <div className="button-row">
              <button className="button button-primary" type="submit">
                View report
              </button>
              <Link className="button button-secondary" href="/full-maintenance-lease/reports">
                Back
              </Link>
            </div>
          </div>
        </form>
      )}
    </FmlFrame>
  );
}

export default function FmlReportFilterPage(props: Readonly<{ searchParams: SearchParams }>) {
  return (
    <StreamedRoute>
      <FmlReportFilterPageContent {...props} />
    </StreamedRoute>
  );
}
