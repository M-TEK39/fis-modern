import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { createLeaseTariffAction } from "@/app/full-maintenance-lease/actions";
import { AccessRestricted, ActionNotice, ApiUnavailable, FmlFrame, formatCurrency, formatDate, hasFmlPermission, vehicleLabel } from "@/app/full-maintenance-lease/_components";
import { FmlApiError, getLatestLeaseTariff } from "@/lib/api-fml";
import { getVehicleOptions, type VehicleOption } from "@/lib/api-vehicles";
import { getSession } from "@/lib/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positiveInteger(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}

function matches(vehicle: VehicleOption, query: string, mode: string) {
  const value = mode.toUpperCase() === "GP" ? vehicle.registrationNumber : vehicle.fleetNumber;
  return !query.trim() || value?.toLowerCase().includes(query.trim().toLowerCase());
}

function addOneMonth(value: string) {
  const date = new Date(`${value.slice(0, 10)}T00:00:00Z`);
  date.setUTCMonth(date.getUTCMonth() + 1);
  return date.toISOString().slice(0, 10);
}

export default async function FmlAddLeasePage({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/full-maintenance-lease/add-lease" /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><ApiUnavailable message="The FML tariff form could not be opened." /></main>;
  if (!hasFmlPermission(session.accessLevel)) return <main className="page-shell vehicle-page-shell"><AccessRestricted /></main>;

  const query = await searchParams;
  const search = first(query.search) ?? "";
  const mode = first(query.mode) ?? "GG";
  const selectedVmfCode = positiveInteger(first(query.vmfCode));
  const result = first(query.result);
  const message = first(query.message);
  let vehicles: VehicleOption[];
  try {
    vehicles = await getVehicleOptions();
  } catch (error) {
    return <FmlFrame title="Add Lease Tariff" description="Capture a new tariff period for an in-service lease vehicle."><ApiUnavailable message={error instanceof FmlApiError ? error.message : undefined} /></FmlFrame>;
  }

  const vehicleMatches = vehicles.filter((vehicle) => matches(vehicle, search, mode)).slice(0, 100);
  const selectedVehicle = vehicles.find((vehicle) => vehicle.vmfCode === selectedVmfCode);
  let latest = null;
  if (selectedVmfCode) {
    try {
      latest = await getLatestLeaseTariff(selectedVmfCode);
    } catch (error) {
      if (!(error instanceof FmlApiError && error.reason === "not-found")) {
        return <FmlFrame title="Add Lease Tariff" description="Capture a new tariff period for an in-service lease vehicle."><ApiUnavailable message="The existing lease tariff could not be loaded." /></FmlFrame>;
      }
    }
  }

  const defaultStart = latest ? latest.endDate.slice(0, 10) : "";
  const defaultEnd = latest ? addOneMonth(latest.endDate) : "";
  return (
    <FmlFrame title="Add Lease Tariff" description="Capture a new tariff period for an in-service lease vehicle.">
      <ActionNotice result={result} message={message} />
      <div className="notice notice-info" role="note">Select a vehicle first. The legacy workflow starts the new tariff from the selected vehicle’s latest tariff end date.</div>
      <section className="form-card"><div className="form-card-header"><h2>Vehicle lookup</h2><p>Search by GG fleet number or GP registration number.</p></div><div className="form-card-body"><form method="get" className="form-grid"><div className="form-field"><label className="form-label" htmlFor="fml-add-search">Search number</label><input className="form-input" id="fml-add-search" name="search" defaultValue={search} placeholder="GG or GP number" /></div><div className="form-field"><label className="form-label" htmlFor="fml-add-mode">Number type</label><select className="form-select" id="fml-add-mode" name="mode" defaultValue={mode}><option value="GG">GG</option><option value="GP">GP</option></select></div><div className="form-actions form-group-full"><button className="button button-secondary" type="submit">Search vehicles</button></div></form>{search.trim() ? vehicleMatches.length === 0 ? <p className="muted-copy">No vehicles matched that search.</p> : <div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Vehicle search results</caption><thead><tr><th scope="col">Vehicle</th><th scope="col">Select</th></tr></thead><tbody>{vehicleMatches.map((vehicle) => <tr key={vehicle.vmfCode}><td>{vehicleLabel(vehicle)}</td><td><Link className="button button-secondary" href={`/full-maintenance-lease/add-lease?search=${encodeURIComponent(search)}&mode=${encodeURIComponent(mode)}&vmfCode=${vehicle.vmfCode}`}>Select</Link></td></tr>)}</tbody></table></div> : null}</div></section>
      {selectedVehicle ? <section className="form-card"><div className="form-card-header"><h2>Tariff details</h2><p>{vehicleLabel(selectedVehicle)}</p></div><div className="form-card-body"><form action={createLeaseTariffAction} className="form-grid"><input name="vmfCode" type="hidden" value={selectedVehicle.vmfCode} readOnly /><div className="form-field"><label className="form-label" htmlFor="fml-add-start">Start date</label><input className="form-input" id="fml-add-start" name="startDate" type="date" defaultValue={defaultStart} required /></div><div className="form-field"><label className="form-label" htmlFor="fml-add-end">End date</label><input className="form-input" id="fml-add-end" name="endDate" type="date" defaultValue={defaultEnd} required /></div><div className="form-field"><label className="form-label" htmlFor="fml-add-fixed">Fixed tariff</label><input className="form-input" id="fml-add-fixed" name="fixedTariff" type="number" min="0" step="0.01" defaultValue={latest?.fixedTariff ?? ""} required /></div><div className="form-field"><label className="form-label" htmlFor="fml-add-excess">Excess kilo tariff</label><input className="form-input" id="fml-add-excess" name="excessKiloTariff" type="number" min="0" step="0.01" defaultValue={latest?.excessKiloTariff ?? ""} /></div><div className="form-actions form-group-full"><button className="button button-primary" type="submit">Submit tariff</button><Link className="button button-secondary" href="/full-maintenance-lease/add-lease">Clear</Link></div></form>{latest ? <p className="muted-copy">Latest tariff: {formatDate(latest.startDate)} to {formatDate(latest.endDate)} · {formatCurrency(latest.fixedTariff)}</p> : <p className="muted-copy">No existing tariff was found for this vehicle.</p>}</div></section> : <section className="vehicle-status-card" role="status"><h2>Select a vehicle to continue</h2><p className="muted-copy">Search above, then select the matching in-service vehicle.</p></section>}
      <div className="button-row"><Link className="button button-secondary" href="/full-maintenance-lease/tariffs">Open capture queue</Link><Link className="button button-secondary" href="/full-maintenance-lease">FML Menu</Link></div>
    </FmlFrame>
  );
}
