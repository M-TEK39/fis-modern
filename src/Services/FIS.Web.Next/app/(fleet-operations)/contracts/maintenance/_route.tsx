import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { ClipboardList } from "lucide-react";

import ModulePageHeader from "@/components/app-shell/module-page-header";
import ApiUnavailableCard from "@/components/app-shell/api-unavailable-card";
import { StreamedRoute } from "@/components/app-shell/streamed-route";
import { runContractAction } from "@/app/(fleet-operations)/contracts/actions";
import { ContractVehicleSearchFieldset } from "@/app/(fleet-operations)/contracts/_components";
import {
  canCloseActiveContract,
  canEditContract,
  canManageActiveContract,
  canReviewContract,
  canSubmitContract,
  hasContractAccess,
  type ContractSession,
} from "@/app/(fleet-operations)/contracts/access";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  getContractPage,
  type ContractRecord,
  type ContractVehicleSearchResult,
} from "@/lib/api/finance/api-contracts";
import type { SiteRecord } from "@/lib/api/reference-data/api-sites";
import { getSession } from "@/lib/auth/session";
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationNext,
  PaginationPrevious,
} from "@/components/ui/pagination";

import { loadContractMaintenanceData } from "./_data";

export type ContractMaintenancePageProps = {
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

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function siteLabel(site: SiteRecord) {
  return `${site.description?.trim() || `Site ${site.siteCode}`} (${site.siteCode})`;
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function getStatusLabel(contract: ContractRecord) {
  switch (contract.contractStatusCode) {
    case 0:
      return "Draft";
    case 1:
      return "Pending Review";
    case 2:
      return "Approved";
    case 3:
      return "Active";
    case 4:
      return "Declined for Correction";
    case 5:
      return "Declined";
    case 6:
      return "Cancelled";
    case 7:
      return "Closed";
    default:
      return contract.stillCurrent?.toUpperCase() === "Y" ? "Active" : "Unknown";
  }
}

function getStatusClass(contract: ContractRecord) {
  if (contract.contractStatusCode === 3 || contract.stillCurrent?.toUpperCase() === "Y")
    return "badge-success";
  if (contract.contractStatusCode === 1 || contract.contractStatusCode === 4)
    return "badge-warning";
  if ([5, 6].includes(contract.contractStatusCode ?? -1)) return "badge-error";
  return "badge";
}

function isActiveContract(contract: ContractRecord) {
  return (
    contract.contractStatusCode === 3 ||
    (contract.contractStatusCode === null && contract.stillCurrent?.toUpperCase() === "Y")
  );
}

function detailHref(contract: ContractRecord) {
  const params = new URLSearchParams({ contractId: String(contract.contractCode) });
  if (contract.fleetNumber) params.set("ggnumber", contract.fleetNumber);
  if (contract.registrationNumber) params.set("regnumber", contract.registrationNumber);
  return `/contracts/detail?${params.toString()}`;
}

function vehicleDetailHref(vehicle: ContractVehicleSearchResult) {
  const params = new URLSearchParams({ vmfCode: String(vehicle.vmfCode) });
  if (vehicle.fleetNumber) params.set("ggnumber", vehicle.fleetNumber);
  if (vehicle.registrationNumber) params.set("regnumber", vehicle.registrationNumber);
  return `/contracts/detail?${params.toString()}`;
}

function buildPageHref(query: Record<string, string | string[] | undefined>, page: number) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    const first = getQueryValue(value);
    if (first && key !== "page") params.set(key, first);
  }
  if (page > 1) params.set("page", String(page));
  const queryString = params.toString();
  return `/contracts/maintenance${queryString ? `?${queryString}` : ""}`;
}

const HIDDEN_FILTERS = [
  "status",
  "siteCode",
  "stillCurrent",
  "startDateFrom",
  "startDateTo",
] as const;

function SearchForm({
  searchType,
  searchQuery,
  query,
}: Readonly<{
  searchType: "GG" | "GP";
  searchQuery: string;
  query: Record<string, string | string[] | undefined>;
}>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      {HIDDEN_FILTERS.map((key) => {
        const value = getQueryValue(query[key]);
        return value ? <input key={key} name={key} type="hidden" value={value} /> : null;
      })}
      <ContractVehicleSearchFieldset
        ggLabel="GG Number"
        legend="Select the vehicle to manage"
        registrationLabel="Registration Number"
        searchType={searchType}
      />
      <div className="vehicle-search-row">
        <label className="sr-only" htmlFor="contract-vehicle-search">
          {searchType === "GG" ? "GG number" : "Registration number"}
        </label>
        <input
          className="vehicle-search"
          id="contract-vehicle-search"
          maxLength={20}
          name="searchQuery"
          placeholder={searchType === "GG" ? "Enter GG number" : "Enter registration number"}
          defaultValue={searchQuery}
        />
        <button className="button button-primary" type="submit">
          Search
        </button>
      </div>
      <div className="button-row">
        <Link className="button button-secondary" href="/contracts">
          Contracts Menu
        </Link>
      </div>
    </form>
  );
}

function Filters({
  query,
  sites,
  siteLookupUnavailable,
}: Readonly<{
  query: Record<string, string | string[] | undefined>;
  sites: SiteRecord[];
  siteLookupUnavailable: boolean;
}>) {
  const selectedSiteCode = getQueryValue(query.siteCode) ?? "";
  const hasSelectedSite = sites.some((site) => String(site.siteCode) === selectedSiteCode);

  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <input name="searchType" type="hidden" value={getQueryValue(query.searchType) ?? "GG"} />
      <input name="searchQuery" type="hidden" value={getQueryValue(query.searchQuery) ?? ""} />
      <div className="form-grid">
        <div className="form-field">
          <label className="form-label" htmlFor="contract-status">
            Status
          </label>
          <select
            className="form-select"
            id="contract-status"
            name="status"
            defaultValue={getQueryValue(query.status) ?? ""}
          >
            <option value="">All statuses</option>
            {[
              [0, "Draft"],
              [1, "Pending Review"],
              [2, "Approved"],
              [3, "Active"],
              [4, "Declined for Correction"],
              [5, "Declined"],
              [6, "Cancelled"],
              [7, "Closed"],
            ].map(([code, label]) => (
              <option key={code} value={code}>
                {label}
              </option>
            ))}
          </select>
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="contract-site">
            Site
          </label>
          <select
            className="form-select"
            disabled={siteLookupUnavailable}
            id="contract-site"
            name="siteCode"
            defaultValue={selectedSiteCode}
          >
            <option value="">All sites</option>
            {selectedSiteCode && !hasSelectedSite ? (
              <option
                value={selectedSiteCode}
              >{`Site ${selectedSiteCode} (${selectedSiteCode})`}</option>
            ) : null}
            {sites.map((site) => (
              <option key={site.siteCode} value={site.siteCode}>
                {siteLabel(site)}
              </option>
            ))}
          </select>
          {siteLookupUnavailable && selectedSiteCode ? (
            <input name="siteCode" type="hidden" value={selectedSiteCode} />
          ) : null}
          {siteLookupUnavailable ? (
            <p className="muted-copy" role="status">
              Site options are temporarily unavailable; other filters remain available.
            </p>
          ) : null}
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="contract-current">
            Current
          </label>
          <select
            className="form-select"
            id="contract-current"
            name="stillCurrent"
            defaultValue={getQueryValue(query.stillCurrent) ?? ""}
          >
            <option value="">All</option>
            <option value="Y">Still current</option>
            <option value="N">Not current</option>
          </select>
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="contract-from">
            Start date from
          </label>
          <input
            className="form-input"
            id="contract-from"
            name="startDateFrom"
            type="date"
            defaultValue={getQueryValue(query.startDateFrom)?.slice(0, 10) ?? ""}
          />
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="contract-to">
            Start date to
          </label>
          <input
            className="form-input"
            id="contract-to"
            name="startDateTo"
            type="date"
            defaultValue={getQueryValue(query.startDateTo)?.slice(0, 10) ?? ""}
          />
        </div>
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Apply filters
        </button>
        <Link className="button button-secondary" href="/contracts/maintenance">
          Reset
        </Link>
      </div>
    </form>
  );
}

function VehicleSearchResults({ vehicles }: Readonly<{ vehicles: ContractVehicleSearchResult[] }>) {
  if (vehicles.length === 0) return null;
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="contract-vehicle-results-title"
    >
      <p className="eyebrow">Vehicle search results</p>
      <h2 id="contract-vehicle-results-title">Select a vehicle to open contract management</h2>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table">
          <caption className="sr-only">Vehicles matched by contract search</caption>
          <DataTableHeader
            columns={[
              { key: "column-1", label: <>GG number</> },
              { key: "column-2", label: <>Registration</> },
              { key: "column-3", label: <>Action</> },
            ]}
          />
          <tbody>
            {vehicles.slice(0, 10).map((vehicle) => (
              <tr key={vehicle.vmfCode}>
                <td>{valueOrDash(vehicle.fleetNumber)}</td>
                <td>{valueOrDash(vehicle.registrationNumber)}</td>
                <td>
                  <Link
                    className="button button-primary button-small"
                    href={vehicleDetailHref(vehicle)}
                  >
                    Open
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function ContractActionForm({
  contract,
  action,
  label,
  className = "button button-secondary button-small",
}: Readonly<{
  contract: ContractRecord;
  action: "submit" | "recall" | "approve-activate" | "cancel";
  label: string;
  className?: string;
}>) {
  return (
    <form action={runContractAction}>
      <input name="contractId" type="hidden" value={contract.contractCode} />
      <input name="returnPath" type="hidden" value="/contracts/maintenance" />
      <input name="action" type="hidden" value={action} />
      <button className={className} type="submit">
        {label}
      </button>
    </form>
  );
}

function ContractTable({
  contracts,
  session,
}: Readonly<{
  contracts: ContractRecord[];
  session: ContractSession;
}>) {
  if (contracts.length === 0)
    return (
      <div className="vehicle-empty-state">
        <p className="eyebrow">No records found</p>
        <h2>No contracts matched the selected filters.</h2>
        <p className="muted-copy">Search for a vehicle or adjust the contract filters.</p>
      </div>
    );
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Vehicle contracts</caption>
        <DataTableHeader
          columns={[
            { key: "column-1", label: <>Contract</> },
            { key: "column-2", label: <>Vehicle</> },
            { key: "column-3", label: <>Site</> },
            { key: "column-4", label: <>Driver</> },
            { key: "column-5", label: <>Start date</> },
            { key: "column-6", label: <>Target return</> },
            { key: "column-7", label: <>Status</> },
            { key: "column-8", label: <>Action</> },
          ]}
        />
        <tbody>
          {contracts.map((contract) => (
            <tr key={contract.contractCode}>
              <td>{contract.contractCode}</td>
              <td>
                {valueOrDash(contract.fleetNumber)} / {valueOrDash(contract.registrationNumber)}
              </td>
              <td>
                {valueOrDash(contract.siteDescription)} ({contract.siteCode})
              </td>
              <td>{valueOrDash(contract.driverName)}</td>
              <td>{formatDate(contract.startDate)}</td>
              <td>{formatDate(contract.targetReturnDate)}</td>
              <td>
                <span className={`badge ${getStatusClass(contract)}`}>
                  {getStatusLabel(contract)}
                </span>
              </td>
              <td>
                <div className="button-row">
                  <Link
                    className="button button-secondary button-small"
                    href={detailHref(contract)}
                  >
                    {[5, 6, 7].includes(contract.contractStatusCode ?? -1) ? "View" : "Open"}
                  </Link>
                  {(contract.contractStatusCode === 0 || contract.contractStatusCode === 4) &&
                  canEditContract(contract, session) ? (
                    <Link
                      className="button button-primary button-small"
                      href={`${detailHref(contract)}#edit-contract`}
                    >
                      Edit
                    </Link>
                  ) : null}
                  {(contract.contractStatusCode === 0 || contract.contractStatusCode === 4) &&
                  canSubmitContract(contract, session) ? (
                    <ContractActionForm
                      action="submit"
                      contract={contract}
                      label={contract.contractStatusCode === 4 ? "Resubmit" : "Submit"}
                    />
                  ) : null}
                  {contract.contractStatusCode === 1 && canSubmitContract(contract, session) ? (
                    <ContractActionForm action="recall" contract={contract} label="Recall" />
                  ) : null}
                  {contract.contractStatusCode === 1 && canReviewContract(contract, session) ? (
                    <Link
                      className="button button-primary button-small"
                      href={detailHref(contract)}
                    >
                      Review
                    </Link>
                  ) : null}
                  {contract.contractStatusCode === 2 && canReviewContract(contract, session) ? (
                    <ContractActionForm
                      action="approve-activate"
                      contract={contract}
                      label="Activate"
                      className="button button-primary button-small"
                    />
                  ) : null}
                  {isActiveContract(contract) && canManageActiveContract(session.roles) ? (
                    <Link
                      className="button button-secondary button-small"
                      href={`${detailHref(contract)}#extend-contract`}
                    >
                      Extend
                    </Link>
                  ) : null}
                  {isActiveContract(contract) && canCloseActiveContract(session.roles) ? (
                    <Link
                      className="button button-secondary button-small"
                      href={`${detailHref(contract)}#close-contract`}
                    >
                      Close
                    </Link>
                  ) : null}
                  {isActiveContract(contract) && canCloseActiveContract(session.roles) ? (
                    <ContractActionForm
                      action="cancel"
                      contract={contract}
                      label="Cancel"
                      className="button button-danger button-small"
                    />
                  ) : null}
                  {(contract.contractStatusCode ?? (isActiveContract(contract) ? 3 : 0)) >= 2 ? (
                    <Link
                      className="button button-secondary button-small"
                      href={`/contracts/printout?contractId=${contract.contractCode}`}
                    >
                      Print
                    </Link>
                  ) : null}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ApiUnavailable({ routePath }: Readonly<{ routePath: string }>) {
  return (
    <ApiUnavailableCard
      message="Contract maintenance could not be loaded."
      retryHref={routePath}
      secondaryHref="/login"
      secondaryLabel="Sign in"
      showIcon={false}
    />
  );
}

function ContractResultsSection({
  pageData,
  query,
  session,
}: Readonly<{
  pageData: Awaited<ReturnType<typeof getContractPage>>;
  query: Record<string, string | string[] | undefined>;
  session: ContractSession;
}>) {
  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="contract-results-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">
            {pageData.totalRecords} record{pageData.totalRecords === 1 ? "" : "s"}
          </p>
          <h2 id="contract-results-title">Existing contracts</h2>
        </div>
      </div>
      <ContractTable contracts={pageData.items} session={session} />
      <Pagination className="mt-4" aria-label="Contract results pages">
        <PaginationContent className="flex-wrap justify-center gap-2">
          <PaginationItem>
            <PaginationPrevious
              aria-disabled={pageData.page <= 1}
              className={pageData.page <= 1 ? "pointer-events-none opacity-50" : undefined}
              href={
                pageData.page <= 1
                  ? buildPageHref(query, pageData.page)
                  : buildPageHref(query, pageData.page - 1)
              }
              tabIndex={pageData.page <= 1 ? -1 : undefined}
            />
          </PaginationItem>
          <PaginationItem>
            <span className="inline-flex h-9 items-center whitespace-nowrap px-2 text-sm font-medium text-muted-foreground">
              Page {pageData.page} of {pageData.totalPages}
            </span>
          </PaginationItem>
          <PaginationItem>
            <PaginationNext
              aria-disabled={pageData.page >= pageData.totalPages}
              className={
                pageData.page >= pageData.totalPages ? "pointer-events-none opacity-50" : undefined
              }
              href={
                pageData.page >= pageData.totalPages
                  ? buildPageHref(query, pageData.page)
                  : buildPageHref(query, pageData.page + 1)
              }
              tabIndex={pageData.page >= pageData.totalPages ? -1 : undefined}
            />
          </PaginationItem>
        </PaginationContent>
      </Pagination>
    </section>
  );
}

function ContractMaintenancePageView({
  filteredVehicles,
  notice,
  pageData,
  query,
  searchQuery,
  searchType,
  siteLookupUnavailable,
  sites,
  session,
}: Readonly<{
  filteredVehicles: ContractVehicleSearchResult[];
  notice?: string;
  pageData: Awaited<ReturnType<typeof getContractPage>>;
  query: Record<string, string | string[] | undefined>;
  searchQuery: string;
  searchType: "GG" | "GP";
  siteLookupUnavailable: boolean;
  sites: SiteRecord[];
  session: ContractSession;
}>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="contract-maintenance-title">
        <ModulePageHeader
          icon={ClipboardList}
          eyebrow="Contract maintenance"
          title="Vehicle Contract Maintenance"
          titleId="contract-maintenance-title"
          description="Search a vehicle and manage its existing or new legacy contract."
          actions={
            <Link className="button button-secondary" href="/contracts">
              Contracts Menu
            </Link>
          }
        />
        {notice ? (
          <div
            className={getQueryValue(query.error) ? "notice notice-error" : "notice notice-success"}
            role={getQueryValue(query.error) ? "alert" : "status"}
          >
            {notice}
          </div>
        ) : null}
        <SearchForm searchType={searchType} searchQuery={searchQuery} query={query} />
        <VehicleSearchResults vehicles={filteredVehicles} />
        <Filters query={query} siteLookupUnavailable={siteLookupUnavailable} sites={sites} />
        <ContractResultsSection pageData={pageData} query={query} session={session} />
        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </div>
      </section>
    </main>
  );
}

const ContractMaintenancePageContent = renderContractMaintenancePageContent;

async function renderContractMaintenancePageContent({
  searchParams,
  routePath = "/contracts/maintenance",
}: ContractMaintenancePageProps) {
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
          <h2>You do not have permission to maintain vehicle contracts.</h2>
        </section>
      </main>
    );

  const query = await searchParams;
  const searchType = getQueryValue(query.searchType) === "GP" ? "GP" : "GG";
  const searchQuery = (
    getQueryValue(query.searchQuery) ??
    getQueryValue(query.txtGGNumber) ??
    getQueryValue(query.txtRegistrationNumber) ??
    ""
  )
    .trim()
    .slice(0, 20);
  const page = positiveInt(getQueryValue(query.page)) ?? 1;
  const statusCode = positiveInt(getQueryValue(query.status));
  const siteCode = positiveInt(getQueryValue(query.siteCode));
  const statusFilter = getQueryValue(query.status) === "0" ? 0 : statusCode;
  const notice =
    getQueryValue(query.saved) === "1"
      ? "Contract captured successfully."
      : getQueryValue(query.updated) === "1"
        ? "Contract updated successfully."
        : getQueryValue(query.success)
          ? `Contract ${getQueryValue(query.success)} successfully.`
          : getQueryValue(query.error);
  const data = await loadContractMaintenanceData({
    page,
    searchQuery,
    searchType,
    siteCode: siteCode ?? undefined,
    startDateFrom: getQueryValue(query.startDateFrom),
    startDateTo: getQueryValue(query.startDateTo),
    statusFilter: statusFilter ?? undefined,
    stillCurrent: getQueryValue(query.stillCurrent),
  });
  if (data.kind === "unauthorized")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (data.kind === "error")
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailable routePath={routePath} />
      </main>
    );
  return (
    <ContractMaintenancePageView
      filteredVehicles={data.filteredVehicles}
      notice={notice}
      pageData={data.pageData}
      query={query}
      searchQuery={searchQuery}
      searchType={searchType}
      siteLookupUnavailable={data.siteLookupUnavailable}
      sites={data.sites}
      session={session}
    />
  );
}

export function ContractMaintenanceRoute(props: ContractMaintenancePageProps) {
  return (
    <StreamedRoute>
      <ContractMaintenancePageContent {...props} />
    </StreamedRoute>
  );
}

export default function ContractMaintenancePage({
  searchParams,
}: Pick<ContractMaintenancePageProps, "searchParams">) {
  return <ContractMaintenanceRoute searchParams={searchParams} />;
}
