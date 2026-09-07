import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { getWorkshopVehicles, getWorkshops, WorkshopApiError, type WorkshopRecord, type WorkshopVehicle } from "@/lib/api-workshop";
import { getSession } from "@/lib/session";

const WORKSHOP_ROLE = "Workshop";

function hasRole(roles: readonly string[]) { return roles.some((role) => role.localeCompare(WORKSHOP_ROLE, undefined, { sensitivity: "accent" }) === 0); }
function formatDate(value: string | null) { return value?.slice(0, 10) || "-"; }
function valueOrDash(value: string | number | null | undefined) { return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value); }

function VehicleSearch({ search, type }: Readonly<{ search: string; type: string }>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <fieldset className="vehicle-search-options"><legend>Search by</legend><label className="vehicle-checkbox-label"><input type="radio" name="type" value="GG" defaultChecked={type !== "GP"} /> GG</label><label className="vehicle-checkbox-label"><input type="radio" name="type" value="GP" defaultChecked={type === "GP"} /> GP</label></fieldset>
      <div className="vehicle-search-row"><label className="sr-only" htmlFor="workshop-entry-search">{type === "GP" ? "GP Number" : "GG Number"}</label><input className="vehicle-search" id="workshop-entry-search" name="search" defaultValue={search} placeholder={type === "GP" ? "Enter GP number" : "Enter GG number"} /><button className="button button-primary" type="submit">Search</button><Link className="button button-secondary" href="/workshop/entry">Clear</Link></div>
    </form>
  );
}

function EntryTable({ workshops, vehicles }: Readonly<{ workshops: WorkshopRecord[]; vehicles: WorkshopVehicle[] }>) {
  const vehicleByCode = new Map(vehicles.map((vehicle) => [vehicle.vmfCode, vehicle]));
  return workshops.length === 0 ? <p className="muted-copy">No previous workshop records found.</p> : (
    <div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Previous workshop entries</caption><thead><tr><th scope="col">Vehicle Number</th><th scope="col">Department</th><th scope="col">Date Received</th><th scope="col">Inquiry Refer</th><th scope="col">Workshop</th><th scope="col">Job Card CLOSED?</th><th scope="col">Actions</th></tr></thead><tbody>
      {workshops.map((entry) => { const vehicle = entry.vmfCode === null ? undefined : vehicleByCode.get(entry.vmfCode); const closed = entry.completeDate !== null || entry.completeTime !== null; return <tr key={entry.wwCode}><td>{vehicle ? `${valueOrDash(vehicle.fleetNumber)} / ${valueOrDash(vehicle.registrationNumber)}` : `VMF ${valueOrDash(entry.vmfCode)}`}</td><td>{valueOrDash(vehicle?.locationDescription ?? vehicle?.locationCode)}</td><td>{formatDate(entry.receiveDate)}</td><td>{entry.wwCode}</td><td>Workshop</td><td>{closed ? "Yes" : "No"}</td><td><div className="button-row"><Link className="button button-secondary button-small" href={`/workshop/entry/modify?id=${entry.wwCode}`}>Edit</Link><Link className="button button-danger button-small" href={`/workshop/entry/delete?id=${entry.wwCode}`}>Delete</Link></div></td></tr>; })}
    </tbody></table></div>
  );
}

export default async function WorkshopEntryPage({ searchParams }: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/workshop/entry" /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/workshop/entry" /></main>;
  if (!hasRole(session.roles)) return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to maintain Workshop records.</h2></section></main>;

  const query = await searchParams;
  const search = Array.isArray(query.search) ? query.search[0] ?? "" : query.search ?? "";
  const type = Array.isArray(query.type) ? query.type[0] ?? "GG" : query.type ?? "GG";
  try {
    const [allWorkshops, vehicles] = await Promise.all([getWorkshops(), getWorkshopVehicles()]);
    const normalized = search.trim().toLocaleLowerCase();
    const matchingVehicles = normalized ? vehicles.filter((vehicle) => (type === "GP" ? vehicle.registrationNumber : vehicle.fleetNumber)?.toLocaleLowerCase().includes(normalized)) : vehicles;
    const vmfCodes = new Set(matchingVehicles.map((vehicle) => vehicle.vmfCode));
    const workshops = normalized ? allWorkshops.filter((entry) => entry.vmfCode !== null && vmfCodes.has(entry.vmfCode)) : allWorkshops;
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="workshop-entry-title"><header className="vehicle-page-header"><div><p className="eyebrow">Workshop maintenance</p><h1 id="workshop-entry-title">WorkShop Entry</h1><p>Search for a vehicle to capture or update a workshop entry.</p></div><Link className="button button-secondary" href="/workshop">Workshop Menu</Link></header><VehicleSearch search={search} type={type} /><section className="vehicle-status-maintenance-panel" aria-labelledby="workshop-history-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">{workshops.length} record{workshops.length === 1 ? "" : "s"}</p><h2 id="workshop-history-title">Previous Workshop Entries</h2></div><Link className="button button-primary" href="/workshop/entry/details">Add Entry</Link></div><EntryTable workshops={workshops} vehicles={vehicles} /></section></section></main>;
  } catch (error) {
    const message = error instanceof WorkshopApiError && error.reason === "unavailable" ? "The workshop service is temporarily unavailable. Please try again." : "Workshop entries could not be loaded.";
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Workshop maintenance</p><h2>{message}</h2><div className="button-row"><Link className="button button-primary" href="/workshop/entry">Try again</Link><Link className="button button-secondary" href="/workshop">Workshop Menu</Link></div></section></main>;
  }
}
