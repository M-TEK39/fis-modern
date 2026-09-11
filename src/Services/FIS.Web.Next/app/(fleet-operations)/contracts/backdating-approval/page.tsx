import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  ContractApiError,
  getContractPage,
  searchContractVehicles,
  type ContractRecord,
  type ContractVehicleSearchResult,
} from "@/lib/api/finance/api-contracts";
import { getSession } from "@/lib/auth/session";

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function hasApproverRole(roles: readonly string[]) {
  return roles.some((role) =>
    [
      "contracts approver",
      "contracts_approver",
      "back dating contract (approver)",
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

function detailHref(contract: ContractRecord) {
  const params = new URLSearchParams({
    contractId: String(contract.contractCode),
    action: "review",
  });
  if (contract.fleetNumber) params.set("ggnumber", contract.fleetNumber);
  if (contract.registrationNumber) params.set("regnumber", contract.registrationNumber);
  return `/contracts/backdating-approval/detail?${params.toString()}`;
}

function vehicleDetailHref(vehicle: ContractVehicleSearchResult) {
  const params = new URLSearchParams({ vmfCode: String(vehicle.vmfCode), action: "review" });
  if (vehicle.fleetNumber) params.set("ggnumber", vehicle.fleetNumber);
  if (vehicle.registrationNumber) params.set("regnumber", vehicle.registrationNumber);
  return `/contracts/backdating-approval/detail?${params.toString()}`;
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>Backdating approvals could not be loaded.</h2>
      <p className="muted-copy">Retry when the FIS API is available.</p>
      <Link className="button button-primary" href="/contracts/backdating-approval">
        Try again
      </Link>
    </section>
  );
}

function ApprovalQueue({ contracts }: Readonly<{ contracts: ContractRecord[] }>) {
  if (contracts.length === 0) {
    return (
      <div className="vehicle-empty-state">
        <p className="eyebrow">Approval queue</p>
        <h2>No pending backdating requests</h2>
        <p className="muted-copy">
          Pending requests use the legacy Pending Review contract status.
        </p>
      </div>
    );
  }

  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Pending vehicle contract backdating requests</caption>
        <thead>
          <tr>
            <th scope="col">Contract</th>
            <th scope="col">GG number</th>
            <th scope="col">Registration</th>
            <th scope="col">Site</th>
            <th scope="col">Start date</th>
            <th scope="col">Submitted</th>
            <th scope="col">Action</th>
          </tr>
        </thead>
        <tbody>
          {contracts.map((contract) => (
            <tr key={contract.contractCode}>
              <td>{contract.contractCode}</td>
              <td>{valueOrDash(contract.fleetNumber)}</td>
              <td>{valueOrDash(contract.registrationNumber)}</td>
              <td>
                {valueOrDash(contract.siteDescription)} ({contract.siteCode})
              </td>
              <td>{formatDate(contract.startDate)}</td>
              <td>{formatDate(contract.captureDate ?? contract.dateCreated)}</td>
              <td>
                <Link className="button button-primary button-small" href={detailHref(contract)}>
                  Review
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

async function BackdatingApprovalPageContent({
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
        <SessionRecovery returnPath="/contracts/backdating-approval" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable />
      </main>
    );
  if (!hasApproverRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to approve backdated contracts.</h2>
        </section>
      </main>
    );

  const query = await searchParams;
  const searchType = getQueryValue(query.searchType) === "GP" ? "GP" : "GG";
  const searchQuery = (getQueryValue(query.searchQuery) ?? "").trim().slice(0, 20);
  let pending: ContractRecord[] = [];
  let vehicles: ContractVehicleSearchResult[] = [];
  let errorMessage: string | undefined;

  try {
    if (!searchQuery) {
      pending = (await getContractPage({ page: 1, pageSize: 100, statusCode: 1 })).items;
    } else {
      const matches = await searchContractVehicles(searchQuery);
      vehicles = matches.filter((vehicle) =>
        (searchType === "GG" ? vehicle.fleetNumber : vehicle.registrationNumber)
          ?.toLocaleLowerCase()
          .includes(searchQuery.toLocaleLowerCase()),
      );
      pending = (
        await Promise.all(
          vehicles
            .slice(0, 10)
            .map((vehicle) =>
              getContractPage({ page: 1, pageSize: 100, statusCode: 1, vmfCode: vehicle.vmfCode }),
            ),
        )
      ).flatMap((page) => page.items);
    }
  } catch (error) {
    if (error instanceof ContractApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/contracts/backdating-approval" />
        </main>
      );
    errorMessage = "Backdating approval records could not be loaded.";
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="backdating-approval-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Contract maintenance</p>
            <h1 id="backdating-approval-title">Vehicle Backdating Contract Management</h1>
            <p>
              Review pending backdated contract requests using the established approval workflow.
            </p>
          </div>
          <Link className="button button-secondary" href="/contracts">
            Contracts Menu
          </Link>
        </header>
        <form className="vehicle-status-maintenance-panel" method="get">
          <fieldset className="vehicle-search-options">
            <legend>Select a vehicle to filter</legend>
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
            <label className="sr-only" htmlFor="backdating-search">
              {searchType === "GG" ? "GG number" : "Registration number"}
            </label>
            <input
              className="vehicle-search"
              id="backdating-search"
              maxLength={20}
              name="searchQuery"
              defaultValue={searchQuery}
              placeholder={searchType === "GG" ? "Enter GG number" : "Enter registration number"}
            />
            <button className="button button-primary" type="submit">
              Search
            </button>
            <Link className="button button-secondary" href="/contracts/backdating-approval">
              Load queue
            </Link>
          </div>
        </form>
        {errorMessage ? (
          <div className="notice notice-error" role="alert">
            {errorMessage}
          </div>
        ) : null}
        {vehicles.length > 0 ? (
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="backdating-vehicle-results-title"
          >
            <p className="eyebrow">Vehicle search results</p>
            <h2 id="backdating-vehicle-results-title">Open a vehicle review</h2>
            <div className="vehicle-table-wrapper">
              <table className="vehicle-table">
                <caption className="sr-only">Vehicles matched for backdating approval</caption>
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
                          Open review
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        ) : null}
        <section
          className="vehicle-status-maintenance-panel"
          aria-labelledby="backdating-queue-title"
        >
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">
                {pending.length} request{pending.length === 1 ? "" : "s"}
              </p>
              <h2 id="backdating-queue-title">Pending authorisation on backdating contracts</h2>
            </div>
          </div>
          <ApprovalQueue contracts={pending} />
        </section>
        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href="/contracts/backdating-history">
            Contract history backdating
          </Link>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </div>
      </section>
    </main>
  );
}

export default function BackdatingApprovalPage(
  props: Readonly<{
    searchParams: Promise<Record<string, string | string[] | undefined>>;
  }>,
) {
  return (
    <StreamedRoute>
      <BackdatingApprovalPageContent {...props} />
    </StreamedRoute>
  );
}
