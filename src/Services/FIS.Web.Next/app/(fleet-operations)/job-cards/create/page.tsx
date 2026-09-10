import Link from "next/link";

import { createJobCardAction } from "@/app/(fleet-operations)/job-cards/actions";
import {
  accessRestricted,
  getJobCardSession,
  queryValue,
  sessionMessage,
} from "@/app/(fleet-operations)/job-cards/_page";
import {
  hasJobCardAccess,
  hasRole,
  valueOrDash,
} from "@/app/(fleet-operations)/job-cards/_components";
import { getExtraCodes, ExtraCodeApiError } from "@/lib/api/reference-data/api-extra-codes";
import {
  getVehicleOptions,
  VehicleApiError,
  type VehicleOption,
} from "@/lib/api/vehicles/api-vehicles";

export default async function CreateJobCardPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  const session = await getJobCardSession();
  const problem = sessionMessage(session, "/job-cards/create");
  if (problem) return problem;
  if (session.status !== "authenticated")
    return accessRestricted("Your session could not be loaded.");
  if (!hasRole(session.roles, "capturer") && !hasJobCardAccess(session.accessLevel, session.roles))
    return accessRestricted("Your profile does not include Job Card capturer access.");

  const query = await searchParams;
  const vmfCode = Number(queryValue(query.vmfCode));
  const search = queryValue(query.search).trim();
  const saved = queryValue(query.saved) === "1";
  const errorMessage = queryValue(query.error);
  try {
    const [vehicles, extraCodes] = await Promise.all([getVehicleOptions(), getExtraCodes()]);
    const normalized = search.toLocaleLowerCase();
    const matchingVehicles = normalized
      ? vehicles.filter((vehicle) =>
          [vehicle.fleetNumber, vehicle.registrationNumber, vehicle.vmfCode].some((value) =>
            String(value ?? "")
              .toLocaleLowerCase()
              .includes(normalized),
          ),
        )
      : vehicles;
    const vehicle =
      Number.isInteger(vmfCode) && vmfCode > 0
        ? vehicles.find((item) => item.vmfCode === vmfCode)
        : null;
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
            <VehicleSummary vehicle={vehicle} />
          ) : (
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
              {matchingVehicles.length === 0 ? (
                <p className="muted-copy">No matching vehicles found.</p>
              ) : (
                <div className="vehicle-table-wrapper">
                  <table className="vehicle-table">
                    <caption className="sr-only">Vehicle search results</caption>
                    <thead>
                      <tr>
                        <th scope="col">VMF code</th>
                        <th scope="col">GG number</th>
                        <th scope="col">Registration</th>
                        <th scope="col">Action</th>
                      </tr>
                    </thead>
                    <tbody>
                      {matchingVehicles.map((item) => (
                        <tr key={item.vmfCode}>
                          <td>{item.vmfCode}</td>
                          <td>{valueOrDash(item.fleetNumber)}</td>
                          <td>{valueOrDash(item.registrationNumber)}</td>
                          <td>
                            <Link
                              className="button button-primary button-small"
                              href={`/job-cards/create?vmfCode=${item.vmfCode}`}
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
          )}
          {vehicle ? (
            <section
              className="vehicle-status-maintenance-panel"
              aria-labelledby="job-card-category-title"
            >
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
                    <div className="form-field">
                      <label className="form-label" htmlFor="job-card-create-priority">
                        Priority
                      </label>
                      <select
                        className="form-select"
                        id="job-card-create-priority"
                        name="priority"
                        defaultValue="N"
                      >
                        <option value="N">Normal</option>
                        <option value="H">High</option>
                      </select>
                    </div>
                    <div className="form-field form-group-full">
                      <label className="form-label" htmlFor="job-card-create-comment">
                        Job card comment
                      </label>
                      <textarea
                        className="form-input"
                        id="job-card-create-comment"
                        name="jcsComment"
                        maxLength={2000}
                        rows={3}
                        placeholder="Describe the required work"
                      />
                    </div>
                    <div className="form-field form-group-full">
                      <label className="form-label" htmlFor="job-card-create-damages">
                        Damages
                      </label>
                      <textarea
                        className="form-input"
                        id="job-card-create-damages"
                        name="damages"
                        maxLength={2000}
                        rows={3}
                      />
                    </div>
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
          ) : null}
        </section>
      </main>
    );
  } catch (error) {
    const message =
      error instanceof VehicleApiError || error instanceof ExtraCodeApiError
        ? "Vehicle or job card categories could not be loaded."
        : "The create Job Card page could not be loaded.";
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
