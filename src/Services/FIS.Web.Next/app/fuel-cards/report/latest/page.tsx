import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { ApiUnavailable, FuelCardNotice, FuelCardTable, queryValue, VehicleResults, VehicleSearchForm } from "@/app/fuel-cards/_components";
import { FuelCardApiError, getFuelCardsByVehicle } from "@/lib/api-fuel-cards";
import { getSession } from "@/lib/session";
import { searchWorkshopVehicles } from "@/lib/api-workshop";

export default async function LatestFuelCardReportPage({ searchParams }: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/fuel-cards/report/latest" /></main>;
  if (!session.roles.some((role) => role.localeCompare("Fuelcards", undefined, { sensitivity: "accent" }) === 0)) return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to access Fuelcard reports.</h2></section></main>;
  const query = await searchParams;
  const type = queryValue(query.type) === "GP" ? "GP" : "GG";
  const search = queryValue(query.search).trim();
  const selected = Number(queryValue(query.vmfCode));
  const vmfCode = Number.isInteger(selected) && selected > 0 ? selected : null;
  try {
    const vehicles = search ? (await searchWorkshopVehicles(search)).filter((vehicle) => (type === "GP" ? vehicle.registrationNumber : vehicle.fleetNumber)?.toLocaleLowerCase().includes(search.toLocaleLowerCase())) : [];
    const selectedVehicle = vmfCode ? vehicles.find((vehicle) => vehicle.vmfCode === vmfCode) : null;
    const cards = vmfCode ? await getFuelCardsByVehicle(vmfCode) : [];
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="latest-fuel-card-report-title"><header className="vehicle-page-header"><div><p className="eyebrow">Fuelcard reports</p><h1 id="latest-fuel-card-report-title">Latest Fuelcard Report for a GG Vehicle</h1><p>Search for a vehicle to view its latest compatible fuelcard records.</p></div><Link className="button button-secondary" href="/fuel-cards">Menu</Link></header><FuelCardNotice query={query} /><VehicleSearchForm path="/fuel-cards/report/latest" type={type} search={search} /><section className="vehicle-status-maintenance-panel" aria-labelledby="latest-report-vehicles-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Vehicle lookup</p><h2 id="latest-report-vehicles-title">Matching vehicles</h2></div></div><VehicleResults vehicles={vehicles} path="/fuel-cards/report/latest" type={type} search={search} selectedCode={vmfCode} /></section>{selectedVehicle ? <section className="vehicle-status-maintenance-panel" aria-labelledby="latest-report-cards-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Latest records</p><h2 id="latest-report-cards-title">Fuelcards for {selectedVehicle.fleetNumber || selectedVehicle.registrationNumber || selectedVehicle.vmfCode}</h2></div></div><FuelCardTable cards={cards} /></section> : null}</section></main>;
  } catch (error) {
    if (error instanceof FuelCardApiError && error.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/fuel-cards/report/latest" /></main>;
    return <main className="page-shell vehicle-page-shell"><ApiUnavailable path="/fuel-cards/report/latest" subject="Latest fuelcard report" /></main>;
  }
}
