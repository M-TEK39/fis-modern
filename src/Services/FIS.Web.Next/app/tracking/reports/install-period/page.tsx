import Link from "next/link";

import { DateRangeFields, TrackingNotice, TrackingReportTable, TrackingShell } from "@/app/tracking/_components";
import { accessRestricted, getTrackingSession, hasTrackingAccess, queryValue, reportDateRange, sessionMessage } from "@/app/tracking/_page";
import { getTrackingInstallPeriodReport, TrackingApiError } from "@/lib/api-tracking";

export default async function TrackingInstallPeriodReportPage({ searchParams }: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getTrackingSession();
  const problem = sessionMessage(session, "/tracking/reports/install-period");
  if (problem) return problem;
  if (session.status !== "authenticated") return accessRestricted("Your session could not be loaded.");
  if (!hasTrackingAccess(session)) return accessRestricted("Your profile does not include Vehicle Management access.");
  const query = await searchParams;
  const range = reportDateRange(queryValue(query.startDate), queryValue(query.endDate));
  const run = queryValue(query.run) === "1";
  try {
    const rows = run ? await getTrackingInstallPeriodReport(range.startDate, range.endDate) : [];
    return <TrackingShell title="Tracking Report for an Install Period" description="View tracker installations captured during a selected period."><TrackingNotice query={query} /><form className="vehicle-status-maintenance-panel" method="get"><DateRangeFields startDate={range.startDate} endDate={range.endDate} /><input name="run" type="hidden" value="1" /><div className="button-row"><button className="button button-primary" type="submit">Submit</button><Link className="button button-secondary" href="/tracking/reports">Report menu</Link></div></form>{run ? <TrackingReportTable records={rows} title="Tracking report for install period" /> : <p className="muted-copy">Choose a date range, then submit the report.</p>}</TrackingShell>;
  } catch (error) {
    return <TrackingShell title="Tracking Report for an Install Period" description="View tracker installations captured during a selected period."><section className="vehicle-status-card" role="alert"><h2>{error instanceof TrackingApiError && error.reason === "unauthorized" ? "Your session has expired." : "The Tracking report service is temporarily unavailable."}</h2><Link className="button button-primary" href="/tracking/reports/install-period">Try again</Link></section></TrackingShell>;
  }
}
