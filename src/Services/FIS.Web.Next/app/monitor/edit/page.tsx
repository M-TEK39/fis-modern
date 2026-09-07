import Link from "next/link";

import { MonitorForm, MonitorNotice, MonitorShell } from "@/app/monitor/_components";
import { accessRestricted, getMonitorSession, hasCallCentreAccess, parsePositiveInteger, queryValue, sessionMessage } from "@/app/monitor/_page";
import { getMonitor, getMonitorDrivers, MonitorApiError } from "@/lib/api-monitor";
import { getSites } from "@/lib/api-sites";

export default async function MonitorEditPage({ searchParams }: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getMonitorSession();
  const problem = sessionMessage(session, "/monitor/edit");
  if (problem) return problem;
  if (session.status !== "authenticated") return accessRestricted("Your session could not be loaded.");
  if (!hasCallCentreAccess(session)) return accessRestricted("Your profile does not include Call Centre access.");

  const query = await searchParams;
  const reference = parsePositiveInteger(queryValue(query.referenceNumber));
  try {
    const [sites, drivers] = await Promise.all([getSites(), getMonitorDrivers()]);
    const record = reference && reference <= 32767 ? await getMonitor(reference) : null;
    return <MonitorShell title="Edit / Update an Existing Inquiry" description="Update a captured Call Centre monitoring inquiry."><MonitorNotice query={query} /><form className="vehicle-status-maintenance-panel" method="get"><div className="vehicle-form-section-header"><div><p className="eyebrow">Inquiry lookup</p><h2>Enter a reference number</h2></div></div><div className="vehicle-search-row"><label className="sr-only" htmlFor="monitor-reference-number">Reference number</label><input className="vehicle-search" id="monitor-reference-number" name="referenceNumber" defaultValue={queryValue(query.referenceNumber)} inputMode="numeric" required /><button className="button button-primary" type="submit">Find inquiry</button></div></form>{reference && !record ? <section className="vehicle-status-card" role="alert"><h2>Inquiry not found</h2><p className="muted-copy">No monitor inquiry matched reference {reference}.</p></section> : record ? <MonitorForm record={record} vmfCode={record.vmfCode ?? 0} sites={sites} drivers={drivers} returnPath={`/monitor/edit?referenceNumber=${record.monitorCode}`} /> : null}<div className="button-row"><Link className="button button-secondary" href="/monitor">Menu</Link></div></MonitorShell>;
  } catch (error) {
    return <MonitorShell title="Edit / Update an Existing Inquiry" description="Update a captured Call Centre monitoring inquiry."><section className="vehicle-status-card" role="alert"><h2>{error instanceof MonitorApiError && error.reason === "not-found" ? "Inquiry not found." : "The Monitor service is temporarily unavailable."}</h2><Link className="button button-primary" href="/monitor/edit">Try again</Link></section></MonitorShell>;
  }
}
