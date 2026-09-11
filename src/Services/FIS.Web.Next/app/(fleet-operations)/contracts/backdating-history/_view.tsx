import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";

import { ContractVehicleSearchFieldset } from "@/app/(fleet-operations)/contracts/_components";
import { updateContractHistoryAction } from "@/app/(fleet-operations)/contracts/actions";
import ContractVehicleResults from "@/components/ui/contract-vehicle-results";
import type { ContractRecord, ContractVehicleSearchResult } from "@/lib/api/finance/api-contracts";
import type { BackdatingHistoryData } from "./_data";

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

export function BackdatingHistoryUnavailable() {
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

function HistoryVehicleResults({
  vehicles,
}: Readonly<{ vehicles: ContractVehicleSearchResult[] }>) {
  return (
    <ContractVehicleResults
      vehicles={vehicles}
      headingId="history-vehicle-results-title"
      heading="Select a vehicle"
      caption="Vehicles matched for contract history backdating"
      actionLabel="Load history"
      hrefForVehicle={vehicleDetailHref}
    />
  );
}

function HistoryContracts({ contracts }: Readonly<{ contracts: ContractRecord[] }>) {
  if (contracts.length === 0) return <p className="muted-copy">No Contract History Found</p>;
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Contract history for selected vehicle</caption>
        <DataTableHeader
          columns={[
            { key: "column-1", label: <>Contract</> },
            { key: "column-2", label: <>Start date</> },
            { key: "column-3", label: <>End date</> },
            { key: "column-4", label: <>Start odometer</> },
            { key: "column-5", label: <>End odometer</> },
            { key: "column-6", label: <>Status</> },
            { key: "column-7", label: <>Action</> },
          ]}
        />
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
  );
}

export function BackdatingHistoryView({
  data,
  errorMessage,
  notice,
  requestedVmfCode,
  searchQuery,
  searchType,
}: Readonly<{
  data: Extract<BackdatingHistoryData, { kind: "ok" }>;
  errorMessage?: string;
  notice?: { isError: boolean; message: string };
  requestedVmfCode: number | null;
  searchQuery: string;
  searchType: "GG" | "GP";
}>) {
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
            className={notice.isError ? "notice notice-error" : "notice notice-success"}
            role={notice.isError ? "alert" : "status"}
          >
            {notice.message}
          </div>
        ) : null}
        {errorMessage ? (
          <div className="notice notice-error" role="alert">
            {errorMessage}
          </div>
        ) : null}
        <form className="vehicle-status-maintenance-panel" method="get">
          <ContractVehicleSearchFieldset
            legend="Select a vehicle to manage"
            searchType={searchType}
          />
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
        <HistoryVehicleResults vehicles={data.vehicles} />
        {requestedVmfCode ? (
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="history-results-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">
                  {data.contracts.length} record{data.contracts.length === 1 ? "" : "s"}
                </p>
                <h2 id="history-results-title">Contract history</h2>
              </div>
            </div>
            <HistoryContracts contracts={data.contracts} />
          </section>
        ) : null}
        {data.selectedContract ? <HistoryEditForm contract={data.selectedContract} /> : null}
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
