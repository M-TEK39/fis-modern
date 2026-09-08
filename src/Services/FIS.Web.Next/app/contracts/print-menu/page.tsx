import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import {
  ContractApiError,
  getContract,
  getContractPage,
  searchContractVehicles,
  type ContractRecord,
} from "@/lib/api-contracts";
import { getSession } from "@/lib/session";

const CONTRACT_PERMISSION = BigInt(2);

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

export default async function ContractPrintMenuPage({
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
        <SessionRecovery returnPath="/contracts/print-menu" />
      </main>
    );
  if (session.status !== "authenticated")
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h2>Contract print menu could not be loaded.</h2>
        </section>
      </main>
    );
  if (!hasContractAccess(session.accessLevel, session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to print contracts.</h2>
        </section>
      </main>
    );

  const query = await searchParams;
  const mode =
    getQueryValue(query.mode) === "GG" || getQueryValue(query.mode) === "GP"
      ? getQueryValue(query.mode)!
      : "Contract";
  const value = (getQueryValue(query.value) ?? "").trim().slice(0, 20);
  let results: ContractRecord[] = [];
  let errorMessage: string | undefined;
  try {
    if (!value) {
      results = (await getContractPage({ page: 1, pageSize: 100 })).items;
    } else if (mode === "Contract") {
      const contractId = positiveInt(value);
      if (!contractId) errorMessage = "Enter a valid contract number.";
      else results = [await getContract(contractId)];
    } else {
      const vehicles = await searchContractVehicles(value);
      const matches = vehicles.filter(
        (vehicle) =>
          (mode === "GG"
            ? vehicle.fleetNumber
            : vehicle.registrationNumber
          )?.toLocaleLowerCase() === value.toLocaleLowerCase(),
      );
      results = (
        await Promise.all(
          matches.map((vehicle) =>
            getContractPage({ page: 1, pageSize: 100, vmfCode: vehicle.vmfCode }),
          ),
        )
      ).flatMap((page) => page.items);
    }
  } catch (error) {
    if (error instanceof ContractApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath="/contracts/print-menu" />
        </main>
      );
    errorMessage =
      error instanceof ContractApiError && error.reason === "not-found"
        ? "The requested contract was not found."
        : "Contract print records could not be loaded.";
  }

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
          {results.length === 0 ? (
            <p className="muted-copy">No contracts are available for printing.</p>
          ) : (
            <div className="vehicle-table-wrapper">
              <table className="vehicle-table">
                <caption className="sr-only">Contracts available for printing</caption>
                <thead>
                  <tr>
                    <th scope="col">Contract</th>
                    <th scope="col">Vehicle</th>
                    <th scope="col">Registration</th>
                    <th scope="col">Status</th>
                    <th scope="col">Start date</th>
                    <th scope="col">Target return</th>
                    <th scope="col">Action</th>
                  </tr>
                </thead>
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
          )}
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
