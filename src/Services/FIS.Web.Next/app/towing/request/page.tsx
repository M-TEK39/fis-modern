import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { TowingApiError, getTowings, searchTowingVehicles, type TowingRecord, type TowingSearchType } from "@/lib/api-towing";
import { getSession } from "@/lib/session";

const TOWING_ROLE = "Towing";

export type TowingRequestPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getSearchType(value: string | undefined): TowingSearchType {
  return value === "GP" || value === "Radiogp" ? "GP" : "GG";
}

function hasTowingRole(roles: readonly string[]) {
  return roles.some((role) => role.localeCompare(TOWING_ROLE, undefined, { sensitivity: "accent" }) === 0);
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function vehicleLabel(vehicle: { fleetNumber: string | null; registrationNumber: string | null; vmfCode: number }) {
  return `${valueOrDash(vehicle.fleetNumber)} / ${valueOrDash(vehicle.registrationNumber)} (${vehicle.vmfCode})`;
}

function buildDetailHref(towingCode: number, searchType: TowingSearchType, searchQuery: string) {
  const params = new URLSearchParams({ towingId: String(towingCode), searchType });
  if (searchQuery) params.set("searchQuery", searchQuery);
  return `/towing/request/detail?${params.toString()}`;
}

function SearchForm({ searchType, searchQuery }: Readonly<{ searchType: TowingSearchType; searchQuery: string }>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <fieldset className="vehicle-search-options">
        <legend>Search by</legend>
        <label className="vehicle-checkbox-label"><input type="radio" name="searchType" value="GG" defaultChecked={searchType === "GG"} /> GG</label>
        <label className="vehicle-checkbox-label"><input type="radio" name="searchType" value="GP" defaultChecked={searchType === "GP"} /> GP</label>
      </fieldset>
      <div className="vehicle-search-row">
        <label className="sr-only" htmlFor="towing-vehicle-search">{searchType === "GG" ? "GG number" : "GP number"}</label>
        <input className="vehicle-search" id="towing-vehicle-search" name="searchQuery" maxLength={8} defaultValue={searchQuery} placeholder={searchType === "GG" ? "Enter GG number" : "Enter GP number"} />
      </div>
      <div className="button-row"><button className="button button-primary" type="submit">Submit</button><Link className="button button-secondary" href="/towing">Menu</Link></div>
    </form>
  );
}

function TowingRows({ rows, searchType, searchQuery }: Readonly<{ rows: TowingRecord[]; searchType: TowingSearchType; searchQuery: string }>) {
  if (rows.length === 0) {
    return <div className="vehicle-empty-state"><p className="eyebrow">No records found</p><h2>{searchQuery ? `No towing requests matched “${searchQuery}”.` : "Search for a vehicle to view towing requests."}</h2><p className="muted-copy">Use the GG fleet number or GP registration number from the legacy request screen.</p></div>;
  }

  return (
    <div className="vehicle-table-wrapper" aria-live="polite">
      <table className="vehicle-table"><caption className="sr-only">Towing requests</caption><thead><tr><th scope="col">Vehicle</th><th scope="col">Request Date</th><th scope="col">Reference</th><th scope="col">Location</th><th scope="col">Action</th></tr></thead>
        <tbody>{rows.map((item) => <tr key={item.towingCode}><td>{valueOrDash(searchQuery)}</td><td>{formatDate(item.requestDate)}</td><td>{valueOrDash(item.callReference)}</td><td>{valueOrDash(item.locationStart)}</td><td><Link className="button button-secondary button-small" href={buildDetailHref(item.towingCode, searchType, searchQuery)}>Mod</Link></td></tr>)}</tbody>
      </table>
    </div>
  );
}

function ApiUnavailable({ routePath }: Readonly<{ routePath: string }>) {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>Towing requests could not be loaded.</h2><p className="muted-copy">The application is still running. Retry when the FIS API is available.</p><Link className="button button-primary" href={routePath}>Try again</Link></section>;
}

export default async function TowingRequestPage({ searchParams, routePath = "/towing/request" }: TowingRequestPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><ApiUnavailable routePath={routePath} /></main>;
  if (!hasTowingRole(session.roles)) return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to maintain towing requests.</h2></section></main>;

  const query = await searchParams;
  const searchType = getSearchType(getQueryValue(query.searchType) ?? getQueryValue(query.Radio1));
  const searchQuery = (getQueryValue(query.searchQuery) ?? getQueryValue(query.xggnum) ?? "").trim().slice(0, 8);
  const notice = getQueryValue(query.error) ?? (getQueryValue(query.saved) === "1" ? "Towing request captured successfully." : getQueryValue(query.updated) === "1" ? "Towing request updated successfully." : getQueryValue(query.deleted) === "1" ? "Towing request deleted successfully." : "");
  const isError = Boolean(getQueryValue(query.error));

  let rows: TowingRecord[] = [];
  let vehicles: Awaited<ReturnType<typeof searchTowingVehicles>> = [];
  try {
    if (searchQuery) {
      const [all, matches] = await Promise.all([getTowings(), searchTowingVehicles(searchType, searchQuery)]);
      vehicles = matches;
      const codes = new Set(matches.map((vehicle) => vehicle.vmfCode));
      rows = all.filter((item) => codes.has(item.vmfCode));
    }
  } catch (error) {
    if (error instanceof TowingApiError && error.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={`${routePath}?${new URLSearchParams({ searchType, searchQuery }).toString()}`} /></main>;
    console.error("FIS towing request lookup failed", error instanceof Error ? error.message : "unknown error");
    return <main className="page-shell vehicle-page-shell"><ApiUnavailable routePath={routePath} /></main>;
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="towing-request-title">
        <header className="vehicle-page-header"><div><p className="eyebrow">Road Side Assistance</p><h1 id="towing-request-title">Road Side Assistance Request Maintenance</h1><p>Search by the same GG or GP vehicle identifier used by the legacy screen, then capture, edit, or delete a request.</p></div><Link className="button button-secondary" href="/towing">Towing Menu</Link></header>
        {notice ? <div className={isError ? "notice notice-error" : "notice notice-success"} role={isError ? "alert" : "status"}>{notice}</div> : null}
        <SearchForm searchType={searchType} searchQuery={searchQuery} />
        {searchQuery && vehicles.length === 0 ? <div className="vehicle-empty-state"><p className="eyebrow">Vehicle not found</p><h2>No vehicle matched “{searchQuery}”.</h2><p className="muted-copy">Check the lookup type and try again.</p></div> : null}
        {vehicles.length > 0 ? <section className="vehicle-status-maintenance-panel" aria-labelledby="towing-vehicle-match-title"><p className="eyebrow">Vehicle resolution</p><h2 id="towing-vehicle-match-title">Matched vehicle{vehicles.length === 1 ? "" : "s"}</h2><ul className="form-hint fis-list-pad">{vehicles.slice(0, 5).map((vehicle) => <li key={vehicle.vmfCode}>{vehicleLabel(vehicle)} <Link className="button button-secondary button-small" href={`/towing/request/detail?vmfCode=${vehicle.vmfCode}&searchType=${searchType}&searchQuery=${encodeURIComponent(searchQuery)}`}>New request</Link></li>)}</ul></section> : null}
        <section className="vehicle-status-maintenance-panel" aria-labelledby="towing-request-results-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">Existing requests</p><h2 id="towing-request-results-title">Requests found</h2></div></div><TowingRows rows={rows} searchType={searchType} searchQuery={searchQuery} /></section>
        <div className="vehicle-footer-actions"><Link className="button button-secondary" href="/towing/help">Help</Link><Link className="button button-secondary" href="/home">Home</Link></div>
      </section>
    </main>
  );
}
