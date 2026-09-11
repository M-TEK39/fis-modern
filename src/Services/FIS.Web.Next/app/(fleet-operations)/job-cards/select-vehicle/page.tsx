import Link from "next/link";

import { JobCardVehicleResults } from "@/app/(fleet-operations)/job-cards/_components";
import {
  AccessRestricted,
  JobCardPageBoundary,
  SessionProblem,
} from "@/app/(fleet-operations)/job-cards/_page";
import { getJobCardSession, queryValue } from "@/app/(fleet-operations)/job-cards/_page-utils";
import { hasJobCardAccess, hasRole } from "@/app/(fleet-operations)/job-cards/_utils";
import { getVehicleOptions, VehicleApiError } from "@/lib/api/vehicles/api-vehicles";

export default function SelectJobCardVehiclePage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <JobCardPageBoundary>
      <SelectJobCardVehicleContent searchParams={searchParams} />
    </JobCardPageBoundary>
  );
}

async function SelectJobCardVehicleContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getJobCardSession();
  if (session.status === "expired" || session.status === "unavailable")
    return <SessionProblem returnPath="/job-cards/select-vehicle" />;
  if (session.status !== "authenticated")
    return <AccessRestricted message="Your session could not be loaded." />;
  if (!hasRole(session.roles, "capturer") && !hasJobCardAccess(session.accessLevel, session.roles))
    return <AccessRestricted message="Your profile does not include Job Card capturer access." />;
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
              <JobCardVehicleResults
                caption="Vehicles available for job card creation"
                vehicles={filtered}
              />
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
