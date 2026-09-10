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
  getTripAuthorities,
  getTripAuthorityVehicles,
  type TripAuthorityVehicle,
} from "@/lib/api/fleet-operations/api-trip-authorities";
import { MenuSection } from "@/components/ui/menu-section";

type SearchParams = Record<string, string | string[] | undefined>;
type Tab = "gg" | "department" | "authority";
type SearchMode = "GG" | "GP";
type AccessMode = "SITE" | "DEP" | "ALL";
type DisplayRow = TripAuthorityVehicle & { tripId?: number; contractCode?: number };

const PAGE_SIZE = 12;

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

function matchesMode(
  rowSiteCode: number | null,
  mode: AccessMode,
  currentDepartment: number | null,
  currentSite: number | null,
  siteDepartments: Map<number, number | null>,
) {
  if (mode === "ALL") return true;
  if (mode === "SITE") return currentSite !== null && rowSiteCode === currentSite;
  return (
    currentDepartment !== null &&
    rowSiteCode !== null &&
    siteDepartments.get(rowSiteCode) === currentDepartment
  );
}

function matchesLocation(
  rowSiteCode: number | null,
  selectedDepartment: number | null,
  selectedSite: number | null,
  siteDepartments: Map<number, number | null>,
) {
  if (selectedSite !== null && rowSiteCode !== selectedSite) return false;
  return (
    selectedDepartment === null ||
    (rowSiteCode !== null && siteDepartments.get(rowSiteCode) === selectedDepartment)
  );
}

function matchesNumber(row: DisplayRow, number: string, mode: SearchMode) {
  if (!number) return true;
  const candidate = mode === "GG" ? row.fleetNumber : row.registrationNumber;
  return candidate?.toLocaleLowerCase().includes(number.toLocaleLowerCase()) === true;
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
  rows,
  page,
  pageParam,
  queryValues,
  action,
}: Readonly<{
  title: string;
  emptyText: string;
  rows: readonly DisplayRow[];
  page: number;
  pageParam: "pageIn" | "pageOut";
  queryValues: Record<string, string | number | undefined>;
  action: (row: DisplayRow) => string;
}>) {
  const pageCount = Math.max(1, Math.ceil(rows.length / PAGE_SIZE));
  const safePage = Math.min(Math.max(page, 1), pageCount);
  const visibleRows = rows.slice((safePage - 1) * PAGE_SIZE, safePage * PAGE_SIZE);
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
            {rows.length} record{rows.length === 1 ? "" : "s"}
          </p>
          <h2 id={`${title.toLowerCase().replaceAll(" ", "-")}-title`}>{title}</h2>
        </div>
      </div>
      {rows.length === 0 ? (
        <div className="vehicle-empty-state">
          <p>{emptyText}</p>
        </div>
      ) : (
        <>
          <div className="vehicle-table-wrapper">
            <table className="vehicle-table">
              <caption className="sr-only">{title}</caption>
              <thead>
                <tr>
                  <th scope="col">Fleet Number</th>
                  <th scope="col">Registration Number</th>
                  <th scope="col">License Disk Expires</th>
                  <th scope="col">Make Description</th>
                  <th scope="col">Model Description</th>
                  <th scope="col">Contract Type</th>
                  <th scope="col">Action</th>
                </tr>
              </thead>
              <tbody>
                {visibleRows.map((row) => (
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

export default async function TripAuthoritiesPage({
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

  const [vehiclesResult, tripsResult, departmentsResult, sitesResult] = await Promise.allSettled([
    getTripAuthorityVehicles(),
    getTripAuthorities(),
    getDepartments(),
    getSites(),
  ]);
  const vehicles = vehiclesResult.status === "fulfilled" ? vehiclesResult.value : [];
  const trips = tripsResult.status === "fulfilled" ? tripsResult.value : [];
  const departments = departmentsResult.status === "fulfilled" ? departmentsResult.value : [];
  const sites = sitesResult.status === "fulfilled" ? sitesResult.value : [];
  const unavailable = [vehiclesResult, tripsResult, departmentsResult, sitesResult].some(
    (result) => result.status === "rejected",
  );
  const siteDepartments = new Map(sites.map((site) => [site.siteCode, site.departmentCode]));
  const vehicleMap = new Map(vehicles.map((vehicle) => [vehicle.vmfCode, vehicle]));

  const openTrips = trips
    .filter((trip) => trip.endOdometer === null && trip.vmfCode !== null)
    .sort((left, right) => (right.issueDate ?? "").localeCompare(left.issueDate ?? ""));
  const outRows: DisplayRow[] = [];
  const seenOutVehicles = new Set<number>();
  for (const trip of openTrips) {
    if (trip.vmfCode === null || seenOutVehicles.has(trip.vmfCode)) continue;
    const vehicle = vehicleMap.get(trip.vmfCode);
    if (!vehicle) continue;
    seenOutVehicles.add(trip.vmfCode);
    outRows.push({ ...vehicle, tripId: trip.tripId, contractCode: trip.contractCode });
  }

  const accessible = (row: DisplayRow) =>
    matchesMode(row.siteCode, mode, currentDepartment, currentSite, siteDepartments) &&
    matchesLocation(row.siteCode, selectedDepartment, selectedSite, siteDepartments);
  let filteredOut = outRows.filter(accessible);
  let filteredIn = vehicles
    .filter((vehicle) => accessible(vehicle))
    .filter((vehicle) => !seenOutVehicles.has(vehicle.vmfCode));

  if (tab === "gg") {
    filteredOut = filteredOut.filter((row) => matchesNumber(row, number, searchMode));
    filteredIn = filteredIn.filter((row) => matchesNumber(row, number, searchMode));
  } else if (tab === "authority") {
    const authorityNumber = parsePositiveInteger(authority);
    filteredOut =
      authorityNumber === null ? [] : filteredOut.filter((row) => row.tripId === authorityNumber);
    filteredIn = [];
  }

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
          rows={filteredIn}
          page={pageIn}
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
          rows={filteredOut}
          page={pageOut}
          pageParam="pageOut"
          queryValues={queryValues}
          action={(row) =>
            hrefWithValues("/trips/show", {
              tripId: row.tripId,
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
