import Link from "next/link";

import { MonitorForm, MonitorNotice, MonitorShell } from "@/app/monitor/_components";
import { accessRestricted, getMonitorSession, hasCallCentreAccess, queryValue, sessionMessage } from "@/app/monitor/_page";
import { getMonitorDrivers, MonitorApiError } from "@/lib/api-monitor";
import { getSites, type SiteRecord } from "@/lib/api-sites";
import { getVehicleOptions, type VehicleOption } from "@/lib/api-vehicles";

function filterVehicles(options: readonly VehicleOption[], search: string, mode: string) {
  const normalized = search.trim().toLocaleLowerCase();
  if (!normalized) return [];
  const useRegistration = mode.toLocaleUpperCase() === "GP";
  return options.filter((vehicle) => (useRegistration ? vehicle.registrationNumber : vehicle.fleetNumber)?.toLocaleLowerCase().includes(normalized)).sort((left, right) => left.vmfCode - right.vmfCode);
}

async function referenceData(): Promise<[SiteRecord[], Awaited<ReturnType<typeof getMonitorDrivers>>]> {
  return Promise.all([getSites(), getMonitorDrivers()]);
}

export default async function MonitorCapturePage({ searchParams }: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getMonitorSession();
  const problem = sessionMessage(session, "/monitor/capture");
  if (problem) return problem;
  if (session.status !== "authenticated") return accessRestricted("Your session could not be loaded.");
  if (!hasCallCentreAccess(session)) return accessRestricted("Your profile does not include Call Centre access.");

  const query = await searchParams;
  const search = queryValue(query.search);
  const mode = queryValue(query.mode) || "GG";
  const requestedVmf = Number(queryValue(query.vmfCode));
  try {
    const [vehicles, [sites, drivers]] = await Promise.all([getVehicleOptions(), referenceData()]);
    const matches = filterVehicles(vehicles, search, mode);
    const selected = Number.isInteger(requestedVmf) && requestedVmf > 0 ? vehicles.find((vehicle) => vehicle.vmfCode === requestedVmf) : null;
    return <MonitorShell title="Capture a New Inquiry" description="Capture a new Call Centre monitoring inquiry."><MonitorNotice query={query} /><form className="vehicle-status-maintenance-panel" method="get"><div className="vehicle-form-section-header"><div><p className="eyebrow">Vehicle lookup</p><h2>Find a vehicle before capturing an inquiry</h2></div></div><div className="vehicle-search-row"><label className="sr-only" htmlFor="monitor-lookup-mode">Lookup type</label><select className="form-select" id="monitor-lookup-mode" name="mode" defaultValue={mode}><option value="GG">GG number</option><option value="GP">Registration number</option></select><label className="sr-only" htmlFor="monitor-lookup-search">Vehicle number</label><input className="vehicle-search" id="monitor-lookup-search" name="search" defaultValue={search} placeholder={mode === "GP" ? "Enter registration number" : "Enter GG number"} required /><label className="sr-only" htmlFor="monitor-lookup-vehicle">Vehicle match</label><select className="form-select" id="monitor-lookup-vehicle" name="vmfCode" defaultValue={selected?.vmfCode ?? ""}><option value="">Select vehicle</option>{matches.map((vehicle) => <option key={vehicle.vmfCode} value={vehicle.vmfCode}>{vehicle.fleetNumber || "-"} / {vehicle.registrationNumber || "-"} ({vehicle.vmfCode})</option>)}</select><button className="button button-primary" type="submit">Find / select</button></div></form>{selected ? <MonitorForm record={null} vmfCode={selected.vmfCode} sites={sites} drivers={drivers} returnPath={`/monitor/capture?mode=${encodeURIComponent(mode)}&search=${encodeURIComponent(search)}&vmfCode=${selected.vmfCode}`} /> : <section className="vehicle-status-card"><p className="muted-copy">Search for a GG or registration number, select the vehicle match, then continue with the inquiry details.</p></section>}<div className="button-row"><Link className="button button-secondary" href="/monitor">Menu</Link></div></MonitorShell>;
  } catch (error) {
    return <MonitorShell title="Capture a New Inquiry" description="Capture a new Call Centre monitoring inquiry."><section className="vehicle-status-card" role="alert"><h2>{error instanceof MonitorApiError && error.reason === "unauthorized" ? "Your session has expired." : "Monitor reference data could not be loaded."}</h2><p className="muted-copy">Retry after the FIS API is available.</p><Link className="button button-primary" href="/monitor/capture">Try again</Link></section></MonitorShell>;
  }
}
