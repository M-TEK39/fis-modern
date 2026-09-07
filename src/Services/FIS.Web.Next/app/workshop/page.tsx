import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import { getWorkshopVehicles, getWorkshops, WorkshopApiError, type WorkshopRecord, type WorkshopVehicle } from "@/lib/api-workshop";
import { getSession } from "@/lib/session";

const WORKSHOP_ROLE = "Workshop";

function hasRole(roles: readonly string[], role: string) {
  return roles.some((candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0);
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access Workshop.</h2>
    </section>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">!</div>
      <p className="eyebrow">API unavailable</p>
      <h2>Workshop could not be opened.</h2>
      <p className="muted-copy">The application is still running. Retry when the FIS API is available.</p>
      <div className="button-row">
        <Link className="button button-primary" href="/workshop">Try again</Link>
        <Link className="button button-secondary" href="/login">Sign in</Link>
      </div>
    </section>
  );
}

function WorkshopSnapshot({ workshops, vehicles, search, status }: Readonly<{
  workshops: WorkshopRecord[];
  vehicles: WorkshopVehicle[];
  search: string;
  status: string;
}>) {
  const vehicleByCode = new Map(vehicles.map((vehicle) => [vehicle.vmfCode, vehicle]));
  const normalizedSearch = search.trim().toLocaleLowerCase();
  const rows = workshops.filter((workshop) => {
    const vehicle = workshop.vmfCode === null ? undefined : vehicleByCode.get(workshop.vmfCode);
    const isClosed = workshop.completeDate !== null || workshop.completeTime !== null;
    if (status === "open" && isClosed) return false;
    if (status === "closed" && !isClosed) return false;
    if (status === "vehicle" && workshop.vmfCode === null) return false;
    if (!normalizedSearch) return true;
    return [
      workshop.wwCode,
      workshop.vmfCode,
      vehicle?.fleetNumber,
      vehicle?.registrationNumber,
      isClosed ? "closed" : "open",
    ].some((value) => String(value ?? "").toLocaleLowerCase().includes(normalizedSearch));
  });

  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="workshop-snapshot-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Live records</p>
          <h2 id="workshop-snapshot-title">All Workshop Entries Snapshot ({rows.length})</h2>
        </div>
      </div>
      <form className="vehicle-search-row" method="get">
        <label className="sr-only" htmlFor="workshop-search">Search workshop entries</label>
        <input className="vehicle-search" id="workshop-search" name="search" placeholder="Search entry ID, vehicle, status..." defaultValue={search} />
        <label className="sr-only" htmlFor="workshop-status">Filter workshop entries</label>
        <select className="form-select" id="workshop-status" name="status" defaultValue={status}>
          <option value="">All entries</option>
          <option value="open">Open entries</option>
          <option value="closed">Closed entries</option>
          <option value="vehicle">Vehicle-linked entries</option>
        </select>
        <button className="button button-secondary" type="submit">Filter</button>
      </form>
      {rows.length === 0 ? (
        <p className="muted-copy">No workshop entries match the current filter.</p>
      ) : (
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">Workshop entry snapshot</caption>
            <thead>
              <tr><th scope="col">Entry ID</th><th scope="col">Vehicle</th><th scope="col">Date Received</th><th scope="col">Date Completed</th><th scope="col">Status</th></tr>
            </thead>
            <tbody>
              {rows.slice(0, 500).map((workshop) => {
                const vehicle = workshop.vmfCode === null ? undefined : vehicleByCode.get(workshop.vmfCode);
                const closed = workshop.completeDate !== null || workshop.completeTime !== null;
                return (
                  <tr key={workshop.wwCode}>
                    <td>{workshop.wwCode}</td>
                    <td>{vehicle ? `${valueOrDash(vehicle.fleetNumber)} / ${valueOrDash(vehicle.registrationNumber)}` : `VMF ${valueOrDash(workshop.vmfCode)}`}</td>
                    <td>{formatDate(workshop.receiveDate)}</td>
                    <td>{formatDate(workshop.completeDate)}</td>
                    <td>{closed ? "Closed" : "Open"}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

export default async function WorkshopPage({ searchParams }: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/workshop" /></main>;
  if (session.status === "unavailable") return <main className="page-shell vehicle-page-shell"><SessionRecovery returnPath="/workshop" /></main>;
  if (!hasRole(session.roles, WORKSHOP_ROLE)) return <main className="page-shell vehicle-page-shell"><AccessRestricted /></main>;

  const query = await searchParams;
  const search = Array.isArray(query.search) ? query.search[0] ?? "" : query.search ?? "";
  const status = Array.isArray(query.status) ? query.status[0] ?? "" : query.status ?? "";
  let workshops: WorkshopRecord[] = [];
  let vehicles: WorkshopVehicle[] = [];
  let unavailable = false;
  try {
    [workshops, vehicles] = await Promise.all([getWorkshops(), getWorkshopVehicles()]);
  } catch (error) {
    unavailable = error instanceof WorkshopApiError && error.reason === "unavailable";
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="workshop-title">
        <header className="vehicle-page-header">
          <div><p className="eyebrow">Workshop</p><h1 id="workshop-title">Workshop Maintenance Menu</h1><p>Capture workshop entries, reopen closed job cards, and maintain workshop merchants.</p></div>
          <Link className="button button-secondary" href="/home">Home</Link>
        </header>
        <div className="vehicle-menu-tiles">
          <section className="vehicle-menu-tile"><h2 className="vehicle-menu-header">Workshop Maintenance Information / Help</h2><div className="vehicle-menu-body"><Link className="vehicle-menu-link" href="/workshop/help">Workshop Maintenance Information / Help</Link></div></section>
          <section className="vehicle-menu-tile"><h2 className="vehicle-menu-header">Workshop Section</h2><div className="vehicle-menu-body"><Link className="vehicle-menu-link" href="/workshop/entry">1) Enter a WorkShop Entry</Link><Link className="vehicle-menu-link" href="/workshop/open-job-card">2) OPEN a CLOSED Job Card</Link><Link className="vehicle-menu-link" href="/workshop/merchant">3) Enter / Update a Merchant</Link></div></section>
        </div>
        {unavailable ? <ApiUnavailable /> : <WorkshopSnapshot workshops={workshops} vehicles={vehicles} search={search} status={status} />}
      </section>
    </main>
  );
}
