import DataTableHeader from "@/components/ui/data-table-header";

import { Suspense } from "react";

import RouteLoading from "@/components/app-shell/route-loading";

import Link from "next/link";

import {
  hasTripAuthorityAccess,
  parsePositiveInteger,
  queryValue,
  getTripSession,
  tripAccessRestricted,
  tripSessionMessage,
} from "@/app/(fleet-operations)/trips/_page";
import { getDepartments } from "@/lib/api/reference-data/api-departments";
import { getSites } from "@/lib/api/reference-data/api-sites";
import {
  DEFAULT_TRIP_AUTHORITY_PAGE_SIZE,
  emptyTripAuthorityVehiclePage,
  getTripAuthorityInPage,
  getTripAuthorityOutPage,
  type TripAuthorityVehiclePage,
  type TripAuthorityVehiclePageOptions,
} from "@/lib/api/fleet-operations/api-trip-authorities";
import { MenuSection } from "@/components/ui/menu-section";

type SearchParams = Record<string, string | string[] | undefined>;
type Tab = "gg" | "department" | "authority";
type SearchMode = "GG" | "GP";
type AccessMode = "SITE" | "DEP" | "ALL";
type DisplayRow = TripAuthorityVehiclePage["items"][number];

const PAGE_SIZE = DEFAULT_TRIP_AUTHORITY_PAGE_SIZE;

function selectedTab(value: string): Tab {
  return value === "department" || value === "authority" ? value : "gg";
}

function selectedSearchMode(value: string): SearchMode {
  return value.toUpperCase() === "GP" ? "GP" : "GG";
}

function asContextNumber(value: string | undefined) {
  return value && Number.isSafeInteger(Number(value)) ? Number(value) : null;
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function accessMode(
  session: Extract<Awaited<ReturnType<typeof getTripSession>>, { status: "authenticated" }>,
): AccessMode {
  if (
    session.roles.some(
      (role) =>
        role.localeCompare("VehicleListForAllDepartmentsInProvince", undefined, {
          sensitivity: "accent",
        }) === 0,
    )
  ) {
    return "ALL";
  }

  if (
    session.roles.some(
      (role) =>
        role.localeCompare("VehicleListForAllSitesInDepartment", undefined, {
          sensitivity: "accent",
        }) === 0,
    )
  ) {
    return "DEP";
  }

  return "SITE";
}

function hrefWithValues(path: string, values: Record<string, string | number | undefined>) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(values)) {
    if (value !== undefined && String(value) !== "") params.set(key, String(value));
  }

  const query = params.toString();
  return query ? `${path}?${query}` : path;
}

function TripTools() {
  return (
    <div className="vehicle-menu-tiles">
      <MenuSection title="Trip Tools">
        <Link className="vehicle-menu-link" href="/trips/create">
          Create Trip Authority
        </Link>
        <Link className="vehicle-menu-link" href="/trips/show">
          View / Close Trip Authority
        </Link>
        <Link className="vehicle-menu-link" href="/drivers">
          Maintain Drivers
        </Link>
        <Link className="vehicle-menu-link" href="/trips/driver-licence-details">
          Driver Licence Details
        </Link>
        <Link className="vehicle-menu-link" href="/trips/driver-info">
          Driver Information (Financial Year)
        </Link>
        <Link className="vehicle-menu-link" href="/trips/remove-without-routes">
          Remove Trips Without Routes
        </Link>
      </MenuSection>
    </div>
  );
}

function SearchTabs({
  tab,
  departmentCode,
  siteCode,
}: Readonly<{ tab: Tab; departmentCode: number | null; siteCode: number | null }>) {
  const values = { departmentCode: departmentCode ?? undefined, siteCode: siteCode ?? undefined };
  return (
    <nav className="reference-data-tabs" aria-label="Trip authority filters">
      <Link
        className={`reference-data-tab${tab === "gg" ? " active" : ""}`}
        aria-current={tab === "gg" ? "page" : undefined}
        href={hrefWithValues("/trip-authorities", { tab: "gg", ...values })}
      >
        GG Number
      </Link>
      <Link
        className={`reference-data-tab${tab === "department" ? " active" : ""}`}
        aria-current={tab === "department" ? "page" : undefined}
        href={hrefWithValues("/trip-authorities", { tab: "department", ...values })}
      >
        Department / Site
      </Link>
      <Link
        className={`reference-data-tab${tab === "authority" ? " active" : ""}`}
        aria-current={tab === "authority" ? "page" : undefined}
        href={hrefWithValues("/trip-authorities", { tab: "authority", ...values })}
      >
        Trip Authority Number
      </Link>
    </nav>
  );
}

function FilterForm({
  tab,
  mode,
  searchMode,
  number,
  selectedDepartment,
  selectedSite,
  authority,
  departments,
  sites,
}: Readonly<{
  tab: Tab;
  mode: AccessMode;
  searchMode: SearchMode;
  number: string;
  selectedDepartment: number | null;
  selectedSite: number | null;
  authority: string;
  departments: readonly { departmentCode: number; description: string | null }[];
  sites: readonly { siteCode: number; departmentCode: number | null; description: string | null }[];
}>) {
  if (tab === "gg") {
    return (
      <form className="vehicle-status-maintenance-panel" method="get">
        <input name="tab" type="hidden" value="gg" />
        <div className="form-grid">
          <fieldset className="vehicle-search-options">
            <legend>Number type</legend>
            <label className="vehicle-checkbox-label">
              <input
                name="searchMode"
                type="radio"
                value="GG"
                defaultChecked={searchMode === "GG"}
              />{" "}
              GG
            </label>
            <label className="vehicle-checkbox-label">
              <input
                name="searchMode"
                type="radio"
                value="GP"
                defaultChecked={searchMode === "GP"}
              />{" "}
              GP
            </label>
          </fieldset>
          <div className="form-field">
            <label className="form-label" htmlFor="trip-vehicle-number">
              {searchMode === "GG" ? "GG Number" : "GP Number"}
            </label>
            <input
              className="form-input"
              id="trip-vehicle-number"
              name="number"
              defaultValue={number}
              maxLength={50}
              placeholder={searchMode === "GG" ? "Search by GG number" : "Search by GP number"}
            />
          </div>
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            Search
          </button>
          <Link className="button button-secondary" href="/trip-authorities">
            Clear
          </Link>
        </div>
      </form>
    );
  }

  if (tab === "authority") {
    return (
      <form className="vehicle-status-maintenance-panel" method="get">
        <input name="tab" type="hidden" value="authority" />
        <div className="form-grid">
          <div className="form-field">
            <label className="form-label" htmlFor="trip-authority-number">
              Trip Authority Number
            </label>
            <input
              className="form-input"
              id="trip-authority-number"
              name="authority"
              inputMode="numeric"
              defaultValue={authority}
              placeholder="Numbers only"
            />
          </div>
        </div>
        <div className="button-row">
          <button className="button button-primary" type="submit">
            Search
          </button>
          <Link className="button button-secondary" href="/trip-authorities">
            Clear
          </Link>
        </div>
      </form>
    );
  }

  const filteredSites =
    selectedDepartment === null
      ? sites
      : sites.filter((site) => site.departmentCode === selectedDepartment);
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <input name="tab" type="hidden" value="department" />
      <div className="form-grid">
        <div className="form-field">
          <label className="form-label" htmlFor="trip-department">
            Department
          </label>
          <select
            className="form-select"
            id="trip-department"
            name="departmentCode"
            defaultValue={selectedDepartment?.toString() ?? ""}
            disabled={mode === "SITE" || mode === "DEP"}
          >
            <option value="">Select Department</option>
            {departments.map((department) => (
              <option key={department.departmentCode} value={department.departmentCode}>
                {valueOrDash(department.description)} ({department.departmentCode})
              </option>
            ))}
          </select>
          {mode !== "ALL" && selectedDepartment !== null ? (
            <input name="departmentCode" type="hidden" value={selectedDepartment} />
          ) : null}
        </div>
        <div className="form-field">
          <label className="form-label" htmlFor="trip-site">
            Site
          </label>
          <select
            className="form-select"
            id="trip-site"
            name="siteCode"
            defaultValue={selectedSite?.toString() ?? ""}
            disabled={mode === "SITE"}
          >
            <option value="">Select Site</option>
            {filteredSites.map((site) => (
              <option key={site.siteCode} value={site.siteCode}>
                {valueOrDash(site.description)} ({site.siteCode})
              </option>
            ))}
          </select>
          {mode === "SITE" && selectedSite !== null ? (
            <input name="siteCode" type="hidden" value={selectedSite} />
          ) : null}
        </div>
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Apply filters
        </button>
        <Link className="button button-secondary" href="/trip-authorities?tab=department">
          Clear
        </Link>
      </div>
    </form>
  );
}

function VehicleTable({
  title,
  emptyText,
  result,
  pageParam,
  queryValues,
  action,
}: Readonly<{
  title: string;
  emptyText: string;
  result: TripAuthorityVehiclePage;
  pageParam: "pageIn" | "pageOut";
  queryValues: Record<string, string | number | undefined>;
  action: (row: DisplayRow) => string;
}>) {
  const pageCount = Math.max(1, result.totalPages);
  const safePage = Math.min(Math.max(result.page, 1), pageCount);
  const previousHref = hrefWithValues("/trip-authorities", {
    ...queryValues,
    [pageParam]: safePage - 1,
  });
  const nextHref = hrefWithValues("/trip-authorities", {
    ...queryValues,
    [pageParam]: safePage + 1,
  });
  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby={`${title.toLowerCase().replaceAll(" ", "-")}-title`}
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">
            {result.total} record{result.total === 1 ? "" : "s"}
          </p>
          <h2 id={`${title.toLowerCase().replaceAll(" ", "-")}-title`}>{title}</h2>
        </div>
      </div>
      {result.items.length === 0 ? (
        <div className="vehicle-empty-state">
          <p>{emptyText}</p>
        </div>
      ) : (
        <>
          <div className="vehicle-table-wrapper">
            <table className="vehicle-table">
              <caption className="sr-only">{title}</caption>
              <DataTableHeader
                columns={[
                  { key: "column-1", label: <>Fleet Number</> },
                  { key: "column-2", label: <>Registration Number</> },
                  { key: "column-3", label: <>License Disk Expires</> },
                  { key: "column-4", label: <>Make Description</> },
                  { key: "column-5", label: <>Model Description</> },
                  { key: "column-6", label: <>Contract Type</> },
                  { key: "column-7", label: <>Action</> },
                ]}
              />
              <tbody>
                {result.items.map((row) => (
                  <tr key={`${title}-${row.vmfCode}-${row.tripId ?? "in"}`}>
                    <td>{valueOrDash(row.fleetNumber)}</td>
                    <td>{valueOrDash(row.registrationNumber)}</td>
                    <td>{formatDate(row.licenceDueDate)}</td>
                    <td>{valueOrDash(row.make)}</td>
                    <td>{valueOrDash(row.model)}</td>
                    <td>{valueOrDash(row.contractType)}</td>
                    <td>
                      <Link className="button button-secondary" href={action(row)}>
                        {row.tripId ? "View" : "Select"}
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {pageCount > 1 ? (
            <nav className="vehicle-pagination" aria-label={`${title} pages`}>
              <span className="vehicle-pagination-meta">
                Page {safePage} of {pageCount}
              </span>
              {safePage > 1 ? (
                <Link className="button button-secondary" href={previousHref}>
                  Previous
                </Link>
              ) : null}
              {safePage < pageCount ? (
                <Link className="button button-secondary" href={nextHref}>
                  Next
                </Link>
              ) : null}
            </nav>
          ) : null}
        </>
      )}
    </section>
  );
}

const TripAuthoritiesPageContent = renderTripAuthoritiesPageContent;

async function renderTripAuthoritiesPageContent({
  searchParams,
}: Readonly<{ searchParams: Promise<SearchParams> }>) {
  const session = await getTripSession();
  const sessionProblem = tripSessionMessage(session, "/trip-authorities");
  if (sessionProblem) return sessionProblem;
  if (session.status !== "authenticated")
    return tripAccessRestricted("Your session could not be loaded.");
  if (!hasTripAuthorityAccess(session)) return tripAccessRestricted();

  const query = await searchParams;
  const tab = selectedTab(queryValue(query.tab));
  const searchMode = selectedSearchMode(queryValue(query.searchMode));
  const number = queryValue(query.number || query.search).trim();
  const authority = queryValue(query.authority).trim();
  const mode = accessMode(session);
  const currentDepartment = asContextNumber(session.departmentCode);
  const currentSite = asContextNumber(session.siteCode);
  const requestedDepartment = asContextNumber(
    queryValue(query.departmentCode) || queryValue(query.department),
  );
  const requestedSite = asContextNumber(queryValue(query.siteCode) || queryValue(query.site));
  const selectedDepartment =
    mode === "SITE" || mode === "DEP" ? currentDepartment : requestedDepartment;
  const selectedSite = mode === "SITE" ? currentSite : requestedSite;
  const pageIn = parsePositiveInteger(queryValue(query.pageIn)) ?? 1;
  const pageOut = parsePositiveInteger(queryValue(query.pageOut)) ?? 1;
  const authorityNumber = tab === "authority" ? parsePositiveInteger(authority) : null;
  const hasValidLocationFilters =
    (selectedDepartment === null || selectedDepartment > 0) &&
    (selectedSite === null || selectedSite > 0);
  const hasVehicleScope =
    hasValidLocationFilters &&
    (mode === "ALL" ||
      (mode === "SITE"
        ? currentSite !== null && currentSite > 0
        : currentDepartment !== null && currentDepartment > 0));
  const canQueryVehiclePages = hasVehicleScope && (tab !== "authority" || authorityNumber !== null);
  const pageQuery: TripAuthorityVehiclePageOptions = {
    searchMode: tab === "gg" ? searchMode : "GG",
    number: tab === "gg" ? number : undefined,
    departmentCode: selectedDepartment ?? undefined,
    siteCode: selectedSite ?? undefined,
    authority: authorityNumber ?? undefined,
    pageSize: PAGE_SIZE,
  };

  const [inPageResult, outPageResult, departmentsResult, sitesResult] = await Promise.allSettled([
    canQueryVehiclePages
      ? getTripAuthorityInPage({ ...pageQuery, page: pageIn })
      : Promise.resolve(emptyTripAuthorityVehiclePage(pageIn)),
    canQueryVehiclePages
      ? getTripAuthorityOutPage({ ...pageQuery, page: pageOut })
      : Promise.resolve(emptyTripAuthorityVehiclePage(pageOut)),
    getDepartments(),
    getSites(),
  ]);
  const inPage =
    inPageResult.status === "fulfilled" ? inPageResult.value : emptyTripAuthorityVehiclePage();
  const outPage =
    outPageResult.status === "fulfilled" ? outPageResult.value : emptyTripAuthorityVehiclePage();
  const departments = departmentsResult.status === "fulfilled" ? departmentsResult.value : [];
  const sites = sitesResult.status === "fulfilled" ? sitesResult.value : [];
  const unavailable = [inPageResult, outPageResult, departmentsResult, sitesResult].some(
    (result) => result.status === "rejected",
  );

  const hrefContext = {
    departmentCode: selectedDepartment ?? undefined,
    siteCode: selectedSite ?? undefined,
  };
  const queryValues = {
    tab,
    searchMode: tab === "gg" ? searchMode : undefined,
    number: tab === "gg" ? number : undefined,
    authority: tab === "authority" ? authority : undefined,
    ...hrefContext,
    pageIn: inPage.page,
    pageOut: outPage.page,
  };
  const apiProblem = unavailable
    ? "Some trip authority data could not be loaded. The available results are shown; retry when the API is available."
    : null;

  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Trips</p>
            <h1>Trip Authority</h1>
            <p>
              Select a tab below to change filtering from GG Number to Department &amp; Site to Trip
              Authority Number.
            </p>
          </div>
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </header>
        <TripTools />
        <section className="vehicle-status-maintenance-panel">
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Access mode: {mode}</p>
              <h2>Location context</h2>
            </div>
          </div>
          <p className="muted-copy">
            My site: {valueOrDash(currentSite)} · My department: {valueOrDash(currentDepartment)}
          </p>
        </section>
        {apiProblem ? (
          <div className="notice notice-error" role="alert">
            {apiProblem}
          </div>
        ) : null}
        <SearchTabs tab={tab} departmentCode={selectedDepartment} siteCode={selectedSite} />
        <FilterForm
          tab={tab}
          mode={mode}
          searchMode={searchMode}
          number={number}
          selectedDepartment={selectedDepartment}
          selectedSite={selectedSite}
          authority={authority}
          departments={departments}
          sites={sites}
        />
        <VehicleTable
          title="(IN) Vehicles available for a trip."
          emptyText="No in-service vehicles available for trip assignment."
          result={inPage}
          pageParam="pageIn"
          queryValues={queryValues}
          action={(row) =>
            hrefWithValues("/trips/create", {
              mode: "New",
              vmfCode: row.vmfCode,
              site: row.siteCode ?? undefined,
              department: selectedDepartment ?? undefined,
            })
          }
        />
        <VehicleTable
          title="(OUT) Vehicles allocated to a trip."
          emptyText="No vehicles were found that are currently OUT on a trip."
          result={outPage}
          pageParam="pageOut"
          queryValues={queryValues}
          action={(row) =>
            hrefWithValues("/trips/show", {
              tripId: row.tripId ?? undefined,
              vmfCode: row.vmfCode,
              site: row.siteCode ?? undefined,
              department: selectedDepartment ?? undefined,
            })
          }
        />
        <div className="vehicle-footer-actions">
          <Link className="button button-secondary" href="/home">
            Home
          </Link>
        </div>
      </section>
    </main>
  );
}

export default function TripAuthoritiesPage(
  props: Parameters<typeof TripAuthoritiesPageContent>[0],
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <TripAuthoritiesPageContent {...props} />
    </Suspense>
  );
}
