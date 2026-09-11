import DataTableHeader from "@/components/ui/data-table-header";

import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import ApiUnavailableCard from "@/components/app-shell/api-unavailable-card";
import { MenuSection } from "@/components/ui/menu-section";
import {
  DEFAULT_WORKSHOP_PAGE_SIZE,
  getWorkshopPage,
  WorkshopApiError,
  type WorkshopPage,
} from "@/lib/api/fleet-operations/api-workshop";
import { getSession } from "@/lib/auth/session";

const WORKSHOP_ROLE = "Workshop";

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function pageHref(search: string, status: string, page: number) {
  const params = new URLSearchParams({ page: String(page) });
  if (search) params.set("search", search);
  if (status) params.set("status", status);
  return `/workshop?${params.toString()}`;
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to access Workshop.</h2>
    </section>
  );
}

function ApiUnavailable() {
  return (
    <ApiUnavailableCard
      message="Workshop could not be opened."
      retryHref="/workshop"
      secondaryHref="/login"
      secondaryLabel="Sign in"
    />
  );
}

function WorkshopSnapshot({
  pageData,
  search,
  status,
}: Readonly<{
  pageData: WorkshopPage;
  search: string;
  status: string;
}>) {
  const { items, page, pageSize, total, totalPages } = pageData;

  return (
    <section className="vehicle-status-maintenance-panel" aria-labelledby="workshop-snapshot-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">{total} matching entries</p>
          <h2 id="workshop-snapshot-title">All Workshop Entries Snapshot</h2>
        </div>
      </div>
      <form className="vehicle-search-row" method="get">
        <input name="page" type="hidden" value="1" />
        <label className="sr-only" htmlFor="workshop-search">
          Search workshop entries
        </label>
        <input
          className="vehicle-search"
          id="workshop-search"
          name="search"
          placeholder="Search entry ID, vehicle, status..."
          defaultValue={search}
        />
        <label className="sr-only" htmlFor="workshop-status">
          Filter workshop entries
        </label>
        <select className="form-select" id="workshop-status" name="status" defaultValue={status}>
          <option value="">All entries</option>
          <option value="open">Open entries</option>
          <option value="closed">Closed entries</option>
          <option value="vehicle">Vehicle-linked entries</option>
        </select>
        <button className="button button-secondary" type="submit">
          Filter
        </button>
      </form>
      {items.length === 0 ? (
        <p className="muted-copy">No workshop entries match the current filter.</p>
      ) : (
        <div className="vehicle-table-wrapper">
          <table className="vehicle-table">
            <caption className="sr-only">Workshop entry snapshot</caption>
            <DataTableHeader
              columns={[
                { key: "column-1", label: <>Entry ID</> },
                { key: "column-2", label: <>Vehicle</> },
                { key: "column-3", label: <>Date Received</> },
                { key: "column-4", label: <>Date Completed</> },
                { key: "column-5", label: <>Status</> },
              ]}
            />
            <tbody>
              {items.map((workshop) => {
                const hasVehicleLabel =
                  workshop.fleetNumber !== null || workshop.registrationNumber !== null;
                return (
                  <tr key={workshop.wwCode}>
                    <td>{workshop.wwCode}</td>
                    <td>
                      {hasVehicleLabel
                        ? `${valueOrDash(workshop.fleetNumber)} / ${valueOrDash(workshop.registrationNumber)}`
                        : `VMF ${valueOrDash(workshop.vmfCode)}`}
                    </td>
                    <td>{formatDate(workshop.receiveDate)}</td>
                    <td>{formatDate(workshop.completeDate)}</td>
                    <td>{workshop.status}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
      {totalPages > 1 ? (
        <nav className="vehicle-pagination" aria-label="Workshop snapshot pages">
          {page > 1 ? (
            <Link className="vehicle-pagination-button" href={pageHref(search, status, page - 1)}>
              Previous
            </Link>
          ) : (
            <span
              className="vehicle-pagination-button vehicle-pagination-disabled"
              aria-disabled="true"
            >
              Previous
            </span>
          )}
          <span className="vehicle-pagination-meta" aria-live="polite">
            Page {page} of {totalPages}
          </span>
          {page < totalPages ? (
            <Link className="vehicle-pagination-button" href={pageHref(search, status, page + 1)}>
              Next
            </Link>
          ) : (
            <span
              className="vehicle-pagination-button vehicle-pagination-disabled"
              aria-disabled="true"
            >
              Next
            </span>
          )}
        </nav>
      ) : null}
      <div className="pagination-meta">
        Total records: {total} | Page size: {pageSize}
      </div>
    </section>
  );
}

const WorkshopPageContent = renderWorkshopPageContent;

async function renderWorkshopPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/workshop" />
      </main>
    );
  if (session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath="/workshop" />
      </main>
    );
  if (!hasRole(session.roles, WORKSHOP_ROLE))
    return (
      <main className="page-shell vehicle-page-shell">
        <AccessRestricted />
      </main>
    );

  const query = await searchParams;
  const search = (
    Array.isArray(query.search) ? (query.search[0] ?? "") : (query.search ?? "")
  ).trim();
  const requestedStatus = (
    Array.isArray(query.status) ? (query.status[0] ?? "") : (query.status ?? "")
  )
    .trim()
    .toLowerCase();
  const status = ["open", "closed", "vehicle"].includes(requestedStatus) ? requestedStatus : "";
  const requestedPage = Number.parseInt(
    Array.isArray(query.page) ? (query.page[0] ?? "1") : (query.page ?? "1"),
    10,
  );
  const page = Number.isFinite(requestedPage) ? Math.max(1, requestedPage) : 1;
  let pageData: WorkshopPage | null = null;
  let unavailable = false;
  try {
    pageData = await getWorkshopPage({
      page,
      pageSize: DEFAULT_WORKSHOP_PAGE_SIZE,
      search,
      status,
    });
  } catch (error) {
    unavailable = error instanceof WorkshopApiError && error.reason === "unavailable";
  }

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="workshop-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Workshop</p>
            <h1 id="workshop-title">Workshop Maintenance Menu</h1>
            <p>
              Capture workshop entries, reopen closed job cards, and maintain workshop merchants.
            </p>
          </div>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </header>
        <div className="vehicle-menu-tiles">
          <MenuSection title="Workshop Maintenance Information / Help">
            <Link className="vehicle-menu-link" href="/workshop/help">
              Workshop Maintenance Information / Help
            </Link>
          </MenuSection>
          <MenuSection title="Workshop Section">
            <Link className="vehicle-menu-link" href="/workshop/entry">
              1) Enter a WorkShop Entry
            </Link>
            <Link className="vehicle-menu-link" href="/workshop/open-job-card">
              2) OPEN a CLOSED Job Card
            </Link>
            <Link className="vehicle-menu-link" href="/workshop/merchant">
              3) Enter / Update a Merchant
            </Link>
          </MenuSection>
        </div>
        {unavailable ? (
          <ApiUnavailable />
        ) : (
          <WorkshopSnapshot
            pageData={
              pageData ?? {
                items: [],
                page,
                pageSize: DEFAULT_WORKSHOP_PAGE_SIZE,
                total: 0,
                totalPages: 1,
              }
            }
            search={search}
            status={status}
          />
        )}
      </section>
    </main>
  );
}

export default function WorkshopPage(props: Parameters<typeof WorkshopPageContent>[0]) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <WorkshopPageContent {...props} />
    </Suspense>
  );
}
