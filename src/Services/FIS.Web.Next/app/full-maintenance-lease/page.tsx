import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import {
  AccessRestricted,
  ActionNotice,
  ApiUnavailable,
  FmlFrame,
  formatDate,
  getStatusClass,
  getStatusLabel,
  hasFmlPermission,
  termNotes,
  vehicleLabel,
  valueOrDash,
} from "@/app/full-maintenance-lease/_components";
import { FmlApiError, getLeaseTerms, type LeaseTermRecord } from "@/lib/api-fml";
import { getVehicleOptions, type VehicleOption } from "@/lib/api-vehicles";
import { getSession } from "@/lib/session";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function firstQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function filterTerms(
  terms: LeaseTermRecord[],
  vehicles: VehicleOption[],
  search: string,
  mode: string,
) {
  const query = search.trim().toLowerCase();
  if (!query) return terms;
  const matchingCodes = new Set(
    vehicles
      .filter((vehicle) => {
        const value =
          mode.toUpperCase() === "GP" ? vehicle.registrationNumber : vehicle.fleetNumber;
        return value?.toLowerCase().includes(query);
      })
      .map((vehicle) => vehicle.vmfCode),
  );
  return terms.filter((term) => matchingCodes.has(term.vmfCode));
}

export default async function FullMaintenanceLeasePage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/full-maintenance-lease" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable message="Full Maintenance Lease could not be opened." />
      </main>
    );
  if (!hasFmlPermission(session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );

  const query = await searchParams;
  const search = firstQueryValue(query.search) ?? "";
  const mode = firstQueryValue(query.mode) ?? "GG";
  const result = firstQueryValue(query.result);
  const message = firstQueryValue(query.message);
  let terms: LeaseTermRecord[] = [];
  let vehicles: VehicleOption[] = [];
  let lookupError = false;

  if (search.trim()) {
    try {
      [terms, vehicles] = await Promise.all([getLeaseTerms(), getVehicleOptions()]);
      terms = filterTerms(terms, vehicles, search, mode);
    } catch (error) {
      lookupError = error instanceof FmlApiError;
    }
  }

  const labels = new Map(vehicles.map((vehicle) => [vehicle.vmfCode, vehicleLabel(vehicle)]));
  return (
    <FmlFrame
      title="Full Maintenance Lease"
      description="Capture, approve, extend, and report on lease vehicle tariffs."
    >
      <ActionNotice result={result} message={message} />
      <div className="vehicle-menu-tiles">
        <section className="vehicle-menu-tile">
          <h2 className="vehicle-menu-header">Full Maintenance Lease Information / Help</h2>
          <div className="vehicle-menu-body">
            <Link className="vehicle-menu-link" href="/full-maintenance-lease/help">
              Open FML information and help
            </Link>
          </div>
        </section>
        <section className="vehicle-menu-tile">
          <h2 className="vehicle-menu-header">Lease Vehicle Section</h2>
          <div className="vehicle-menu-body">
            <Link className="vehicle-menu-link" href="/full-maintenance-lease/tariffs">
              1) Capturing of Tariffs for Lease Vehicles
            </Link>
            <Link className="vehicle-menu-link" href="/full-maintenance-lease/upload">
              2) Import the Tariff File
            </Link>
            <Link className="vehicle-menu-link" href="/full-maintenance-lease/extend">
              3) Extend Latest Lease Tariff Period
            </Link>
            <Link className="vehicle-menu-link" href="/full-maintenance-lease/add-lease">
              4) Add Lease Tariff for In Service Vehicles
            </Link>
            <Link className="vehicle-menu-link" href="/full-maintenance-lease/reports">
              5) FML Reports
            </Link>
          </div>
        </section>
      </div>

      <section className="vehicle-status-maintenance-panel" aria-labelledby="fml-lookup-title">
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Live records</p>
            <h2 id="fml-lookup-title">Quick lease lookup</h2>
          </div>
        </div>
        <form method="get" className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="fml-search">
              Search number
            </label>
            <input
              className="form-input"
              id="fml-search"
              name="search"
              defaultValue={search}
              placeholder="GG fleet number or GP registration"
            />
          </div>
          <div className="form-field">
            <label className="form-label" htmlFor="fml-mode">
              Number type
            </label>
            <select className="form-select" id="fml-mode" name="mode" defaultValue={mode}>
              <option value="GG">GG</option>
              <option value="GP">GP</option>
            </select>
          </div>
          <div className="form-actions form-group-full">
            <button className="button button-primary" type="submit">
              Search
            </button>
            <Link className="button button-secondary" href="/full-maintenance-lease">
              Clear
            </Link>
          </div>
        </form>
        {lookupError ? <ApiUnavailable message="The lease lookup could not be completed." /> : null}
        {!lookupError && search.trim() && terms.length === 0 ? (
          <p className="muted-copy">No lease records matched that vehicle search.</p>
        ) : null}
        {terms.length > 0 ? (
          <div className="vehicle-table-wrapper">
            <table className="vehicle-table">
              <caption className="sr-only">Lease records matching the search</caption>
              <thead>
                <tr>
                  <th scope="col">Lease #</th>
                  <th scope="col">Vehicle</th>
                  <th scope="col">Status</th>
                  <th scope="col">Start</th>
                  <th scope="col">End</th>
                  <th scope="col">Notes</th>
                </tr>
              </thead>
              <tbody>
                {terms.slice(0, 100).map((term) => (
                  <tr key={term.termId}>
                    <td>{term.termId}</td>
                    <td>{labels.get(term.vmfCode) ?? term.vmfCode}</td>
                    <td>
                      <span className={getStatusClass(term.authorityStatus)}>
                        {getStatusLabel(term.authorityStatus)}
                      </span>
                    </td>
                    <td>{formatDate(term.startDate)}</td>
                    <td>{formatDate(term.endDate)}</td>
                    <td>{valueOrDash(termNotes(term))}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}
      </section>
    </FmlFrame>
  );
}
