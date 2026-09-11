import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";
import { Suspense } from "react";

import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  hasAssetVerificationAccess,
  getQueryValue,
} from "@/app/(fleet-operations)/vehicle-verification/access";
import {
  searchVehiclesAgainstApi,
  type VehicleSearchResult,
  VehicleCreateApiError,
} from "@/lib/api/vehicles/api-vehicle-create";
import { getSession } from "@/lib/auth/session";
import RouteLoading from "@/components/app-shell/route-loading";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

function filterVehicles(vehicles: VehicleSearchResult[], searchMode: "GG" | "GP", query: string) {
  const term = query.trim().toLocaleLowerCase();
  return vehicles
    .filter((vehicle) => {
      const value = searchMode === "GP" ? vehicle.registrationNumber : vehicle.fleetNumber;
      return value?.trim().toLocaleLowerCase().includes(term) === true;
    })
    .toSorted((left, right) => {
      const leftValue = searchMode === "GP" ? left.registrationNumber : left.fleetNumber;
      const rightValue = searchMode === "GP" ? right.registrationNumber : right.fleetNumber;
      return (
        (leftValue?.trim().toLocaleLowerCase() === term ? 0 : 1) -
          (rightValue?.trim().toLocaleLowerCase() === term ? 0 : 1) || left.vmfCode - right.vmfCode
      );
    });
}

const VehicleVerificationSearchContent = renderVehicleVerificationSearchContent;

async function renderVehicleVerificationSearchContent({
  mode,
  searchParams,
  routePath,
}: Readonly<{ mode: "add" | "edit"; searchParams: SearchParams; routePath: string }>) {
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
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">API unavailable</p>
          <h1>Vehicle search is unavailable.</h1>
          <Link className="button button-primary" href={routePath}>
            Try again
          </Link>
        </section>
      </main>
    );
  if (!hasAssetVerificationAccess(session.roles, session.accessLevel))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h1>You do not have permission to maintain asset verification.</h1>
          <Link className="button button-secondary" href="/vehicle-verification">
            Back
          </Link>
        </section>
      </main>
    );

  const query = await searchParams;
  const searchTerm = (getQueryValue(query.q) ?? getQueryValue(query.txtGGNum) ?? "")
    .trim()
    .slice(0, 30);
  const searchMode =
    getQueryValue(query.searchMode) === "GP" || getQueryValue(query.Radio1) === "Radiogp"
      ? "GP"
      : "GG";
  let matches: VehicleSearchResult[] = [];
  let errorMessage: string | null = null;
  if (searchTerm) {
    try {
      matches = filterVehicles(await searchVehiclesAgainstApi(searchTerm), searchMode, searchTerm);
      if (matches.length === 0) errorMessage = "No vehicles matched your search.";
    } catch (error) {
      if (error instanceof VehicleCreateApiError && error.reason === "unauthorized")
        return (
          <main className="page-shell vehicle-page-shell">
            <SessionRecovery returnPath={routePath} />
          </main>
        );
      errorMessage =
        "The vehicle search could not be completed. Retry when the FIS API is available.";
    }
  }
  const detailsBase = `/vehicle-verification/${mode}/details`;
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="asset-verification-search-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Vehicle asset verification</p>
            <h1 id="asset-verification-search-title">
              {mode === "add" ? "Add" : "Edit"} Asset Verification
            </h1>
            <p>Search and select a vehicle to continue.</p>
          </div>
          <Link className="button button-secondary" href="/vehicle-verification">
            Menu
          </Link>
        </header>
        <form className="vehicle-status-maintenance-panel" method="get">
          <div className="form-grid">
            <fieldset className="vehicle-search-options">
              <legend>Find vehicle by</legend>
              <label className="vehicle-checkbox-label">
                <input
                  type="radio"
                  name="searchMode"
                  value="GG"
                  defaultChecked={searchMode === "GG"}
                />{" "}
                GG
              </label>
              <label className="vehicle-checkbox-label">
                <input
                  type="radio"
                  name="searchMode"
                  value="GP"
                  defaultChecked={searchMode === "GP"}
                />{" "}
                Reg Number
              </label>
            </fieldset>
            <div className="form-field">
              <label className="form-label" htmlFor="asset-verification-search">
                Vehicle Number <span className="required">*</span>
              </label>
              <input
                className="form-input"
                id="asset-verification-search"
                name="q"
                defaultValue={searchTerm}
                maxLength={30}
                placeholder={searchMode === "GG" ? "Enter GG number" : "Enter GP number"}
                required
              />
            </div>
          </div>
          <div className="button-row">
            <button className="button button-primary" type="submit">
              Find
            </button>
            <Link className="button button-secondary" href="/vehicle-verification">
              Cancel
            </Link>
          </div>
        </form>
        {errorMessage ? (
          <div className="notice notice-error" role="alert">
            {errorMessage}
          </div>
        ) : null}
        {matches.length > 0 ? (
          <section
            className="vehicle-status-maintenance-panel"
            aria-labelledby="asset-verification-match-title"
          >
            <div className="vehicle-form-section-header">
              <div>
                <p className="eyebrow">Vehicle matches</p>
                <h2 id="asset-verification-match-title">Select a vehicle</h2>
              </div>
              <span className="form-hint">{matches.length} match(es)</span>
            </div>
            <div className="vehicle-table-wrapper">
              <table className="vehicle-table">
                <caption className="sr-only">
                  Vehicles matching the asset verification search
                </caption>
                <DataTableHeader
                  columns={[
                    { key: "column-1", label: <>GG Number</> },
                    { key: "column-2", label: <>Registration</> },
                    { key: "column-3", label: <>Chassis</> },
                    { key: "column-4", label: <>Action</> },
                  ]}
                />
                <tbody>
                  {matches.map((vehicle) => (
                    <tr key={vehicle.vmfCode}>
                      <td>{vehicle.fleetNumber ?? "-"}</td>
                      <td>{vehicle.registrationNumber ?? "-"}</td>
                      <td>{vehicle.chassisNumber ?? "-"}</td>
                      <td>
                        <Link
                          className="button button-primary button-small"
                          href={`${detailsBase}?gg=${encodeURIComponent(searchMode === "GP" ? (vehicle.registrationNumber ?? vehicle.fleetNumber ?? "") : (vehicle.fleetNumber ?? vehicle.registrationNumber ?? ""))}`}
                        >
                          Select
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        ) : null}
      </section>
    </main>
  );
}

export function VehicleVerificationSearchPage(
  props: Readonly<{ mode: "add" | "edit"; searchParams: SearchParams; routePath: string }>,
) {
  return (
    <Suspense fallback={<RouteLoading />}>
      <VehicleVerificationSearchContent {...props} />
    </Suspense>
  );
}
