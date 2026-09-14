import Link from "next/link";

import { createJobCardAction } from "@/app/(fleet-operations)/job-cards/actions";
import { JobCardVehicleResults } from "@/app/(fleet-operations)/job-cards/_components";
import { valueOrDash } from "@/app/(fleet-operations)/job-cards/_utils";
import type { ExtraCodeRecord } from "@/lib/api/reference-data/api-extra-codes";
import type { VehicleOption } from "@/lib/api/vehicles/api-vehicles";

type CreateJobCardViewProps = Readonly<{
  errorMessage: string;
  extraCodes: readonly ExtraCodeRecord[];
  matchingVehicles: readonly VehicleOption[];
  saved: boolean;
  search: string;
  vehicle?: VehicleOption;
}>;

export function CreateJobCardView({
  errorMessage,
  extraCodes,
  matchingVehicles,
  saved,
  search,
  vehicle,
}: CreateJobCardViewProps) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="create-job-card-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Job Cards</p>
            <h1 id="create-job-card-title">Create Job Card</h1>
            <p>Select one or more categories and create entries for the selected vehicle.</p>
          </div>
          <Link className="button button-secondary" href="/job-cards/select-vehicle">
            Back to vehicle list
          </Link>
        </header>
        {saved ? (
          <div className="notice notice-success" role="status">
            Job card records created successfully.
          </div>
        ) : null}
        {errorMessage ? (
          <div className="notice notice-error" role="alert">
            {errorMessage}
          </div>
        ) : null}
        {vehicle ? (
          <>
            <VehicleSummary vehicle={vehicle} />
            <CreateJobCardCategories vehicle={vehicle} extraCodes={extraCodes} />
          </>
        ) : (
          <CreateJobCardVehicleSelection search={search} vehicles={matchingVehicles} />
        )}
      </section>
    </main>
  );
}

function CreateJobCardVehicleSelection({
  search,
  vehicles,
}: Readonly<{ search: string; vehicles: readonly VehicleOption[] }>) {
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="create-select-vehicle-title"
    >
      <h2 id="create-select-vehicle-title">Select vehicle</h2>
      <form className="vehicle-search-row" method="get">
        <label className="sr-only" htmlFor="create-job-card-search">
          Vehicle search
        </label>
        <input
          className="vehicle-search"
          id="create-job-card-search"
          name="search"
          defaultValue={search}
          placeholder="GG number, registration, or VMF code"
        />
        <button className="button button-primary" type="submit">
          Search
        </button>
        <Link className="button button-secondary" href="/job-cards/create">
          Clear
        </Link>
      </form>
      {vehicles.length === 0 ? (
        <p className="muted-copy">No matching vehicles found.</p>
      ) : (
        <JobCardVehicleResults caption="Vehicle search results" vehicles={vehicles} />
      )}
    </section>
  );
}

function CreateJobCardCategories({
  extraCodes,
  vehicle,
}: Readonly<{ extraCodes: readonly ExtraCodeRecord[]; vehicle: VehicleOption }>) {
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="job-card-category-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{extraCodes.length} available</p>
          <h2 id="job-card-category-title">Select Job Card Categories</h2>
        </div>
      </div>
      {extraCodes.length === 0 ? (
        <p className="muted-copy">No job categories are configured.</p>
      ) : (
        <form action={createJobCardAction}>
          <input
            name="returnPath"
            type="hidden"
            value={`/job-cards/create?vmfCode=${vehicle.vmfCode}`}
          />
          <input name="vmfCode" type="hidden" value={vehicle.vmfCode} />
          <div className="form-grid">
            {extraCodes
              .filter((item) => !item.isDeleted)
              .sort((left, right) =>
                (left.description ?? "").localeCompare(right.description ?? ""),
              )
              .map((item) => (
                <label className="vehicle-checkbox-label" key={item.extraCode}>
                  <input name="extraCode" type="checkbox" value={item.extraCode} />{" "}
                  {item.description || `Extra code ${item.extraCode}`}
                </label>
              ))}
          </div>
          <div className="button-row">
            <button className="button button-primary" type="submit">
              Create selected job cards
            </button>
            <Link
              className="button button-secondary"
              href={`/job-cards/list?gg=${encodeURIComponent(vehicle.fleetNumber ?? "")}`}
            >
              View existing cards
            </Link>
          </div>
        </form>
      )}
    </section>
  );
}

function VehicleSummary({ vehicle }: Readonly<{ vehicle: VehicleOption }>) {
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="job-card-vehicle-summary-title"
    >
      <p className="eyebrow">Selected vehicle</p>
      <h2 id="job-card-vehicle-summary-title">
        {valueOrDash(vehicle.fleetNumber)} / {valueOrDash(vehicle.registrationNumber)}
      </h2>
      <p className="muted-copy">VMF code {vehicle.vmfCode}</p>
    </section>
  );
}
