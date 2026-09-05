import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { ContractApiError, getContractPage, searchContractVehicles, type ContractRecord, type ContractVehicleSearchResult } from "@/lib/api-contracts";
import { getSession } from "@/lib/session";

const CONTRACT_PERMISSION = BigInt(2);

export type ContractMaintenancePageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positiveInt(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasContractAccess(accessLevel: string | undefined, roles: readonly string[]) {
  if (roles.some((role) => ["contracts", "contract", "admin", "administrator"].includes(role.trim().toLowerCase()))) {
    return true;
  }

  try {
    return accessLevel ? (BigInt(accessLevel) & CONTRACT_PERMISSION) === CONTRACT_PERMISSION : false;
  } catch {
    return false;
  }
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function getStatusLabel(contract: ContractRecord) {
  switch (contract.contractStatusCode) {
    case 0: return "Draft";
    case 1: return "Pending Review";
    case 2: return "Approved";
    case 3: return "Active";
    case 4: return "Declined for Correction";
    case 5: return "Declined";
    case 6: return "Cancelled";
    case 7: return "Closed";
    default: return contract.stillCurrent?.toUpperCase() === "Y" ? "Active" : "Unknown";
  }
}

function getStatusClass(contract: ContractRecord) {
  if (contract.contractStatusCode === 3 || contract.stillCurrent?.toUpperCase() === "Y") return "badge-success";
  if (contract.contractStatusCode === 1 || contract.contractStatusCode === 4) return "badge-warning";
  if ([5, 6].includes(contract.contractStatusCode ?? -1)) return "badge-error";
  return "badge";
}

function detailHref(contract: ContractRecord) {
  const params = new URLSearchParams({ contractId: String(contract.contractCode) });
  if (contract.fleetNumber) params.set("ggnumber", contract.fleetNumber);
  if (contract.registrationNumber) params.set("regnumber", contract.registrationNumber);
  return `/contracts/detail?${params.toString()}`;
}

function vehicleDetailHref(vehicle: ContractVehicleSearchResult) {
  const params = new URLSearchParams({ vmfCode: String(vehicle.vmfCode) });
  if (vehicle.fleetNumber) params.set("ggnumber", vehicle.fleetNumber);
  if (vehicle.registrationNumber) params.set("regnumber", vehicle.registrationNumber);
  return `/contracts/detail?${params.toString()}`;
}

function buildPageHref(query: Record<string, string | string[] | undefined>, page: number) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    const first = getQueryValue(value);
    if (first && key !== "page") params.set(key, first);
  }
  if (page > 1) params.set("page", String(page));
  const queryString = params.toString();
  return `/contracts/maintenance${queryString ? `?${queryString}` : ""}`;
}

const HIDDEN_FILTERS = ["status", "siteCode", "stillCurrent", "startDateFrom", "startDateTo"] as const;

function SearchForm({ searchType, searchQuery, query }: Readonly<{ searchType: "GG" | "GP"; searchQuery: string; query: Record<string, string | string[] | undefined> }>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      {HIDDEN_FILTERS.map((key) => {
        const value = getQueryValue(query[key]);
        return value ? <input key={key} name={key} type="hidden" value={value} /> : null;
      })}
      <fieldset className="vehicle-search-options">
        <legend>Select the vehicle to manage</legend>
        <label className="vehicle-checkbox-label"><input name="searchType" type="radio" value="GG" defaultChecked={searchType === "GG"} /> GG Number</label>
        <label className="vehicle-checkbox-label"><input name="searchType" type="radio" value="GP" defaultChecked={searchType === "GP"} /> Registration Number</label>
      </fieldset>
      <div className="vehicle-search-row">
        <label className="sr-only" htmlFor="contract-vehicle-search">{searchType === "GG" ? "GG number" : "Registration number"}</label>
        <input className="vehicle-search" id="contract-vehicle-search" maxLength={20} name="searchQuery" placeholder={searchType === "GG" ? "Enter GG number" : "Enter registration number"} defaultValue={searchQuery} />
        <button className="button button-primary" type="submit">Search</button>
      </div>
      <div className="button-row"><Link className="button button-secondary" href="/contracts">Contracts Menu</Link></div>
    </form>
  );
}

function Filters({ query }: Readonly<{ query: Record<string, string | string[] | undefined> }>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <input name="searchType" type="hidden" value={getQueryValue(query.searchType) ?? "GG"} />
      <input name="searchQuery" type="hidden" value={getQueryValue(query.searchQuery) ?? ""} />
      <div className="form-grid">
        <div className="form-field"><label className="form-label" htmlFor="contract-status">Status</label><select className="form-select" id="contract-status" name="status" defaultValue={getQueryValue(query.status) ?? ""}><option value="">All statuses</option>{[[0, "Draft"], [1, "Pending Review"], [2, "Approved"], [3, "Active"], [4, "Declined for Correction"], [5, "Declined"], [6, "Cancelled"], [7, "Closed"]].map(([code, label]) => <option key={code} value={code}>{label}</option>)}</select></div>
        <div className="form-field"><label className="form-label" htmlFor="contract-site">Site code</label><input className="form-input" id="contract-site" min="1" name="siteCode" type="number" defaultValue={getQueryValue(query.siteCode) ?? ""} /></div>
        <div className="form-field"><label className="form-label" htmlFor="contract-current">Current</label><select className="form-select" id="contract-current" name="stillCurrent" defaultValue={getQueryValue(query.stillCurrent) ?? ""}><option value="">All</option><option value="Y">Still current</option><option value="N">Not current</option></select></div>
        <div className="form-field"><label className="form-label" htmlFor="contract-from">Start date from</label><input className="form-input" id="contract-from" name="startDateFrom" type="date" defaultValue={getQueryValue(query.startDateFrom)?.slice(0, 10) ?? ""} /></div>
        <div className="form-field"><label className="form-label" htmlFor="contract-to">Start date to</label><input className="form-input" id="contract-to" name="startDateTo" type="date" defaultValue={getQueryValue(query.startDateTo)?.slice(0, 10) ?? ""} /></div>
      </div>
      <div className="button-row"><button className="button button-primary" type="submit">Apply filters</button><Link className="button button-secondary" href="/contracts/maintenance">Reset</Link></div>
    </form>
  );
}

function VehicleSearchResults({ vehicles }: Readonly<{ vehicles: ContractVehicleSearchResult[] }>) {
  if (vehicles.length === 0) return null;
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="contract-vehicle-results-title">
      <p className="eyebrow">Vehicle search results</p>
      <h2 id="contract-vehicle-results-title">Select a vehicle to open contract management</h2>
      <div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Vehicles matched by contract search</caption><thead><tr><th scope="col">GG number</th><th scope="col">Registration</th><th scope="col">Action</th></tr></thead><tbody>{vehicles.slice(0, 10).map((vehicle) => <tr key={vehicle.vmfCode}><td>{valueOrDash(vehicle.fleetNumber)}</td><td>{valueOrDash(vehicle.registrationNumber)}</td><td><Link className="button button-primary button-small" href={vehicleDetailHref(vehicle)}>Open</Link></td></tr>)}</tbody></table></div>
    </section>
  );
}

function ContractTable({ contracts }: Readonly<{ contracts: ContractRecord[] }>) {
  if (contracts.length === 0) return <div className="vehicle-empty-state"><p className="eyebrow">No records found</p><h2>No contracts matched the selected filters.</h2><p className="muted-copy">Search for a vehicle or adjust the contract filters.</p></div>;
  return (
    <div className="vehicle-table-wrapper"><table className="vehicle-table"><caption className="sr-only">Vehicle contracts</caption><thead><tr><th scope="col">Contract</th><th scope="col">Vehicle</th><th scope="col">Site</th><th scope="col">Driver</th><th scope="col">Start date</th><th scope="col">Target return</th><th scope="col">Status</th><th scope="col">Action</th></tr></thead><tbody>{contracts.map((contract) => <tr key={contract.contractCode}><td>{contract.contractCode}</td><td>{valueOrDash(contract.fleetNumber)} / {valueOrDash(contract.registrationNumber)}</td><td>{valueOrDash(contract.siteDescription)} ({contract.siteCode})</td><td>{valueOrDash(contract.driverName)}</td><td>{formatDate(contract.startDate)}</td><td>{formatDate(contract.targetReturnDate)}</td><td><span className={`badge ${getStatusClass(contract)}`}>{getStatusLabel(contract)}</span></td><td><div className="button-row"><Link className="button button-secondary button-small" href={detailHref(contract)}>Open</Link>{(contract.contractStatusCode ?? 0) >= 2 ? <Link className="button button-secondary button-small" href={`/contracts/printout?contractId=${contract.contractCode}`}>Print</Link> : null}</div></td></tr>)}</tbody></table></div>
  );
}

function ApiUnavailable({ routePath }: Readonly<{ routePath: string }>) {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>Contract maintenance could not be loaded.</h2><p className="muted-copy">The application is still running. Retry when the FIS API is available.</p><div className="button-row"><Link className="button button-primary" href={routePath}>Try again</Link><Link className="button button-secondary" href="/login">Sign in</Link></div></section>;
}

export default async function ContractMaintenancePage({ searchParams, routePath = "/contracts/maintenance" }: ContractMaintenancePageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><ApiUnavailable routePath={routePath} /></main>;
  if (!hasContractAccess(session.accessLevel, session.roles)) return <main className="page-shell vehicle-page-shell"><section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>You do not have permission to maintain vehicle contracts.</h2></section></main>;

  const query = await searchParams;
  const searchType = getQueryValue(query.searchType) === "GP" ? "GP" : "GG";
  const searchQuery = (getQueryValue(query.searchQuery) ?? getQueryValue(query.txtGGNumber) ?? getQueryValue(query.txtRegistrationNumber) ?? "").trim().slice(0, 20);
  const page = positiveInt(getQueryValue(query.page)) ?? 1;
  const statusCode = positiveInt(getQueryValue(query.status));
  const siteCode = positiveInt(getQueryValue(query.siteCode));
  const statusFilter = getQueryValue(query.status) === "0" ? 0 : statusCode;
  const notice = getQueryValue(query.saved) === "1" ? "Contract captured successfully." : getQueryValue(query.updated) === "1" ? "Contract updated successfully." : getQueryValue(query.success) ? `Contract ${getQueryValue(query.success)} successfully.` : getQueryValue(query.error);

  try {
    const [pageData, vehicles] = await Promise.all([
      getContractPage({ page, pageSize: 12, statusCode: statusFilter, siteCode, stillCurrent: getQueryValue(query.stillCurrent), startDateFrom: getQueryValue(query.startDateFrom), startDateTo: getQueryValue(query.startDateTo) }),
      searchQuery ? searchContractVehicles(searchQuery) : Promise.resolve([] as ContractVehicleSearchResult[]),
    ]);
    const filteredVehicles = searchQuery
      ? vehicles.filter((vehicle) => (searchType === "GG" ? vehicle.fleetNumber : vehicle.registrationNumber)?.toLocaleLowerCase().includes(searchQuery.toLocaleLowerCase()))
      : [];
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="contract-maintenance-title">
          <header className="vehicle-page-header"><div><p className="eyebrow">Contract maintenance</p><h1 id="contract-maintenance-title">Vehicle Contract Maintenance</h1><p>Search a vehicle and manage its existing or new legacy contract.</p></div><Link className="button button-secondary" href="/contracts">Contracts Menu</Link></header>
          {notice ? <div className={getQueryValue(query.error) ? "notice notice-error" : "notice notice-success"} role={getQueryValue(query.error) ? "alert" : "status"}>{notice}</div> : null}
          <SearchForm searchType={searchType} searchQuery={searchQuery} query={query} />
          <VehicleSearchResults vehicles={filteredVehicles} />
          <Filters query={query} />
          <section className="vehicle-status-maintenance-panel" aria-labelledby="contract-results-title"><div className="vehicle-form-section-header"><div><p className="eyebrow">{pageData.totalRecords} record{pageData.totalRecords === 1 ? "" : "s"}</p><h2 id="contract-results-title">Existing contracts</h2></div></div><ContractTable contracts={pageData.items} /><div className="pagination"><Link className={`pagination-btn${pageData.page <= 1 ? " disabled" : ""}`} href={pageData.page <= 1 ? "#" : buildPageHref(query, pageData.page - 1)}>Previous</Link><span className="pagination-info">Page {pageData.page} of {pageData.totalPages}</span><Link className={`pagination-btn${pageData.page >= pageData.totalPages ? " disabled" : ""}`} href={pageData.page >= pageData.totalPages ? "#" : buildPageHref(query, pageData.page + 1)}>Next</Link></div></section>
          <div className="vehicle-footer-actions"><Link className="button button-secondary" href="/home">Home</Link></div>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof ContractApiError && error.reason === "unauthorized") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath={routePath} /></main>;
    console.error("FIS contract maintenance request failed", error instanceof Error ? error.message : "unknown error");
    return <main className="page-shell vehicle-page-shell"><ApiUnavailable routePath={routePath} /></main>;
  }
}
