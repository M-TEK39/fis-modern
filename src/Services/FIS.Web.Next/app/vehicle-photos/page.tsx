import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { logoutAction } from "@/app/actions/auth";
import { getQueryValue, hasVehicleManagementPermission } from "@/app/drivers/access";
import SessionRecovery from "@/app/home/session-recovery";
import {
  getVehicleSearchCriteria,
  searchVehiclePhotos,
  VehiclePhotoApiError,
  type VehiclePhotoSearchRecord,
} from "@/lib/api-vehicle-photos";
import { getSession } from "@/lib/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;
const PAGE_SIZE = 12;

function formatDate(value: string | null) {
  if (!value) return "-";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value.slice(0, 10) : date.toLocaleDateString("en-ZA");
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || value === "" ? "-" : String(value);
}

function resultMatchesMode(vehicle: VehiclePhotoSearchRecord, mode: "gg" | "gp", query: string) {
  const value = mode === "gg" ? vehicle.ggNumber : vehicle.registrationNumber;
  return Boolean(value?.toLocaleLowerCase().includes(query.toLocaleLowerCase()));
}

function StatusCard({ title, message, routePath = "/vehicle-photos" }: Readonly<{ title: string; message: string; routePath?: string }>) {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">{title}</p><h2>{message}</h2><p className="muted-copy">The application is still running. Retry when the FIS API is available.</p><div className="button-row"><Link className="button button-primary" href={routePath}>Try again</Link><Link className="button button-secondary" href="/home">Home</Link></div></section>;
}

function VehicleResults({ vehicles, query, mode, page }: Readonly<{ vehicles: VehiclePhotoSearchRecord[]; query: string; mode: "gg" | "gp"; page: number }>) {
  const filtered = vehicles.filter((vehicle) => resultMatchesMode(vehicle, mode, query));
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const currentPage = Math.min(Math.max(page, 1), totalPages);
  const pageItems = filtered.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);
  const pageHref = (nextPage: number) => {
    const params = new URLSearchParams({ q: query, mode });
    if (nextPage > 1) params.set("page", String(nextPage));
    return `/vehicle-photos?${params.toString()}`;
  };

  if (filtered.length === 0) return <div className="vehicle-empty-state"><p className="eyebrow">No vehicles found</p><p>No vehicle matched the selected {mode.toUpperCase()} search.</p></div>;

  return <>
    <div className="table-container"><div className="table-header"><span className="table-title">{filtered.length} vehicle(s)</span></div><div className="table-wrapper"><table className="data-table"><caption className="sr-only">Vehicle photo search results</caption><thead><tr><th scope="col">GG Number</th><th scope="col">Registration Number</th><th scope="col">Make and Model</th><th scope="col">Year Manufactured</th><th scope="col">Colour</th><th scope="col">Hire Type</th><th scope="col">Status</th><th scope="col">Hired From</th><th scope="col">Status Date</th><th scope="col">Actions</th></tr></thead><tbody>{pageItems.map((vehicle) => <tr key={vehicle.vmfCode}><td>{valueOrDash(vehicle.ggNumber)}</td><td>{valueOrDash(vehicle.registrationNumber)}</td><td>{valueOrDash(vehicle.makeAndModel)}</td><td>{valueOrDash(vehicle.yearManufactured)}</td><td>{valueOrDash(vehicle.colour)}</td><td>{valueOrDash(vehicle.hireType)}</td><td>{valueOrDash(vehicle.status)}</td><td>{valueOrDash(vehicle.hiredFrom)}</td><td>{formatDate(vehicle.statusDate)}</td><td><Link className="button button-secondary button-small" href={`/vehicle-photos/manage/${vehicle.vmfCode}?keyword=${encodeURIComponent(query)}`}>Manage photos</Link></td></tr>)}</tbody></table></div></div>
    <nav className="pagination-controls" aria-label="Vehicle photo search pagination">{currentPage <= 1 ? <span className="button button-secondary" aria-disabled="true">Previous</span> : <Link className="button button-secondary" href={pageHref(currentPage - 1)}>Previous</Link>}<span aria-live="polite">Page {currentPage} of {totalPages}</span>{currentPage >= totalPages ? <span className="button button-secondary" aria-disabled="true">Next</span> : <Link className="button button-secondary" href={pageHref(currentPage + 1)}>Next</Link>}</nav>
  </>;
}

export default async function VehiclePhotosPage({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/vehicle-photos" /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><StatusCard title="API unavailable" message="Vehicle photo search is unavailable." /></main>;
  if (!hasVehicleManagementPermission(session.accessLevel)) return <main className="page-shell vehicle-page-shell"><StatusCard title="Access restricted" message="You do not have permission to maintain vehicle photos." /></main>;

  const query = await searchParams;
  const searchQuery = (getQueryValue(query.q) ?? getQueryValue(query.keyword) ?? "").trim();
  const mode = getQueryValue(query.mode) === "gp" ? "gp" : "gg";
  const requestedPage = Number.parseInt(getQueryValue(query.page) ?? "1", 10);
  const page = Number.isFinite(requestedPage) ? Math.max(1, requestedPage) : 1;

  try {
    const criteria = await getVehicleSearchCriteria();
    const vehicles = searchQuery ? await searchVehiclePhotos(searchQuery) : [];
    return <main className="page-shell vehicle-page-shell"><section className="vehicle-card" aria-labelledby="vehicle-photos-title"><header className="vehicle-page-header"><div><p className="eyebrow">Vehicle administration</p><h1 id="vehicle-photos-title">Vehicle Photo Upload</h1><p>Search for vehicles by GG or registration number, then manage their photo references.</p></div><div className="button-row"><Link className="button button-secondary" href="/home">Home</Link><form action={logoutAction}><button className="button button-secondary" type="submit">Sign out</button></form></div></header><form className="vehicle-photo-search-form" method="get"><fieldset><legend>Search for a vehicle</legend><div className="vehicle-photo-search-options"><label><input type="radio" name="mode" value="gg" defaultChecked={mode === "gg"} /> GG number</label><label><input type="radio" name="mode" value="gp" defaultChecked={mode === "gp"} /> Registration number</label></div><div className="field"><label htmlFor="vehicle-photo-search">Search keyword</label><input id="vehicle-photo-search" name="q" type="search" list="vehicle-photo-keywords" defaultValue={searchQuery} placeholder={mode === "gg" ? "GG number" : "Registration number"} autoComplete="off" required /><datalist id="vehicle-photo-keywords">{criteria.map((keyword) => <option key={keyword} value={keyword} />)}</datalist></div><button className="button button-primary" type="submit">Search</button></fieldset></form>{searchQuery ? <VehicleResults vehicles={vehicles} query={searchQuery} mode={mode} page={page} /> : <div className="vehicle-empty-state"><p className="eyebrow">Ready to search</p><p>Enter a GG or registration number to find a vehicle.</p></div>}<div className="vehicle-footer-actions"><Link className="button button-secondary" href="/home">Home</Link></div></section></main>;
  } catch (error) {
    if (error instanceof VehiclePhotoApiError && error.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/vehicle-photos" /></main>;
    console.error("FIS vehicle photo search failed", error instanceof Error ? error.message : "unknown error");
    return <main className="page-shell vehicle-page-shell"><StatusCard title="API unavailable" message="Vehicle photo search could not be loaded." /></main>;
  }
}
