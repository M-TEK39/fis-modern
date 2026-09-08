import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { updateContractHistoryAction } from "@/app/contracts/actions";
import SessionRecovery from "@/app/home/session-recovery";
import {
  ContractApiError,
  getContract,
  getContractPage,
  searchContractVehicles,
  type ContractRecord,
  type ContractVehicleSearchResult,
} from "@/lib/api-contracts";
import { getSession } from "@/lib/session";

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function positiveInt(value: string | undefined) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function hasHistoryRole(roles: readonly string[]) {
  return roles.some((role) =>
    [
      "contract history back dating",
      "contract_history_backdating",
      "admin",
      "administrator",
    ].includes(role.trim().toLowerCase()),
  );
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function dateInputValue(value: string | null) {
  return value?.slice(0, 10) || "";
}

function vehicleDetailHref(vehicle: ContractVehicleSearchResult) {
  const params = new URLSearchParams({ vmfCode: String(vehicle.vmfCode) });
  if (vehicle.fleetNumber) params.set("ggnumber", vehicle.fleetNumber);
  if (vehicle.registrationNumber) params.set("regnumber", vehicle.registrationNumber);
  return `/contracts/backdating-history?${params.toString()}`;
}

function contractDetailHref(contract: ContractRecord) {
  return `/contracts/backdating-history?contractId=${contract.contractCode}&vmfCode=${contract.vmfCode}`;
}

function HistoryEditForm({ contract }: Readonly<{ contract: ContractRecord }>) {
  return (
    <form action={updateContractHistoryAction} className="vehicle-status-maintenance-panel">
      <input name="contractId" type="hidden" value={contract.contractCode} />
      <input
        name="returnPath"
        type="hidden"
        value={`/contracts/backdating-history?contractId=${contract.contractCode}&vmfCode=${contract.vmfCode}`}
      />
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Legacy history fields</p>
          <h2>Update contract {contract.contractCode}</h2>
          <p>This role-restricted form updates the existing contract dates and odometers only.</p>
        </div>
      </div>
      <div className="form-grid">
        <div className="form-field">
          <label className="form-label" htmlFor="history-start-date">
            Start date
          </label>
          <input
            className="form-input"
            id="history-start-date"
            name="startDate"
            type="date"
            defaultValue={dateInputValue(contract.startDate)}
            required
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="history-end-date">
            End date
          </label>
          <input
            className="form-input"
            id="history-end-date"
            name="endDate"
            type="date"
            defaultValue={dateInputValue(contract.endDate)}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="history-start-odometer">
            Start odometer
          </label>
          <input
            className="form-input"
            id="history-start-odometer"
            min="0"
            name="startOdometer"
            type="number"
            defaultValue={contract.startOdometer ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="history-end-odometer">
            End odometer
          </label>
          <input
            className="form-input"
            id="history-end-odometer"
            min="0"
            name="endOdometer"
            type="number"
            defaultValue={contract.endOdometer ?? ""}
          />
        </div>
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Save history changes
        </button>
        <Link
          className="button button-secondary"
          href={`/contracts/backdating-history?vmfCode=${contract.vmfCode}`}
        >
          Cancel
        </Link>
      </div>
    </form>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>Contract history could not be loaded.</h2>
      <p className="muted-copy">Retry when the FIS API is available.</p>
      <Link className="button button-primary" href="/contracts/backdating-history">
        Try again
      </Link>
    </section>
  );
}

export default async function BackdatingHistoryPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/contracts/backdating-history" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  if (!hasHistoryRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to backdate contract history.</h2>
        </section>
      </main>
    );

  const query = await searchParams;
  const searchType = getQueryValue(query.searchType) === "GP" ? "GP" : "GG";
  const searchQuery = (
    getQueryValue(query.searchQuery) ??
    getQueryValue(query.ggnumber) ??
    getQueryValue(query.regnumber) ??
    ""
  )
    .trim()
    .slice(0, 20);
  const requestedVmfCode = positiveInt(getQueryValue(query.vmfCode));
  const requestedContractId = positiveInt(getQueryValue(query.contractId));
  let vehicles: ContractVehicleSearchResult[] = [];
  let contracts: ContractRecord[] = [];
  let selectedContract: ContractRecord | null = null;
  let errorMessage: string | undefined;

  try {
    const [matches, loadedSelectedContract] = await Promise.all([
      searchQuery
        ? searchContractVehicles(searchQuery)
        : Promise.resolve([] as ContractVehicleSearchResult[]),
      requestedContractId ? getContract(requestedContractId) : Promise.resolve(null),
    ]);
    vehicles = matches.filter((vehicle) =>
      (searchType === "GG" ? vehicle.fleetNumber : vehicle.registrationNumber)
        ?.toLocaleLowerCase()
        .includes(searchQuery.toLocaleLowerCase()),
    );
    selectedContract = loadedSelectedContract;
    const vmfCode = requestedVmfCode ?? selectedContract?.vmfCode ?? null;
    if (vmfCode) {
      contracts = (await getContractPage({ page: 1, pageSize: 100, vmfCode })).items;
      if (selectedContract)
        selectedContract =
          contracts.find((contract) => contract.contractCode === selectedContract?.contractCode) ??
          selectedContract;
    }
  } catch (error) {
    if (error instanceof ContractApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/contracts/backdating-history" />
        </main>
      );
    errorMessage =
      error instanceof ContractApiError && error.reason === "not-found"
        ? "The requested contract was not found."
        : "Contract history could not be loaded.";
  }

  const notice =
    getQueryValue(query.updated) === "1"
      ? "Contract history updated successfully."
      : (getQueryValue(query.error) ?? null);
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="backdating-history-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Contract maintenance</p>
            <h1 id="backdating-history-title">Vehicle Contract History Backdating Management</h1>
            <p>
              Review historical contracts by vehicle and update the established date and odometer
              fields.
            </p>
          </div>
          <Link className="button button-secondary" href="/contracts">
            Contracts Menu
          </Link>
        </header>
        {notice ? (
          <div
            className={getQueryValue(query.error) ? "notice notice-error" : "notice notice-success"}
            role={getQueryValue(query.error) ? "alert" : "status"}
          >
            {notice}
          </div>
        ) : null}
        {errorMessage ? (
          <div className="notice notice-error" role="alert">
            {errorMessage}
          </div>
        ) : null}
        <form className="vehicle-status-maintenance-panel" method="get">
          <fieldset className="vehicle-search-options">
            <legend>Select a vehicle to manage</legend>
            <label className="vehicle-checkbox-label">
              <input
                name="searchType"
                type="radio"
                value="GG"
                defaultChecked={searchType === "GG"}
              />{" "}
              GG number
            </label>
            <label className="vehicle-checkbox-label">
              <input
                name="searchType"
                type="radio"
                value="GP"
                defaultChecked={searchType === "GP"}
              />{" "}
              Registration number
            </label>
          </fieldset>
          <div className="vehicle-search-row">
            <label className="sr-only" htmlFor="history-vehicle-search">
              {searchType === "GG" ? "GG number" : "Registration number"}
            </label>
            <input
              className="vehicle-search"
              id="history-vehicle-search"
              maxLength={20}
              name="searchQuery"
              defaultValue={searchQuery}
              placeholder={searchType === "GG" ? "Enter GG number" : "Enter registration number"}
            />
            <button className="button button-primary" type="submit">
              Search
            </button>
            <Link className="button button-secondary" href="/contracts/backdating-history">
              Clear
            </Link>
          </div>
        </form>
        {vehicles.length > 0 ? (
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="history-vehicle-results-title"
          >
            <p className="eyebrow">Vehicle search results</p>
            <h2 id="history-vehicle-results-title">Select a vehicle</h2>
            <div className="vehicle-table-wrapper">
              <table className="vehicle-table">
                <caption className="sr-only">
                  Vehicles matched for contract history backdating
                </caption>
                <thead>
                  <tr>
                    <th scope="col">GG number</th>
                    <th scope="col">Registration</th>
                    <th scope="col">Action</th>
                  </tr>
                </thead>
                <tbody>
                  {vehicles.map((vehicle) => (
                    <tr key={vehicle.vmfCode}>
                      <td>{valueOrDash(vehicle.fleetNumber)}</td>
                      <td>{valueOrDash(vehicle.registrationNumber)}</td>
                      <td>
                        <Link
                          className="button button-secondary button-small"
                          href={vehicleDetailHref(vehicle)}
                        >
                          Load history
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        ) : null}
        {requestedVmfCode ? (
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="history-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">
                  {contracts.length} record{contracts.length === 1 ? "" : "s"}
                </p>
                <h2 id="history-results-title">Contract history</h2>
              </div>
            </div>
            {contracts.length === 0 ? (
              <p className="muted-copy">No Contract History Found</p>
            ) : (
              <div className="vehicle-table-wrapper">
                <table className="vehicle-table">
                  <caption className="sr-only">Contract history for selected vehicle</caption>
                  <thead>
                    <tr>
                      <th scope="col">Contract</th>
                      <th scope="col">Start date</th>
                      <th scope="col">End date</th>
                      <th scope="col">Start odometer</th>
                      <th scope="col">End odometer</th>
                      <th scope="col">Status</th>
                      <th scope="col">Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {contracts.map((contract) => (
                      <tr key={contract.contractCode}>
                        <td>{contract.contractCode}</td>
                        <td>{formatDate(contract.startDate)}</td>
                        <td>{formatDate(contract.endDate)}</td>
                        <td>{valueOrDash(contract.startOdometer)}</td>
                        <td>{valueOrDash(contract.endOdometer)}</td>
                        <td>{contract.contractStatusCode ?? valueOrDash(contract.stillCurrent)}</td>
                        <td>
                          <Link
                            className="button button-secondary button-small"
                            href={contractDetailHref(contract)}
                          >
                            View
                          </Link>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        ) : null}
        {selectedContract ? <HistoryEditForm contract={selectedContract} /> : null}
        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href="/contracts/backdating-approval">
            Backdating approvals
          </Link>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </div>
      </section>
    </main>
  );
}
