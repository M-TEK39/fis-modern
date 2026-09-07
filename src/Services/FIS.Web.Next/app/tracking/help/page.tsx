import Link from "next/link";

import { TrackingShell } from "@/app/tracking/_components";
import { accessRestricted, getTrackingSession, hasTrackingAccess, sessionMessage } from "@/app/tracking/_page";

export default async function TrackingHelpPage() {
  const session = await getTrackingSession();
  const problem = sessionMessage(session, "/tracking/help");
  if (problem) return problem;
  if (session.status !== "authenticated") return accessRestricted("Your session could not be loaded.");
  if (!hasTrackingAccess(session)) return accessRestricted("Your profile does not include Vehicle Management access.");
  return <TrackingShell title="Tracking Information / Help" description="Reference information for tracking maintenance and reports."><section className="vehicle-status-maintenance-panel"><h2>Tracking workflow</h2><p>Use Tracking Maintenance to capture tracker installations, removals, statuses, types, and notes for a vehicle. Use the reports menu to review the same records by vehicle, device, period, site, or department.</p><p>Vehicle searches use the existing GG and registration-number data. The API keeps the original tracking table and legacy fields available when modern audit columns are not present.</p><Link className="button button-secondary" href="/tracking">Tracking menu</Link></section></TrackingShell>;
}
