import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";

import { ContractVehicleSearchFieldset } from "@/app/(fleet-operations)/contracts/_components";
import ContractVehicleResults from "@/components/ui/contract-vehicle-results";
import type { BackdatingApprovalData } from "./_data";
import type { ContractRecord, ContractVehicleSearchResult } from "@/lib/api/finance/api-contracts";

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

export function BackdatingApprovalUnavailable() {
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
        <DataTableHeader
          columns={[
            { key: "column-1", label: <>Contract</> },
            { key: "column-2", label: <>GG number</> },
            { key: "column-3", label: <>Registration</> },
            { key: "column-4", label: <>Site</> },
            { key: "column-5", label: <>Start date</> },
            { key: "column-6", label: <>Submitted</> },
            { key: "column-7", label: <>Action</> },
          ]}
        />
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

function ApprovalVehicleResults({
  vehicles,
}: Readonly<{ vehicles: ContractVehicleSearchResult[] }>) {
  return (
    <ContractVehicleResults
      vehicles={vehicles}
      headingId="backdating-vehicle-results-title"
      heading="Open a vehicle review"
      caption="Vehicles matched for backdating approval"
      actionLabel="Open review"
      hrefForVehicle={vehicleDetailHref}
    />
  );
}

export function BackdatingApprovalView({
  data,
  errorMessage,
  searchQuery,
  searchType,
}: Readonly<{
  data: Extract<BackdatingApprovalData, { kind: "ok" }>;
  errorMessage?: string;
  searchQuery: string;
  searchType: "GG" | "GP";
}>) {
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
          <ContractVehicleSearchFieldset
            legend="Select a vehicle to filter"
            searchType={searchType}
          />
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
        <ApprovalVehicleResults vehicles={data.vehicles} />
        <section
          className="vehicle-status-maintenance-panel"
          aria-labelledby="backdating-queue-title"
        >
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">
                {data.pending.length} request{data.pending.length === 1 ? "" : "s"}
              </p>
              <h2 id="backdating-queue-title">Pending authorisation on backdating contracts</h2>
            </div>
          </div>
          <ApprovalQueue contracts={data.pending} />
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
