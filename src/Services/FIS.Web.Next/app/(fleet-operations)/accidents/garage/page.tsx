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
  getGarageAccidentPage,
  type GarageSearchType,
} from "@/lib/api/fleet-operations/api-accidents";
import { getSession } from "@/lib/auth/session";

const ACCIDENTS_ROLE = "Accidents";

type GaragePageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
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

function buildGarageHref(searchType: GarageSearchType, searchTerm: string, page: number) {
  const params = new URLSearchParams({ type: searchType, page: String(page) });
  if (searchTerm) {
    params.set("q", searchTerm);
  }

  return `/accidents/garage?${params.toString()}`;
}

function buildGarageAddHref(searchType: GarageSearchType, searchTerm: string) {
  const params = new URLSearchParams({ type: searchType });
  if (searchTerm) {
    params.set("q", searchTerm);
  }

  return `/accidents/garage/add?${params.toString()}`;
}

const garageTableColumns: readonly VehicleTableColumn<GarageAccidentRow>[] = [
  { key: "vehicleNumber", label: "Vehicle Number", render: (row) => row.vehicleNumber ?? "-" },
  { key: "hireType", label: "Hire Type", render: (row) => row.hireType ?? "-" },
  {
    key: "accidentDate",
    label: "Accident Date",
    render: (row) => row.accidentDate?.slice(0, 10) ?? "-",
  },
  { key: "reference", label: "GG Reference", render: (row) => row.reference ?? "-" },
  {
    key: "action",
    label: "Action",
    render: (row) => (
      <Link
        className="button button-secondary button-small"
        href={`/accidents/garage/edit?accidentId=${encodeURIComponent(row.accidentCode)}`}
      >
        Edit
      </Link>
    ),
  },
];

function GarageFallback() {
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
      <h2>Garage accident records could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/accidents/garage">
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
      <h2>You do not have permission to maintain garage accidents.</h2>
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
        {searchTerm
          ? `No accidents matched “${searchTerm}”.`
          : "No garage accidents are available."}
      </h2>
      <p className="muted-copy">Try another GG or GP number, or add a new accident record.</p>
    </div>
  );
}

function GarageSearchForm({
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
        <label className="sr-only" htmlFor="garage-search">
          {searchType === "GG" ? "GG Number" : "GP Number"}
        </label>
        <input
          className="vehicle-search"
          id="garage-search"
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

function GaragePageResults({
  pageData,
  searchTerm,
}: {
  pageData: GarageAccidentPage;
  searchTerm: string;
}) {
  if (pageData.totalRecords === 0) return <NoRecords searchTerm={searchTerm} />;

  return (
    <>
      <div className="vehicle-table-wrapper" aria-live="polite">
        <VehicleTable
          caption="Garage accident records"
          columns={garageTableColumns}
          rows={pageData.rows}
          rowKey={(row) => row.accidentCode}
        />
      </div>
      {pageData.totalPages > 1 ? (
        <nav className="vehicle-pagination" aria-label="Garage accident pages">
          {pageData.page > 1 ? (
            <Link
              className="vehicle-pagination-button"
              href={buildGarageHref(pageData.searchType, pageData.searchTerm, pageData.page - 1)}
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
              href={buildGarageHref(pageData.searchType, pageData.searchTerm, pageData.page + 1)}
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

async function GarageContent({ searchParams }: GaragePageProps) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return <SessionRecovery returnPath="/accidents/garage" />;
  }

  if (session.status === "unavailable") {
    return <ApiUnavailable />;
  }

  if (!hasRole(session.roles, ACCIDENTS_ROLE)) {
    return <AccessRestricted />;
  }

  const query = await searchParams;
  const legacySearchType = getQueryValue(query.Radio1);
  const searchType = getSearchType(getQueryValue(query.type) ?? legacySearchType);
  const searchTerm = (getQueryValue(query.q) ?? getQueryValue(query.txtGGNum) ?? "").trim();
  const pageNumber = getPage(getQueryValue(query.page));
  const created = getQueryValue(query.created) === "1";
  const updated = getQueryValue(query.updated) === "1";

  let pageData;
  try {
    pageData = await getGarageAccidentPage(pageNumber, searchType, searchTerm);
  } catch (error) {
    if (error instanceof AccidentApiError && error.reason === "unauthorized") {
      return <SessionRecovery returnPath="/accidents/garage" />;
    }

    console.error(
      "FIS garage accident request failed",
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
      <GarageSearchForm searchType={searchType} searchTerm={searchTerm} />
      <GaragePageResults pageData={pageData} searchTerm={searchTerm} />

      <div className="vehicle-footer-actions">
        <Link className="button button-primary" href={buildGarageAddHref(searchType, searchTerm)}>
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

export default function GarageAccidentPage({ searchParams }: GaragePageProps) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="garage-accidents-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Accident maintenance</p>
            <h1 id="garage-accidents-title">Accident Maintenance (Garage)</h1>
            <p>Search by GG or GP number, then edit or add accidents.</p>
          </div>
          <Link className="button button-secondary" href="/accidents">
            Accident Menu
          </Link>
        </header>
        <Suspense fallback={<GarageFallback />}>
          <GarageContent searchParams={searchParams} />
        </Suspense>
      </section>
    </main>
  );
}
