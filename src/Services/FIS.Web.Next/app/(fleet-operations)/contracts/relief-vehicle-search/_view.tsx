import Link from "next/link";

import { ContractVehicleSearchFieldset } from "@/app/(fleet-operations)/contracts/_components";
import type { ContractRecord, ReliefVehicleSearchResult } from "@/lib/api/finance/api-contracts";

import { Notice } from "./notice";
import { ReliefVehicleNotFound } from "./not-found";
import { ParentSummary } from "./parent-summary";
import { ReliefResults } from "./relief-results";

export function ReliefVehicleSearchView({
  canManage,
  contract,
  notice,
  routePath,
  searchQuery,
  searchType,
  vehicles,
}: Readonly<{
  canManage: boolean;
  contract: ContractRecord;
  notice?: { isError: boolean; message: string };
  routePath: string;
  searchQuery: string;
  searchType: "GG" | "GP";
  vehicles: ReliefVehicleSearchResult[];
}>) {
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
        {notice ? <Notice {...notice} /> : null}
        <ParentSummary contract={contract} />
        <form className="vehicle-status-maintenance-panel" method="get">
          <input name="contractId" type="hidden" value={contract.contractCode} />
          <ContractVehicleSearchFieldset legend="Find relief vehicle by" searchType={searchType} />
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
}
