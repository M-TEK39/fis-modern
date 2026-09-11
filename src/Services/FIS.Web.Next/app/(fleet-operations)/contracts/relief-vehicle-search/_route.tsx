import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { createReliefContractAction } from "@/app/(fleet-operations)/contracts/actions";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  ContractApiError,
  getContract,
  searchReliefVehicles,
  type ContractRecord,
  type ReliefVehicleSearchResult,
} from "@/lib/api/finance/api-contracts";
import { getSession } from "@/lib/auth/session";

const CONTRACT_PERMISSION = BigInt(2);

export type ReliefVehicleSearchPageProps = {
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
  if (
    roles.some((role) =>
      ["contracts", "contract", "admin", "administrator"].includes(role.trim().toLowerCase()),
    )
  )
    return true;
  try {
    return accessLevel
      ? (BigInt(accessLevel) & CONTRACT_PERMISSION) === CONTRACT_PERMISSION
      : false;
  } catch {
    return false;
  }
}

function hasLoadAndManageAccess(roles: readonly string[]) {
  return roles.some((role) =>
    [
      "contract (load and manage)",
      "contracts (load and manage)",
      "contract_load_and_manage",
      "contracts_load_and_manage",
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

function parentContractId(query: Record<string, string | string[] | undefined>) {
  return positiveInt(
    getQueryValue(query.contractId) ??
      getQueryValue(query.relief_for_contract) ??
      getQueryValue(query.reliefForContract),
  );
}

function filterMatches(
  vehicles: ReliefVehicleSearchResult[],
  searchType: "GG" | "GP",
  searchQuery: string,
) {
  const normalized = searchQuery.toLocaleLowerCase();
  return vehicles.filter((vehicle) => {
    const value = searchType === "GG" ? vehicle.fleetNumber : vehicle.registrationNumber;
    return value?.toLocaleLowerCase().includes(normalized) === true;
  });
}

function ParentSummary({ contract }: Readonly<{ contract: ContractRecord }>) {
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="relief-parent-title">
      <p className="eyebrow">Existing active contract</p>
      <h2 id="relief-parent-title">Relief for contract {contract.contractCode}</h2>
      <p>
        {valueOrDash(contract.fleetNumber)} / {valueOrDash(contract.registrationNumber)} at{" "}
        {valueOrDash(contract.siteDescription)} ({contract.siteCode}).
      </p>
      <p className="muted-copy">
        The relief record is created for the parent contract's site and remains linked through the
        legacy relief_for_contract field.
      </p>
    </section>
  );
}

function ReliefResults({
  contract,
  vehicles,
}: Readonly<{ contract: ContractRecord; vehicles: ReliefVehicleSearchResult[] }>) {
  if (vehicles.length === 0)
    return (
      <section className="vehicle-status-maintenance-panel">
        <p className="muted-copy">No available vehicles matched this search.</p>
      </section>
    );
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="relief-results-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">
            {vehicles.length} match{vehicles.length === 1 ? "" : "es"}
          </p>
          <h2 id="relief-results-title">Select vehicle to be supplied as relief</h2>
        </div>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">Available relief vehicles</caption>
          <thead>
            <tr>
              <th scope="col">GG number</th>
              <th scope="col">Registration</th>
              <th scope="col">Start odometer</th>
              <th scope="col">Target return</th>
              <th scope="col">Reason</th>
              <th scope="col">Action</th>
            </tr>
          </thead>
          <tbody>
            {vehicles.map((vehicle) => (
              <tr key={vehicle.vmfCode}>
                <td>{valueOrDash(vehicle.fleetNumber)}</td>
                <td>{valueOrDash(vehicle.registrationNumber)}</td>
                <td>
                  <label className="sr-only" htmlFor={`relief-odo-${vehicle.vmfCode}`}>
                    Start odometer for vehicle {vehicle.vmfCode}
                  </label>
                  <input
                    className="form-input"
                    id={`relief-odo-${vehicle.vmfCode}`}
                    min="0"
                    name="startOdometer"
                    form={`relief-form-${vehicle.vmfCode}`}
                    type="number"
                  />
                </td>
                <td>
                  <label className="sr-only" htmlFor={`relief-date-${vehicle.vmfCode}`}>
                    Target return date for vehicle {vehicle.vmfCode}
                  </label>
                  <input
                    className="form-input"
                    id={`relief-date-${vehicle.vmfCode}`}
                    name="targetReturnDate"
                    form={`relief-form-${vehicle.vmfCode}`}
                    type="date"
                  />
                </td>
                <td>
                  <label className="sr-only" htmlFor={`relief-reason-${vehicle.vmfCode}`}>
                    Reason for vehicle {vehicle.vmfCode}
                  </label>
                  <input
                    className="form-input"
                    id={`relief-reason-${vehicle.vmfCode}`}
                    maxLength={1000}
                    name="reason"
                    form={`relief-form-${vehicle.vmfCode}`}
                    defaultValue="Assign as relief vehicle"
                  />
                </td>
                <td>
                  <form action={createReliefContractAction} id={`relief-form-${vehicle.vmfCode}`}>
                    <input name="contractId" type="hidden" value={contract.contractCode} />
                    <input name="reliefVmfCode" type="hidden" value={vehicle.vmfCode} />
                    <input
                      name="returnPath"
                      type="hidden"
                      value={`/contracts/relief-vehicle-search?contractId=${contract.contractCode}`}
                    />
                    <button className="button button-primary button-small" type="submit">
                      Create relief
                    </button>
                  </form>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function ApiUnavailable({ routePath }: Readonly<{ routePath: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>Relief vehicle search could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href={routePath}>
        Try again
      </Link>
    </section>
  );
}

async function ReliefVehicleSearchPageContent({
  searchParams,
  routePath = "/contracts/relief-vehicle-search",
}: ReliefVehicleSearchPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  if (!hasContractAccess(session.accessLevel, session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to manage vehicle contracts.</h2>
        </section>
      </main>
    );

  const query = await searchParams;
  const contractId = parentContractId(query);
  if (!contractId)
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Parent contract required</p>
          <h2>Select an active contract before creating relief.</h2>
          <Link className="button button-secondary" href="/contracts/maintenance">
            Back to Contracts
          </Link>
        </section>
      </main>
    );

  const searchType = getQueryValue(query.searchType) === "GP" ? "GP" : "GG";
  const searchQuery = (getQueryValue(query.searchQuery) ?? getQueryValue(query.query) ?? "")
    .trim()
    .slice(0, 30);
  const notice =
    getQueryValue(query.success) === "relief-created"
      ? "Relief contract created successfully."
      : getQueryValue(query.error);

  try {
    const [contract, reliefVehicles] = await Promise.all([
      getContract(contractId),
      searchQuery
        ? searchReliefVehicles(searchQuery)
        : Promise.resolve([] as ReliefVehicleSearchResult[]),
    ]);
    const vehicles = searchQuery ? filterMatches(reliefVehicles, searchType, searchQuery) : [];
    const canManage = hasLoadAndManageAccess(session.roles);
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="relief-search-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Vehicle contract management</p>
              <h1 id="relief-search-title">Create Relief Contract</h1>
              <p>Search for an available vehicle and link it to the active contract.</p>
            </div>
            <Link
              className="button button-secondary"
              href={`/contracts/detail?contractId=${contract.contractCode}`}
            >
              Cancel
            </Link>
          </header>
          {notice ? (
            <div
              className={
                getQueryValue(query.error) ? "notice notice-error" : "notice notice-success"
              }
              role={getQueryValue(query.error) ? "alert" : "status"}
            >
              {notice}
            </div>
          ) : null}
          <ParentSummary contract={contract} />
          <form className="vehicle-status-maintenance-panel" method="get">
            <input name="contractId" type="hidden" value={contract.contractCode} />
            <fieldset className="vehicle-search-options">
              <legend>Find relief vehicle by</legend>
              <label className="vehicle-checkbox-label">
                <input
                  name="searchType"
                  type="radio"
                  value="GG"
                  defaultChecked={searchType === "GG"}
                />
                GG number
              </label>
              <label className="vehicle-checkbox-label">
                <input
                  name="searchType"
                  type="radio"
                  value="GP"
                  defaultChecked={searchType === "GP"}
                />
                Registration number
              </label>
            </fieldset>
            <div className="vehicle-search-row">
              <label className="sr-only" htmlFor="relief-vehicle-search">
                {searchType === "GG" ? "GG number" : "Registration number"}
              </label>
              <input
                className="vehicle-search"
                id="relief-vehicle-search"
                maxLength={30}
                name="searchQuery"
                placeholder={searchType === "GG" ? "Enter GG number" : "Enter registration number"}
                defaultValue={searchQuery}
              />
              <button className="button button-primary" type="submit">
                Search
              </button>
              <Link
                className="button button-secondary"
                href={`/contracts/relief-vehicle-search?contractId=${contract.contractCode}`}
              >
                Clear
              </Link>
            </div>
          </form>
          {!canManage ? (
            <div className="notice notice-error" role="alert">
              Your account can view contracts but cannot create relief contracts.
            </div>
          ) : null}
          {searchQuery && canManage ? (
            <ReliefResults contract={contract} vehicles={vehicles} />
          ) : null}
          <div className="vehicle-footer-actions">
            <Link
              className="button button-secondary"
              href={`/contracts/detail?contractId=${contract.contractCode}`}
            >
              Back to Contract
            </Link>
            <Link className="button button-secondary" href="/contracts">
              Contracts Menu
            </Link>
          </div>
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof ContractApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    if (error instanceof ContractApiError && error.reason === "not-found")
      return (
        <main className="page-shell vehicle-page-shell">
          <section className="vehicle-status-card" role="alert">
            <p className="eyebrow">Record not found</p>
            <h2>The parent contract was not found.</h2>
            <Link className="button button-secondary" href="/contracts/maintenance">
              Back to Contracts
            </Link>
          </section>
        </main>
      );
    console.error(
      "FIS relief vehicle request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  }
}

export function ReliefVehicleSearchRoute(props: ReliefVehicleSearchPageProps) {
  return (
    <StreamedRoute>
      <ReliefVehicleSearchPageContent {...props} />
    </StreamedRoute>
  );
}

export default function ReliefVehicleSearchPage({
  searchParams,
}: Pick<ReliefVehicleSearchPageProps, "searchParams">) {
  return <ReliefVehicleSearchRoute searchParams={searchParams} />;
}
