import Link from "next/link";

import { TrackingNotice, TrackingShell } from "@/app/tracking/_components";
import { accessRestricted, getTrackingSession, hasTrackingAccess, sessionMessage } from "@/app/tracking/_page";

export default async function TrackingReportsPage({ searchParams }: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getTrackingSession();
  const problem = sessionMessage(session, "/tracking/reports");
  if (problem) return problem;
  if (session.status !== "authenticated") return accessRestricted("Your session could not be loaded.");
  if (!hasTrackingAccess(session)) return accessRestricted("Your profile does not include Vehicle Management access.");
  return <TrackingShell title="Tracking Reports Menu" description="Run tracking reports while preserving the legacy report menu."><TrackingNotice query={await searchParams} /><div className="vehicle-menu-tiles"><section className="vehicle-menu-tile"><h2 className="vehicle-menu-header">Vehicle and device reports</h2><div className="vehicle-menu-body"><Link className="vehicle-menu-link" href="/tracking/reports/one-vehicle">1) Tracking Report for ONE Vehicle</Link><Link className="vehicle-menu-link" href="/tracking/reports/one-device">2) Tracking Report for ONE Tracking Device</Link><Link className="vehicle-menu-link" href="/tracking/reports/all-vehicle">3) Tracking Report for ALL Vehicle</Link><Link className="vehicle-menu-link" href="/tracking/reports/all-device">4) Tracking Report for ALL Tracking Device</Link></div></section><section className="vehicle-menu-tile"><h2 className="vehicle-menu-header">Period reports</h2><div className="vehicle-menu-body"><Link className="vehicle-menu-link" href="/tracking/reports/install-period">5) Tracking Report for an Install Period</Link><Link className="vehicle-menu-link" href="/tracking/reports/site-period">6) Tracking Report for a Site / ALL Sites</Link><Link className="vehicle-menu-link" href="/tracking/reports/dept-period">7) Tracking Report for a Dept / ALL Dept&apos;s</Link><Link className="vehicle-menu-link" href="/tracking">Return To Main Page</Link></div></section></div></TrackingShell>;
}
