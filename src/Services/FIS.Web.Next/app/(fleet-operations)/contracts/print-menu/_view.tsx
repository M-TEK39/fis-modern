import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";

import type { ContractRecord } from "@/lib/api/finance/api-contracts";

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function statusLabel(contract: ContractRecord) {
  return (
    [
      "Unknown",
      "Pending Review",
      "Approved",
      "Active",
      "Declined for Correction",
      "Declined",
      "Cancelled",
      "Closed",
    ][contract.contractStatusCode ?? 0] ?? "Unknown"
  );
}

function PrintResults({ results }: Readonly<{ results: ContractRecord[] }>) {
  if (results.length === 0)
    return <p className="muted-copy">No contracts are available for printing.</p>;
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Contracts available for printing</caption>
        <DataTableHeader
          columns={[
            { key: "column-1", label: <>Contract</> },
            { key: "column-2", label: <>Vehicle</> },
            { key: "column-3", label: <>Registration</> },
            { key: "column-4", label: <>Status</> },
            { key: "column-5", label: <>Start date</> },
            { key: "column-6", label: <>Target return</> },
            { key: "column-7", label: <>Action</> },
          ]}
        />
        <tbody>
          {results.map((contract) => (
            <tr key={contract.contractCode}>
              <td>{contract.contractCode}</td>
              <td>{valueOrDash(contract.fleetNumber)}</td>
              <td>{valueOrDash(contract.registrationNumber)}</td>
              <td>{statusLabel(contract)}</td>
              <td>{formatDate(contract.startDate)}</td>
              <td>{formatDate(contract.targetReturnDate)}</td>
              <td>
                <Link
                  className="button button-secondary button-small"
                  href={`/contracts/printout?contractId=${contract.contractCode}`}
                >
                  Preview / Print
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function ContractPrintMenuView({
  errorMessage,
  mode,
  results,
  value,
}: Readonly<{
  errorMessage?: string;
  mode: string;
  results: ContractRecord[];
  value: string;
}>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="contract-print-menu-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Contract maintenance</p>
            <h1 id="contract-print-menu-title">Contract Print Out Menu</h1>
            <p>Search by contract number, GG number, or registration number.</p>
          </div>
          <Link className="button button-secondary" href="/contracts">
            Contracts Menu
          </Link>
        </header>
        <form className="vehicle-status-maintenance-panel" method="get">
          <div className="form-grid">
            <div className="form-field">
              <label className="form-label" htmlFor="print-mode">
                Search type
              </label>
              <select className="form-select" id="print-mode" name="mode" defaultValue={mode}>
                <option value="Contract">Contract number</option>
                <option value="GG">GG number</option>
                <option value="GP">Registration number</option>
              </select>
            </div>
            <div className="form-field">
              <label className="form-label" htmlFor="print-value">
                Search value
              </label>
              <input
                className="form-input"
                id="print-value"
                maxLength={20}
                name="value"
                defaultValue={value}
              />
            </div>
          </div>
          <div className="button-row">
            <button className="button button-primary" type="submit">
              Search
            </button>
            <Link className="button button-secondary" href="/contracts/print-menu">
              Load recent
            </Link>
          </div>
        </form>
        {errorMessage ? (
          <div className="notice notice-error" role="alert">
            {errorMessage}
          </div>
        ) : null}
        <section className="vehicle-status-maintenance-panel" aria-labelledby="print-results-title">
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">
                {results.length} record{results.length === 1 ? "" : "s"}
              </p>
              <h2 id="print-results-title">Contract print results</h2>
            </div>
          </div>
          <PrintResults results={results} />
        </section>
        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href="/contracts/maintenance">
            Vehicle Contract Maintenance
          </Link>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </div>
      </section>
    </main>
  );
}
