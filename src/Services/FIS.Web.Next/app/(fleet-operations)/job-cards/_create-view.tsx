import Link from "next/link";

import { createJobCardAction } from "@/app/(fleet-operations)/job-cards/actions";
import { JobCardVehicleResults } from "@/app/(fleet-operations)/job-cards/_components";
import { valueOrDash } from "@/app/(fleet-operations)/job-cards/_utils";
import type { ExtraCodeRecord } from "@/lib/api/reference-data/api-extra-codes";
import type { VehicleOption } from "@/lib/api/vehicles/api-vehicles";
import type { JobCardCaptureVehicleSummary } from "@/lib/api/fleet-operations/api-job-cards";

type CreateJobCardViewProps = Readonly<{
  errorMessage: string;
  extraCodes: readonly ExtraCodeRecord[];
  extrasPreserveOrder: boolean;
  fittedExtras: readonly string[] | null;
  jobcardsOnStatus: readonly string[] | null;
  matchingVehicles: readonly VehicleOption[];
  saved: boolean;
  search: string;
  summary: JobCardCaptureVehicleSummary | null;
  vehicle?: VehicleOption;
}>;

export function CreateJobCardView({
  errorMessage,
  extraCodes,
  extrasPreserveOrder,
  fittedExtras,
  jobcardsOnStatus,
  matchingVehicles,
  saved,
  search,
  summary,
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
            <VehicleSummary summary={summary} vehicle={vehicle} />
            <CreateJobCardCategories
              extraCodes={extraCodes}
              extrasPreserveOrder={extrasPreserveOrder}
              fittedExtras={fittedExtras}
              jobcardsOnStatus={jobcardsOnStatus}
              vehicle={vehicle}
            />
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
  extrasPreserveOrder,
  fittedExtras,
  jobcardsOnStatus,
  vehicle,
}: Readonly<{
  extraCodes: readonly ExtraCodeRecord[];
  extrasPreserveOrder: boolean;
  fittedExtras: readonly string[] | null;
  jobcardsOnStatus: readonly string[] | null;
  vehicle: VehicleOption;
}>) {
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="job-card-category-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{extraCodes.length} available</p>
          <h2 id="job-card-category-title">Select a Job Card and click create</h2>
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
          {fittedExtras ? (
            <details className="vehicle-status-maintenance-panel">
              <summary>View extras on vehicle</summary>
              <p>These are the extras already fitted to the vehicle</p>
              {fittedExtras.length === 0 ? (
                <p className="muted-copy">No extras are fitted to this vehicle.</p>
              ) : (
                <ul>
                  {fittedExtras.map((item, index) => (
                    <li key={`${item}-${index}`}>{item}</li>
                  ))}
                </ul>
              )}
            </details>
          ) : null}
          {jobcardsOnStatus ? (
            <details className="vehicle-status-maintenance-panel">
              <summary>View Jobcards for vehicle</summary>
              <p>Job Cards created for this vehicle</p>
              {jobcardsOnStatus.length === 0 ? (
                <p className="muted-copy">No job cards exist for this vehicle.</p>
              ) : (
                <ul>
                  {jobcardsOnStatus.map((item, index) => (
                    <li key={`${item}-${index}`}>{item}</li>
                  ))}
                </ul>
              )}
            </details>
          ) : null}
          <div className="form-grid">
            {(extrasPreserveOrder
              ? extraCodes.filter((item) => !item.isDeleted)
              : extraCodes
                  .filter((item) => !item.isDeleted)
                  .toSorted((left, right) =>
                    (left.description ?? "").localeCompare(right.description ?? ""),
                  )
            ).map((item) => (
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

function VehicleSummary({
  summary,
  vehicle,
}: Readonly<{ summary: JobCardCaptureVehicleSummary | null; vehicle: VehicleOption }>) {
  const rows = summary
    ? [
        ["GG Number", summary.ggNumber],
        ["Registration Number", summary.registrationNumber],
        ["Class Description", summary.classDescription],
        ["Model Description", summary.modelDescription],
        ["Take on Odo", summary.odoReading],
        ["VIN Number", summary.vinNumber],
        ["Engine Number", summary.engineNumber],
        ["Year Model", summary.yearModel],
        ["Purchased From", summary.purchasedFrom],
        ["Hire Type", summary.hireType],
        ["Hired From", summary.hiredFrom],
        ["Location", summary.location],
      ]
    : [
        ["GG Number", vehicle.fleetNumber],
        ["Registration Number", vehicle.registrationNumber],
        ["VMF code", String(vehicle.vmfCode)],
      ];
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="job-card-vehicle-summary-title"
    >
      <p className="eyebrow">Vehicle Summary</p>
      <h2 id="job-card-vehicle-summary-title">
        {valueOrDash(summary?.ggNumber ?? vehicle.fleetNumber)} /{" "}
        {valueOrDash(summary?.registrationNumber ?? vehicle.registrationNumber)}
      </h2>
      <dl className="form-grid">
        {rows.map(([label, value]) => (
          <div key={label}>
            <dt>{label}</dt>
            <dd>{valueOrDash(value)}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}
