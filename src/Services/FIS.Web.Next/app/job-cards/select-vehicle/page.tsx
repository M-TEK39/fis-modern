import Link from "next/link";

import {
  accessRestricted,
  getJobCardSession,
  queryValue,
  sessionMessage,
} from "@/app/job-cards/_page";
import { hasJobCardAccess, hasRole, valueOrDash } from "@/app/job-cards/_components";
import { getVehicleOptions, VehicleApiError } from "@/lib/api-vehicles";

export default async function SelectJobCardVehiclePage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getJobCardSession();
  const problem = sessionMessage(session, "/job-cards/select-vehicle");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasRole(session.roles, "capturer") && !hasJobCardAccess(session.accessLevel, session.roles))
    return accessRestricted("Your profile does not include Job Card capturer access.");
  const query = await searchParams;
  const search = queryValue(query.search).trim();
  try {
    const vehicles = await getVehicleOptions();
    const normalized = search.toLocaleLowerCase();
    const filtered = normalized
      ? vehicles.filter((vehicle) =>
          [vehicle.fleetNumber, vehicle.registrationNumber, vehicle.vmfCode].some((value) =>
            String(value ?? "")
              .toLocaleLowerCase()
              .includes(normalized),
          ),
        )
      : vehicles;
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="select-job-card-vehicle-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Job Cards</p>
              <h1 id="select-job-card-vehicle-title">Select a Vehicle to Create a Job Card</h1>
              <p>Search by GG number or registration, then choose a vehicle.</p>
            </div>
            <Link className="button button-secondary" href="/job-cards/capturer-default">
              Main menu
            </Link>
          </header>
          <form className="vehicle-search-row" method="get">
            <label className="sr-only" htmlFor="job-card-vehicle-search">
              Vehicle search
            </label>
            <input
              className="vehicle-search"
              id="job-card-vehicle-search"
              name="search"
              defaultValue={search}
              placeholder="GG number, registration, or VMF code"
            />
            <button className="button button-primary" type="submit">
              Search
            </button>
            <Link className="button button-secondary" href="/job-cards/select-vehicle">
              Clear
            </Link>
          </form>
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="job-card-vehicle-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">
                  {filtered.length} match{filtered.length === 1 ? "" : "es"}
                </p>
                <h2 id="job-card-vehicle-results-title">Vehicles</h2>
              </div>
            </div>
            {filtered.length === 0 ? (
              <p className="muted-copy">No matching vehicles found.</p>
            ) : (
              <div className="vehicle-table-wrapper">
                <table className="vehicle-table">
                  <caption className="sr-only">Vehicles available for job card creation</caption>
                  <thead>
                    <tr>
                      <th scope="col">VMF code</th>
                      <th scope="col">GG number</th>
                      <th scope="col">Registration</th>
                      <th scope="col">Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filtered.map((vehicle) => (
                      <tr key={vehicle.vmfCode}>
                        <td>{vehicle.vmfCode}</td>
                        <td>{valueOrDash(vehicle.fleetNumber)}</td>
                        <td>{valueOrDash(vehicle.registrationNumber)}</td>
                        <td>
                          <Link
                            className="button button-primary button-small"
                            href={`/job-cards/create?vmfCode=${vehicle.vmfCode}`}
                          >
                            Select
                          </Link>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </section>
      </main>
    );
  } catch (error) {
    const message =
      error instanceof VehicleApiError && error.reason === "unavailable"
        ? "The vehicle service is temporarily unavailable. Please try again."
        : "Vehicles could not be loaded.";
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <h2>{message}</h2>
          <Link className="button button-secondary" href="/job-cards/capturer-default">
            Back
          </Link>
        </section>
      </main>
    );
  }
}
