import Link from "next/link";

import {
  PrivateHireContractorForm,
  PrivateHireContractorTable,
  PrivateHireNotice,
  PrivateHirePagination,
  PrivateHireVehicleForm,
  PrivateHireVehicleTable,
} from "@/app/(fleet-operations)/private-hire/_components";
import {
  deletePrivateHireContractorAction,
  deletePrivateHireVehicleAction,
  savePrivateHireContractorAction,
  savePrivateHireVehicleAction,
} from "@/app/(fleet-operations)/private-hire/actions";
import type { ModelRecord } from "@/lib/api/reference-data/api-models";
import type { SiteRecord } from "@/lib/api/reference-data/api-sites";
import type {
  PrivateHireContractorPage,
  PrivateHireContractorRecord,
  PrivateHireVehiclePage,
  PrivateHireVehicleRecord,
} from "@/lib/api/fleet-operations/api-private-hire";

type Query = Record<string, string | string[] | undefined>;

export function PrivateHireContractorMaintenanceView({
  query,
  routePath,
  mode,
  contractorPage,
  selected,
  contractorId,
}: Readonly<{
  query: Query;
  routePath: string;
  mode: string;
  contractorPage: PrivateHireContractorPage;
  selected: PrivateHireContractorRecord | null;
  contractorId: number | null;
}>) {
  const isDelete = mode === "contractor-delete";
  const isEdit = mode === "contractor-edit";
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="private-hire-contractor-maintenance-title">
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

type PrivateHireVehicleMaintenanceViewProps = Readonly<{
  query: Query;
  routePath: string;
  mode: string;
  search: string;
  vehiclePage: PrivateHireVehiclePage | null;
  selected: PrivateHireVehicleRecord | null;
  models: readonly ModelRecord[];
  sites: readonly SiteRecord[];
  contractors: readonly PrivateHireContractorRecord[];
}>;

function renderPrivateHireVehicleMaintenanceView({
  query,
  routePath,
  mode,
  search,
  vehiclePage,
  selected,
  models,
  sites,
  contractors,
}: PrivateHireVehicleMaintenanceViewProps) {
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
}

export function PrivateHireVehicleMaintenanceView(props: PrivateHireVehicleMaintenanceViewProps) {
  return <>{renderPrivateHireVehicleMaintenanceView(props)}</>;
}
