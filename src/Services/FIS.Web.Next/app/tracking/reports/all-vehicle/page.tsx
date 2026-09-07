import Link from "next/link";

import { DateRangeFields, TrackingNotice, TrackingReportTable, TrackingShell } from "@/app/tracking/_components";
import { accessRestricted, getTrackingSession, hasTrackingAccess, queryValue, reportDateRange, sessionMessage } from "@/app/tracking/_page";
import { getTrackingAllVehiclesReport, TrackingApiError } from "@/lib/api-tracking";

export default async function TrackingAllVehicleReportPage({ searchParams }: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getTrackingSession();
  const problem = sessionMessage(session, "/tracking/reports/all-vehicle");
  if (problem) return problem;
  if (session.status !== "authenticated") return accessRestricted("Your session could not be loaded.");
  if (!hasTrackingAccess(session)) return accessRestricted("Your profile does not include Vehicle Management access.");
  const query = await searchParams;
  const trackerType = queryValue(query.trackerType) || "All";
  const range = reportDateRange(queryValue(query.startDate), queryValue(query.endDate));
  const run = queryValue(query.run) === "1";
  try {
    const rows = run ? await getTrackingAllVehiclesReport(trackerType, range.startDate, range.endDate) : [];
    return <TrackingShell title="Tracking Report for ALL Vehicle" description="View tracking records across all vehicles."><TrackingNotice query={query} /><form className="vehicle-status-maintenance-panel" method="get"><div className="form-field"><span className="form-label">Tracker type</span><div className="contract-search-radio"><label className="form-radio-label"><input type="radio" name="trackerType" value="New" defaultChecked={trackerType === "New"} /> New</label><label className="form-radio-label"><input type="radio" name="trackerType" value="Reused" defaultChecked={trackerType === "Reused"} /> Re-used</label><label className="form-radio-label"><input type="radio" name="trackerType" value="All" defaultChecked={trackerType === "All"} /> All</label></div></div><DateRangeFields startDate={range.startDate} endDate={range.endDate} /><input name="run" type="hidden" value="1" /><div className="button-row"><button className="button button-primary" type="submit">Submit</button><Link className="button button-secondary" href="/tracking/reports">Report menu</Link></div></form>{run ? <TrackingReportTable records={rows} title="Tracking report for all vehicles" /> : <p className="muted-copy">Choose a tracker type and date range, then submit the report.</p>}</TrackingShell>;
  } catch (error) {
    return <TrackingShell title="Tracking Report for ALL Vehicle" description="View tracking records across all vehicles."><section className="vehicle-status-card" role="alert"><h2>{error instanceof TrackingApiError && error.reason === "unauthorized" ? "Your session has expired." : "The Tracking report service is temporarily unavailable."}</h2><Link className="button button-primary" href="/tracking/reports/all-vehicle">Try again</Link></section></TrackingShell>;
  }
}
