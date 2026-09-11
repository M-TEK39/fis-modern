import Link from "next/link";
import { connection } from "next/server";
import { redirect } from "next/navigation";
import { Suspense } from "react";

import { logoutAction } from "@/app/(auth)/actions/auth";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import SearchTypeFieldset from "@/components/ui/search-type-fieldset";
import VehicleTable, {
  type VehicleTableColumn,
} from "@/app/(fleet-operations)/accidents/vehicle-table";
import {
  AccidentApiError,
  type GarageAccidentPage,
  type GarageAccidentRow,
  getHqAccidentPage,
  type GarageSearchType,
} from "@/lib/api/fleet-operations/api-accidents";
import { getSession } from "@/lib/auth/session";

const ACCIDENTS_ROLE = "Accidents";

export type HqPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  locationCode?: number;
};

function hasRole(roles: readonly string[], role: string) {
  return roles.some(
    (candidate) => candidate.localeCompare(role, undefined, { sensitivity: "accent" }) === 0,
  );
}

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getSearchType(value: string | undefined): GarageSearchType {
  return value === "GG" || value === "Radiogg" ? "GG" : "GP";
}

function getPage(value: string | undefined) {
  const parsed = Number.parseInt(value ?? "1", 10);
  return Number.isFinite(parsed) ? parsed : 1;
}

function buildHqHref(
  searchType: GarageSearchType,
  searchTerm: string,
  page: number,
  locationCode?: number,
) {
  const params = new URLSearchParams({ type: searchType, page: String(page) });
  if (searchTerm) params.set("q", searchTerm);
  const path = locationCode === 2 ? "/Accident/MNT_accidentp_getreg.aspx" : "/accidents/hq";
  return `${path}?${params.toString()}`;
}

function buildHqAddHref(searchType: GarageSearchType, searchTerm: string, locationCode?: number) {
  const params = new URLSearchParams({ type: searchType });
  if (searchTerm) params.set("q", searchTerm);
  if (locationCode === 2) {
    params.set("Action", "ADD");
    return `/Accident/MNT_accidentp_getdetail.aspx?${params.toString()}`;
  }

  return `/accidents/hq/add?${params.toString()}`;
}

const hqTableColumns: readonly VehicleTableColumn<GarageAccidentRow>[] = [
  { key: "vehicleNumber", label: "Vehicle Number", render: (row) => row.vehicleNumber ?? "-" },
  { key: "hireType", label: "Hire Type", render: (row) => row.hireType ?? "-" },
  {
    key: "accidentDate",
    label: "Accident Date",
    render: (row) => row.accidentDate?.slice(0, 10) ?? "-",
  },
  { key: "reference", label: "Reference", render: (row) => row.reference ?? "-" },
  {
    key: "action",
    label: "Action",
    render: (row) => (
      <Link
        className="button button-secondary button-small"
        href={`/accidents/hq/edit?accidentId=${encodeURIComponent(row.accidentCode)}`}
      >
        Edit
      </Link>
    ),
  },
];

function LoadingState() {
  return (
    <div className="loading-card" aria-busy="true">
      <span className="spinner" aria-hidden="true" />
      <p>Loading page…</p>
    </div>
  );
}

function ApiUnavailable() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">API unavailable</p>
      <h2>HQ accident records could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/accidents/hq">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

function AccessRestricted() {
  return (
    <section className="vehicle-status-card" role="alert">
      <div className="status-icon status-icon-error" aria-hidden="true">
        !
      </div>
      <p className="eyebrow">Access restricted</p>
      <h2>You do not have permission to maintain HQ accidents.</h2>
      <p className="muted-copy">
        Contact your FIS administrator if you need accident-management access.
      </p>
    </section>
  );
}

function NoRecords({ searchTerm }: { searchTerm: string }) {
  return (
    <div className="vehicle-empty-state">
      <p className="eyebrow">No records found</p>
      <h2>
        {searchTerm ? `No accidents matched “${searchTerm}”.` : "No HQ accidents are available."}
      </h2>
      <p className="muted-copy">Try another GG or GP number, or add a new accident record.</p>
    </div>
  );
}

function HqSearchForm({
  searchType,
  searchTerm,
}: {
  searchType: GarageSearchType;
  searchTerm: string;
}) {
  return (
    <form className="accident-garage-search" method="get">
      <SearchTypeFieldset
        selectedType={searchType}
        legend="Search by"
        name="type"
        className="accident-garage-search-options"
      />
      <div className="vehicle-search-row">
        <label className="sr-only" htmlFor="hq-search">
          {searchType === "GG" ? "GG Number" : "GP Number"}
        </label>
        <input
          className="vehicle-search"
          id="hq-search"
          maxLength={8}
          name="q"
          placeholder={searchType === "GG" ? "Enter GG number" : "Enter GP number"}
          defaultValue={searchTerm}
        />
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Submit
        </button>
        <Link className="button button-secondary" href="/accidents">
          Menu
        </Link>
      </div>
    </form>
  );
}

function HqPageResults({
  pageData,
  searchType,
  searchTerm,
  locationCode,
}: {
  pageData: GarageAccidentPage;
  searchType: GarageSearchType;
  searchTerm: string;
  locationCode?: number;
}) {
  if (pageData.totalRecords === 0) return <NoRecords searchTerm={searchTerm} />;

  return (
    <>
      <div className="vehicle-table-wrapper" aria-live="polite">
        <VehicleTable
          caption="HQ accident records"
          columns={hqTableColumns}
          rows={pageData.rows}
          rowKey={(row) => row.accidentCode}
        />
      </div>
      {pageData.totalPages > 1 ? (
        <nav className="vehicle-pagination" aria-label="HQ accident pages">
          {pageData.page > 1 ? (
            <Link
              className="vehicle-pagination-button"
              href={buildHqHref(searchType, searchTerm, pageData.page - 1, locationCode)}
            >
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
          <span>
            Page {pageData.page} of {pageData.totalPages}
          </span>
          {pageData.page < pageData.totalPages ? (
            <Link
              className="vehicle-pagination-button"
              href={buildHqHref(searchType, searchTerm, pageData.page + 1, locationCode)}
            >
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
      <p className="vehicle-pagination-meta">
        Total records: {pageData.totalRecords} | Page size: {pageData.pageSize}
      </p>
    </>
  );
}

async function HqContent({ searchParams, locationCode }: HqPageProps) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired") return <SessionRecovery returnPath="/accidents/hq" />;
  if (session.status === "unavailable") return <ApiUnavailable />;
  if (!hasRole(session.roles, ACCIDENTS_ROLE)) return <AccessRestricted />;

  const query = await searchParams;
  const searchType = getSearchType(getQueryValue(query.type) ?? getQueryValue(query.Radio1));
  const searchTerm = (getQueryValue(query.q) ?? getQueryValue(query.txtGGNum) ?? "").trim();
  const pageNumber = getPage(getQueryValue(query.page));
  const created = getQueryValue(query.created) === "1";
  const updated = getQueryValue(query.updated) === "1";

  let pageData;
  try {
    pageData = await getHqAccidentPage(pageNumber, searchType, searchTerm, 24, locationCode);
  } catch (error) {
    if (error instanceof AccidentApiError && error.reason === "unauthorized") {
      return <SessionRecovery returnPath="/accidents/hq" />;
    }
    console.error(
      "FIS HQ accident request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return <ApiUnavailable />;
  }

  return (
    <>
      {created ? (
        <div className="notice notice-success" role="status">
          Accident created successfully.
        </div>
      ) : null}
      {updated ? (
        <div className="notice notice-success" role="status">
          Accident updated successfully.
        </div>
      ) : null}
      <HqSearchForm searchType={searchType} searchTerm={searchTerm} />
      <HqPageResults
        pageData={pageData}
        searchType={searchType}
        searchTerm={searchTerm}
        locationCode={locationCode}
      />

      <div className="vehicle-footer-actions">
        <Link
          className="button button-primary"
          href={buildHqAddHref(searchType, searchTerm, locationCode)}
        >
          Add New
        </Link>
        <Link className="button button-secondary" href="/accidents">
          Menu
        </Link>
        <Link className="button button-secondary" href="/home">
          Home
        </Link>
        <form action={logoutAction}>
          <button className="button button-secondary" type="submit">
            Sign out
          </button>
        </form>
      </div>
    </>
  );
}

export function HqAccidentRoute({ searchParams, locationCode }: HqPageProps) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="hq-accidents-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Accident maintenance</p>
            <h1 id="hq-accidents-title">Accident Maintenance (HQ)</h1>
            <p>Search by GG or GP number, then edit or add HQ accidents.</p>
          </div>
          <Link className="button button-secondary" href="/accidents">
            Accident Menu
          </Link>
        </header>
        <Suspense fallback={<LoadingState />}>
          <HqContent searchParams={searchParams} locationCode={locationCode} />
        </Suspense>
      </section>
    </main>
  );
}

export default function HqAccidentPage({ searchParams }: Pick<HqPageProps, "searchParams">) {
  return <HqAccidentRoute searchParams={searchParams} />;
}
