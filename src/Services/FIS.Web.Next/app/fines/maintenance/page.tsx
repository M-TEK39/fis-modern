import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import SessionRecovery from "@/app/home/session-recovery";
import {
  FineApiError,
  getFines,
  searchFineVehicles,
  type FineRecord,
  type FineSearchType,
  type FineVehicleOption,
} from "@/lib/api-fines";
import { getSession } from "@/lib/session";

const REPORTS_ROLE = "Reports";

export type FineMaintenancePageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
  routePath?: string;
};

function getQueryValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] : value;
}

function getSearchType(value: string | undefined): FineSearchType {
  return value === "GG" || value === "Radiogg" ? "GG" : "GP";
}

function hasReportsRole(roles: readonly string[]) {
  return roles.some(
    (role) => role.localeCompare(REPORTS_ROLE, undefined, { sensitivity: "accent" }) === 0,
  );
}

function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

function formatDate(value: string | null) {
  return value?.slice(0, 10) || "-";
}

function buildDetailHref(searchType: FineSearchType, searchQuery: string, vmfCode?: number | null) {
  const params = new URLSearchParams();
  if (vmfCode) {
    params.set("vmfCode", String(vmfCode));
  }
  if (searchQuery) {
    params.set("searchType", searchType);
    params.set("searchQuery", searchQuery);
  }
  const query = params.toString();
  return `/fines/maintenance/detail${query ? `?${query}` : ""}`;
}

function FineSearchForm({
  searchType,
  searchQuery,
}: Readonly<{ searchType: FineSearchType; searchQuery: string }>) {
  return (
    <form className="vehicle-status-maintenance-panel" method="get">
      <fieldset className="vehicle-search-options">
        <legend>Search by</legend>
        <label className="vehicle-checkbox-label">
          <input type="radio" name="searchType" value="GG" defaultChecked={searchType === "GG"} />{" "}
          GG
        </label>
        <label className="vehicle-checkbox-label">
          <input type="radio" name="searchType" value="GP" defaultChecked={searchType === "GP"} />{" "}
          GP
        </label>
      </fieldset>
      <div className="vehicle-search-row">
        <label className="sr-only" htmlFor="fine-vehicle-search">
          {searchType === "GG" ? "GG number" : "GP number"}
        </label>
        <input
          className="vehicle-search"
          id="fine-vehicle-search"
          maxLength={8}
          name="searchQuery"
          placeholder={
            searchType === "GG" ? "Enter GG number" : "Enter current or historical GP number"
          }
          defaultValue={searchQuery}
        />
      </div>
      <div className="button-row">
        <button className="button button-primary" type="submit">
          Submit
        </button>
        <Link className="button button-secondary" href="/fines">
          Menu
        </Link>
      </div>
    </form>
  );
}

function VehicleResolution({
  searchType,
  vehicles,
}: Readonly<{ searchType: FineSearchType; vehicles: FineVehicleOption[] }>) {
  if (vehicles.length === 0) {
    return null;
  }

  return (
    <section
      className="vehicle-status-maintenance-panel"
      aria-labelledby="fine-vehicle-resolution-title"
    >
      <p className="eyebrow">Vehicle resolution</p>
      <h2 id="fine-vehicle-resolution-title">Matched vehicle{vehicles.length === 1 ? "" : "s"}</h2>
      <ul className="form-hint fis-list-pad">
        {vehicles.slice(0, 5).map((vehicle) => (
          <li key={vehicle.vmfCode}>
            {valueOrDash(vehicle.fleetNumber)} / {valueOrDash(vehicle.registrationNumber)} (
            {vehicle.vmfCode})
            {searchType === "GP" && vehicle.isHistoricalMatch && vehicle.matchedRegistration
              ? ` — historical GP ${vehicle.matchedRegistration}`
              : ""}
          </li>
        ))}
      </ul>
    </section>
  );
}

function FineTable({
  fines,
  searchType,
  searchQuery,
}: Readonly<{ fines: FineRecord[]; searchType: FineSearchType; searchQuery: string }>) {
  if (fines.length === 0) {
    return (
      <div className="vehicle-empty-state">
        <p className="eyebrow">No records found</p>
        <h2>{searchQuery ? `No fines matched “${searchQuery}”.` : "No fines are available."}</h2>
        <p className="muted-copy">
          Try another GG or GP number, or add a fine for a selected vehicle.
        </p>
      </div>
    );
  }

  return (
    <div className="vehicle-table-wrapper" aria-live="polite">
      <table className="vehicle-table">
        <caption className="sr-only">Fines available for maintenance</caption>
        <thead>
          <tr>
            <th scope="col">Offence Date</th>
            <th scope="col">Document Type</th>
            <th scope="col">Date at GMT</th>
            <th scope="col">Action</th>
          </tr>
        </thead>
        <tbody>
          {fines.map((fine) => (
            <tr key={fine.fineCode}>
              <td>{formatDate(fine.offenceDate)}</td>
              <td>{valueOrDash(fine.documentType ?? fine.offenceName)}</td>
              <td>{formatDate(fine.receiveGgDate)}</td>
              <td>
                <Link
                  className="button button-secondary button-small"
                  href={`/fines/maintenance/detail?fineId=${fine.fineCode}`}
                >
                  Mod
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
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
      <h2>Fines maintenance could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href="/fines/maintenance">
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

export default async function FineMaintenancePage({
  searchParams,
  routePath = "/fines/maintenance",
}: FineMaintenancePageProps) {
  await connection();
  const session = await getSession();

  if (session.status === "anonymous") {
    redirect("/login");
  }

  if (session.status === "expired") {
    return <SessionRecovery returnPath={routePath} />;
  }

  if (session.status === "unavailable") {
    return <ApiUnavailable />;
  }

  if (!hasReportsRole(session.roles)) {
    return (
      <section className="vehicle-status-card" role="alert">
        <div className="status-icon status-icon-error" aria-hidden="true">
          !
        </div>
        <p className="eyebrow">Access restricted</p>
        <h2>You do not have permission to maintain Fines.</h2>
      </section>
    );
  }

  const query = await searchParams;
  const searchType = getSearchType(getQueryValue(query.searchType) ?? getQueryValue(query.Radio1));
  const searchQuery = (getQueryValue(query.searchQuery) ?? getQueryValue(query.txtGGNum) ?? "")
    .trim()
    .slice(0, 8);
  const notice =
    getQueryValue(query.saved) === "1"
      ? "Fine captured successfully."
      : getQueryValue(query.updated) === "1"
        ? "Fine updated successfully."
        : getQueryValue(query.error);

  try {
    const [allFines, vehicles] = await Promise.all([
      getFines(),
      searchQuery
        ? searchFineVehicles(searchType, searchQuery)
        : Promise.resolve([] as FineVehicleOption[]),
    ]);
    const matchingCodes = searchQuery ? new Set(vehicles.map((vehicle) => vehicle.vmfCode)) : null;
    const fines =
      matchingCodes === null
        ? allFines
        : allFines.filter((fine) => fine.vmfCode !== null && matchingCodes.has(fine.vmfCode));
    const addHref = buildDetailHref(
      searchType,
      searchQuery,
      vehicles.length === 1 ? vehicles[0].vmfCode : null,
    );

    return (
      <>
        {notice ? (
          <div
            className={getQueryValue(query.error) ? "notice notice-error" : "notice notice-success"}
            role="status"
          >
            {notice}
          </div>
        ) : null}
        <FineSearchForm searchType={searchType} searchQuery={searchQuery} />
        <VehicleResolution searchType={searchType} vehicles={vehicles} />
        <section
          className="vehicle-status-maintenance-panel"
          aria-labelledby="fine-maintenance-results-title"
        >
          <div className="vehicle-form-section-header">
            <div>
              <p className="eyebrow">Fine maintenance</p>
              <h2 id="fine-maintenance-results-title">Fines found</h2>
            </div>
            <Link className="button button-primary" href={addHref}>
              Add
            </Link>
          </div>
          <FineTable fines={fines} searchType={searchType} searchQuery={searchQuery} />
        </section>
      </>
    );
  } catch (error) {
    if (error instanceof FineApiError && error.reason === "unauthorized") {
      return <SessionRecovery returnPath={routePath} />;
    }

    console.error(
      "FIS fine maintenance request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return <ApiUnavailable />;
  }
}
