import Link from "next/link";
import { redirect } from "next/navigation";
import { connection } from "next/server";

import { StreamedRoute } from "@/components/app-shell/streamed-route";
import SessionRecovery from "@/app/(workspace)/home/session-recovery";
import {
  PrivateHireContractorForm,
  PrivateHireContractorTable,
  PrivateHireNotice,
  PrivateHirePagination,
  PrivateHireVehicleForm,
  PrivateHireVehicleTable,
  queryValue,
} from "@/app/(fleet-operations)/private-hire/_components";
import {
  deletePrivateHireContractorAction,
  deletePrivateHireVehicleAction,
  savePrivateHireContractorAction,
  savePrivateHireVehicleAction,
} from "@/app/(fleet-operations)/private-hire/actions";
import {
  getPrivateHireContractor,
  getPrivateHireContractors,
  getPrivateHireContractorsPage,
  getPrivateHireVehicle,
  getPrivateHirePage,
  PrivateHireApiError,
  DEFAULT_PRIVATE_HIRE_PAGE_SIZE,
} from "@/lib/api/fleet-operations/api-private-hire";
import { getModels } from "@/lib/api/reference-data/api-models";
import { getSites } from "@/lib/api/reference-data/api-sites";
import { getSession } from "@/lib/auth/session";

const ROLE = "Private Hire Vehicles";
type Mode = "add" | "edit" | "delete" | "contractor-add" | "contractor-edit" | "contractor-delete";

function hasRole(roles: readonly string[]) {
  return roles.some((role) => role.localeCompare(ROLE, undefined, { sensitivity: "accent" }) === 0);
}

function modeValue(value: string): Mode {
  return [
    "add",
    "edit",
    "delete",
    "contractor-add",
    "contractor-edit",
    "contractor-delete",
  ].includes(value)
    ? (value as Mode)
    : "add";
}

function requestedPage(value: string) {
  const page = Number(value);
  return Number.isSafeInteger(page) && page > 0 ? page : 1;
}

async function PrivateHireMaintenancePageContent({
  searchParams,
  forcedMode,
  routePath = "/private-hire/maintenance",
}: Readonly<{
  searchParams?: Promise<Record<string, string | string[] | undefined>>;
  forcedMode?: Mode;
  routePath?: string;
}>) {
  await connection();
  const session = await getSession();
  if (session.status === "anonymous") redirect("/login");
  if (session.status === "expired" || session.status === "unavailable")
    return (
      <main className="page-shell vehicle-page-shell">
        <SessionRecovery returnPath={routePath} />
      </main>
    );
  if (!hasRole(session.roles))
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-status-card" role="alert">
          <p className="eyebrow">Access restricted</p>
          <h2>You do not have permission to maintain Private Hire Vehicles.</h2>
        </section>
      </main>
    );

  const query = searchParams ? await searchParams : {};
  const mode = forcedMode ?? modeValue(queryValue(query.mode));
  const search = queryValue(query.search).trim();
  const page = requestedPage(queryValue(query.page));
  const requestedVehicle = Number(queryValue(query.phvCode));
  const phvCode =
    Number.isInteger(requestedVehicle) && requestedVehicle > 0 ? requestedVehicle : null;
  const requestedContractor = Number(queryValue(query.contractorId));
  const contractorId =
    Number.isInteger(requestedContractor) && requestedContractor > 0 ? requestedContractor : null;

  try {
    if (mode.startsWith("contractor")) {
      const [contractorPage, selected] = await Promise.all([
        getPrivateHireContractorsPage({ page, pageSize: DEFAULT_PRIVATE_HIRE_PAGE_SIZE }),
        contractorId ? getPrivateHireContractor(contractorId) : Promise.resolve(null),
      ]);
      const isDelete = mode === "contractor-delete";
      const isEdit = mode === "contractor-edit";
      return (
        <main className="page-shell vehicle-page-shell">
          <section
            className="vehicle-card"
            aria-labelledby="private-hire-contractor-maintenance-title"
          >
            <header className="vehicle-page-header">
              <div>
                <p className="eyebrow">Private Hire maintenance</p>
                <h1 id="private-hire-contractor-maintenance-title">
                  {isDelete ? "Delete" : isEdit ? "Edit" : "Add"} Private Hire Contractor
                </h1>
                <p>
                  Contractor fields use the original legacy names where those columns are available.
                </p>
              </div>
              <Link className="button button-secondary" href="/private-hire/maintenance-menu">
                Menu
              </Link>
            </header>
            <PrivateHireNotice query={query} />
            {isDelete ? (
              <>
                <section
                  className="vehicle-status-maintenance-panel"
                  aria-labelledby="contractor-delete-title"
                >
                  <div className="vehicle-form-section-header">
                    <div>
                      <p className="eyebrow">Select a contractor</p>
                      <h2 id="contractor-delete-title">Contractors</h2>
                    </div>
                  </div>
                  <PrivateHireContractorTable
                    contractors={contractorPage.items}
                    selectPath={routePath}
                    mode="contractor-delete"
                  />
                  <PrivateHirePagination
                    path={routePath}
                    query={query}
                    page={contractorPage.page}
                    totalPages={contractorPage.totalPages}
                  />
                </section>
                {selected ? (
                  <form
                    className="vehicle-status-maintenance-panel"
                    action={deletePrivateHireContractorAction}
                  >
                    <input type="hidden" name="contractorId" value={selected.contractorId} />
                    <input
                      type="hidden"
                      name="returnPath"
                      value={`${routePath}?mode=contractor-delete`}
                    />
                    <div className="vehicle-form-section-header">
                      <div>
                        <p className="eyebrow">Selected contractor</p>
                        <h2>{selected.companyName}</h2>
                        <p>
                          This removes the contractor from active Private Hire maintenance. Modern
                          databases mark it deleted; legacy databases perform the legacy delete
                          operation.
                        </p>
                      </div>
                    </div>
                    <div className="button-row">
                      <button className="button button-danger" type="submit">
                        Delete contractor
                      </button>
                      <Link
                        className="button button-secondary"
                        href={`${routePath}?mode=contractor-delete`}
                      >
                        Cancel
                      </Link>
                    </div>
                  </form>
                ) : null}
              </>
            ) : (
              <>
                {isEdit ? (
                  <section
                    className="vehicle-status-maintenance-panel"
                    aria-labelledby="contractor-edit-title"
                  >
                    <div className="vehicle-form-section-header">
                      <div>
                        <p className="eyebrow">Select a contractor</p>
                        <h2 id="contractor-edit-title">Contractors</h2>
                      </div>
                    </div>
                    <PrivateHireContractorTable
                      contractors={contractorPage.items}
                      selectPath={routePath}
                      mode="contractor-edit"
                    />
                    <PrivateHirePagination
                      path={routePath}
                      query={query}
                      page={contractorPage.page}
                      totalPages={contractorPage.totalPages}
                    />
                  </section>
                ) : null}
                <PrivateHireContractorForm
                  contractor={selected}
                  action={savePrivateHireContractorAction}
                  returnPath={`${routePath}?mode=${mode}${contractorId ? `&contractorId=${contractorId}` : ""}`}
                />
              </>
            )}
          </section>
        </main>
      );
    }

    const [vehiclePage, selected] = await Promise.all([
      mode === "add"
        ? Promise.resolve(null)
        : getPrivateHirePage({
            page,
            pageSize: DEFAULT_PRIVATE_HIRE_PAGE_SIZE,
            searchTerm: search || undefined,
          }),
      phvCode ? getPrivateHireVehicle(phvCode) : Promise.resolve(null),
    ]);
    const shouldLoadVehicleLookups = mode === "add" || (mode === "edit" && selected !== null);
    const [models, sites, contractors] = shouldLoadVehicleLookups
      ? await Promise.all([getModels(), getSites(), getPrivateHireContractors()])
      : [[], [], []];
    const isDelete = mode === "delete";
    const isEdit = mode === "edit";
    return (
      <main className="page-shell vehicle-page-shell">
        <section className="vehicle-card" aria-labelledby="private-hire-vehicle-maintenance-title">
          <header className="vehicle-page-header">
            <div>
              <p className="eyebrow">Private Hire maintenance</p>
              <h1 id="private-hire-vehicle-maintenance-title">
                {isDelete ? "Delete" : isEdit ? "Edit" : "Add"} Private Hire Vehicle
              </h1>
              <p>
                Maintain all legacy Private_hire business fields without requiring modern audit
                columns.
              </p>
            </div>
            <Link className="button button-secondary" href="/private-hire/maintenance-menu">
              Menu
            </Link>
          </header>
          <PrivateHireNotice query={query} />
          {mode === "add" ? (
            <PrivateHireVehicleForm
              action={savePrivateHireVehicleAction}
              returnPath={`${routePath}?mode=add`}
              models={models}
              sites={sites}
              contractors={contractors}
            />
          ) : (
            <>
              <form className="vehicle-status-maintenance-panel" method="get" action={routePath}>
                <input type="hidden" name="mode" value={mode} />
                <div className="vehicle-form-section-header">
                  <div>
                    <p className="eyebrow">Vehicle lookup</p>
                    <h2>Find a vehicle by registration, engine, or chassis number</h2>
                  </div>
                </div>
                <div className="vehicle-search-row">
                  <label className="sr-only" htmlFor="private-hire-search">
                    Vehicle search
                  </label>
                  <input
                    className="vehicle-search"
                    id="private-hire-search"
                    name="search"
                    defaultValue={search}
                    placeholder="Enter registration, engine, or chassis"
                  />
                  <button className="button button-primary" type="submit">
                    Find vehicles
                  </button>
                </div>
              </form>
              <section
                className="vehicle-status-maintenance-panel"
                aria-labelledby="private-hire-vehicle-results-title"
              >
                <div className="vehicle-form-section-header">
                  <div>
                    <p className="eyebrow">Matching records</p>
                    <h2 id="private-hire-vehicle-results-title">Private Hire vehicles</h2>
                  </div>
                </div>
                <PrivateHireVehicleTable
                  vehicles={vehiclePage?.items ?? []}
                  selectPath={routePath}
                  mode={mode}
                />
                {vehiclePage ? (
                  <PrivateHirePagination
                    path={routePath}
                    query={query}
                    page={vehiclePage.page}
                    totalPages={vehiclePage.totalPages}
                  />
                ) : null}
              </section>
              {selected ? (
                isDelete ? (
                  <form
                    className="vehicle-status-maintenance-panel"
                    action={deletePrivateHireVehicleAction}
                  >
                    <input type="hidden" name="phvCode" value={selected.phvCode} />
                    <input type="hidden" name="returnPath" value={`${routePath}?mode=delete`} />
                    <div className="vehicle-form-section-header">
                      <div>
                        <p className="eyebrow">Selected vehicle</p>
                        <h2>{selected.registrationNumber}</h2>
                        <p>
                          This removes the vehicle from active Private Hire maintenance. Modern
                          databases mark it deleted; legacy databases perform the legacy delete
                          operation.
                        </p>
                      </div>
                    </div>
                    <div className="button-row">
                      <button className="button button-danger" type="submit">
                        Delete vehicle
                      </button>
                      <Link className="button button-secondary" href={`${routePath}?mode=delete`}>
                        Cancel
                      </Link>
                    </div>
                  </form>
                ) : isEdit ? (
                  <PrivateHireVehicleForm
                    vehicle={selected}
                    action={savePrivateHireVehicleAction}
                    returnPath={`${routePath}?mode=edit&phvCode=${selected.phvCode}`}
                    models={models}
                    sites={sites}
                    contractors={contractors}
                  />
                ) : null
              ) : null}
            </>
          )}
        </section>
      </main>
    );
  } catch (error) {
    if (error instanceof PrivateHireApiError && error.reason === "unauthorized")
      return (
        <main className="page-shell vehicle-page-shell">
          <SessionRecovery returnPath={routePath} />
        </main>
      );
    return (
      <main className="page-shell vehicle-page-shell">
        <ApiUnavailableForMaintenance path={routePath} />
      </main>
    );
  }
}

export function PrivateHireMaintenancePage(
  props: Readonly<{
    searchParams?: Promise<Record<string, string | string[] | undefined>>;
    forcedMode?: Mode;
    routePath?: string;
  }>,
) {
  return (
    <StreamedRoute>
      <PrivateHireMaintenancePageContent {...props} />
    </StreamedRoute>
  );
}

function ApiUnavailableForMaintenance({ path }: Readonly<{ path: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>Private Hire maintenance could not be loaded.</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <div className="button-row">
        <Link className="button button-primary" href={path}>
          Try again
        </Link>
        <Link className="button button-secondary" href="/login">
          Sign in
        </Link>
      </div>
    </section>
  );
}

export default function PrivateHireMaintenancePageRoute({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return <PrivateHireMaintenancePage searchParams={searchParams} />;
}
