import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import { getSession } from "@/lib/auth/session";
import {
  getVehicleMasterLookup,
  TroubleshootApiError,
} from "@/lib/api/fleet-operations/api-troubleshoot";
import {
  hasTroubleshootingRole,
  Pagination,
  StatusCard,
  TroubleshootMenu,
  TroubleshootShell,
  valueOrDash,
} from "@/app/(fleet-operations)/troubleshoot/_components";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function first(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}
export default async function VehicleMasterEditPage({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/troubleshoot/vehicle-master-edit" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="API unavailable"
          message="The sign-in service is temporarily unavailable."
          href="/troubleshoot/vehicle-master-edit"
        />
      </main>
    );
  if (!hasTroubleshootingRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <StatusCard
          title="Access restricted"
          message="You do not have permission to access Troubleshoot."
          href="/home"
        />
      </main>
    );

  const query = await searchParams;
  const vehicleIdentifier = (first(query.vehicleIdentifier) ?? "").trim();
  const page = Number(first(query.page)) > 0 ? Number(first(query.page)) : 1;
  let vehicles = [] as Awaited<ReturnType<typeof getVehicleMasterLookup>>;
  let errorMessage: string | null = null;
  if (vehicleIdentifier) {
    try {
      vehicles = await getVehicleMasterLookup(vehicleIdentifier);
    } catch (error) {
      errorMessage =
        error instanceof TroubleshootApiError
          ? error.message
          : "Vehicle details could not be loaded.";
    }
  }
  const pageSize = 12;
  const totalPages = Math.max(1, Math.ceil(vehicles.length / pageSize));
  const currentPage = Math.min(page, totalPages);
  const visibleVehicles = vehicles.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  return (
    <TroubleshootShell
      title="Vehicle Master Edit"
      description="Search vehicles and review vehicle master records for edit operations."
    >
      <TroubleshootMenu />
      <section
        className="vehicle-status-maintenance-panel"
        aria-labelledby="vehicle-master-search-title"
      >
        <div className="vehicle-form-section-header">
          <div>
            <p className="eyebrow">Vehicle lookup</p>
            <h2 id="vehicle-master-search-title">Search vehicle master</h2>
          </div>
        </div>
        <form className="vehicle-create-form" method="get">
          <label className="form-label" htmlFor="vehicle-master-identifier">
            Vehicle GG/Registration
          </label>
          <input
            className="form-input"
            id="vehicle-master-identifier"
            name="vehicleIdentifier"
            defaultValue={vehicleIdentifier}
            required
          />
          <div className="button-row">
            <button className="button button-primary" type="submit">
              Load Vehicle
            </button>
            <a className="button button-secondary" href="/troubleshoot/vehicle-master-edit">
              Clear
            </a>
          </div>
        </form>
      </section>
      {errorMessage ? (
        <div className="notice notice-error" role="alert">
          {errorMessage}
        </div>
      ) : null}
      {!vehicleIdentifier ? (
        <div className="vehicle-empty-state">
          <p>Enter a vehicle identifier to load details.</p>
        </div>
      ) : errorMessage ? null : vehicles.length === 0 ? (
        <div className="vehicle-empty-state">
          <p>No vehicle was found.</p>
        </div>
      ) : (
        <section
          className="vehicle-status-maintenance-panel"
          aria-labelledby="vehicle-master-results-title"
        >
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">
                {vehicles.length} vehicle{vehicles.length === 1 ? "" : "s"}
              </p>
              <h2 id="vehicle-master-results-title">Vehicle master records</h2>
            </div>
          </div>
          <div className="vehicle-table-wrapper">
            <table className="vehicle-table">
              <caption className="sr-only">Vehicle master lookup results</caption>
              <thead>
                <tr>
                  <th scope="col">Vehicle (GG / GP / VMF)</th>
                  <th scope="col">Registration</th>
                  <th scope="col">Current Odometer</th>
                  <th scope="col">Recovered GG</th>
                </tr>
              </thead>
              <tbody>
                {visibleVehicles.map((vehicle) => (
                  <tr key={vehicle.vmfCode}>
                    <td>
                      {valueOrDash(vehicle.fleetNumber)} / {valueOrDash(vehicle.registrationNumber)}{" "}
                      ({vehicle.vmfCode})
                    </td>
                    <td>{valueOrDash(vehicle.registrationNumber)}</td>
                    <td>{valueOrDash(vehicle.currentOdometer)}</td>
                    <td>{valueOrDash(vehicle.recoveredGg)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination
            path="/troubleshoot/vehicle-master-edit"
            page={currentPage}
            totalPages={totalPages}
            query={{ vehicleIdentifier }}
          />
        </section>
      )}
    </TroubleshootShell>
  );
}
