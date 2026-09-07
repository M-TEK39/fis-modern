"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useMemo, useState, useTransition } from "react";

import type { VehicleSnapshotPage } from "@/lib/api-vehicles";

type VehicleMasterClientProps = {
  pageData: VehicleSnapshotPage;
  routePath: "/vehicles" | "/vehicle-orders" | "/Master-File/Vehicle_Master.aspx";
  menu: {
    canCaptureInception: boolean;
    canAuthorizeInception: boolean;
    canMaintainVehicleMaster: boolean;
    canViewDemoVehicles: boolean;
  };
};

type OverviewFilter = "" | "inService" | "new" | "recovered" | "renumbered" | "withContract";

function valueOrDash(value: string | null) {
  return value || "-";
}

function contains(value: string | null, search: string) {
  return value?.toLocaleLowerCase().includes(search) === true;
}

function MenuTile({
  title,
  children,
  defaultOpen = true,
}: Readonly<{ title: string; children: React.ReactNode; defaultOpen?: boolean }>) {
  const [open, setOpen] = useState(defaultOpen);

  return (
    <section className="vehicle-menu-tile">
      <button
        className="vehicle-menu-header"
        type="button"
        aria-expanded={open}
        onClick={() => setOpen((current) => !current)}
      >
        <span>{title}</span>
        <span aria-hidden="true">{open ? "⌃" : "⌄"}</span>
      </button>
      {open ? <div className="vehicle-menu-body">{children}</div> : null}
    </section>
  );
}

function MenuItem({ href, children }: Readonly<{ href: string; children: React.ReactNode }>) {
  return (
    <div className="vehicle-menu-item">
      <Link className="vehicle-menu-link" href={href}>
        {children}
      </Link>
    </div>
  );
}

export default function VehicleMasterClient({ pageData, routePath, menu }: VehicleMasterClientProps) {
  const router = useRouter();
  const [searchTerm, setSearchTerm] = useState("");
  const [statusFilter, setStatusFilter] = useState<OverviewFilter>("");
  const [isRefreshing, startRefresh] = useTransition();

  const normalizedSearch = searchTerm.trim().toLocaleLowerCase();
  const filteredRows = useMemo(
    () =>
      pageData.rows.filter((vehicle) => {
        const matchesSearch =
          normalizedSearch.length === 0 ||
          contains(vehicle.fleetNumber, normalizedSearch) ||
          contains(vehicle.registrationNumber, normalizedSearch) ||
          contains(vehicle.invoiceNumber, normalizedSearch) ||
          contains(vehicle.modelName, normalizedSearch) ||
          contains(vehicle.statusDescription, normalizedSearch);

        if (!matchesSearch) {
          return false;
        }

        switch (statusFilter) {
          case "inService":
            return contains(vehicle.statusDescription, "service");
          case "new":
            return contains(vehicle.statusDescription, "new");
          case "recovered":
            return Boolean(vehicle.recoveredGgNumber);
          case "renumbered":
            return Boolean(vehicle.renumberedTo);
          case "withContract":
            return pageData.contractsByVmf[String(vehicle.vmfCode)]?.label !== "No Contract";
          default:
            return true;
        }
      }),
    [normalizedSearch, pageData.contractsByVmf, pageData.rows, statusFilter],
  );

  const pageHref = (page: number) => (page === 1 ? routePath : `${routePath}?page=${page}`);

  return (
    <>
      <div className="vehicle-menu-tiles">
        <MenuTile title="Vehicle Master Information / Help">
          <MenuItem href="/vehicles/help">Vehicle Master Information / Help</MenuItem>
        </MenuTile>

        <MenuTile title="Vehicle Master Maintenance">
          {menu.canCaptureInception ? <MenuItem href="/vehicles/create">1) Add New Vehicle</MenuItem> : null}
          {menu.canAuthorizeInception ? (
            <MenuItem href="/vehicles/authorize">1) Authorize Captured Vehicle Information</MenuItem>
          ) : null}
          {menu.canMaintainVehicleMaster ? (
            <>
              <MenuItem href="/vehicles/edit">2) Edit A Vehicle</MenuItem>
              <MenuItem href="/vehicles/status-maintenance">3) Vehicle Status Maintenance</MenuItem>
              <MenuItem href="/vehicles/barcode">4) Add/Edit Barcode</MenuItem>
              <MenuItem href="/vehicles/gg-block-numbers">5) Add GG Block Number(s)</MenuItem>
            </>
          ) : null}
        </MenuTile>

        {menu.canMaintainVehicleMaster ? (
          <MenuTile title="Vehicle Source Maintenance">
            <MenuItem href="/vehicles/source-maintenance">1) Vehicle Source Maintenance</MenuItem>
          </MenuTile>
        ) : null}

        {menu.canViewDemoVehicles ? (
          <>
            <MenuTile title="Stolen & Recovered Vehicles">
              <MenuItem href="/vehicles/recovered">1) Update Recovered GG</MenuItem>
              <MenuItem href="/vehicles/renumbered-report">2) Report All Renumbered vehicles</MenuItem>
            </MenuTile>
            <MenuTile title="Demo Vehicles">
              <MenuItem href="/vehicles/demo/add">1) Add a Demo Vehicle</MenuItem>
              <MenuItem href="/vehicles/demo/edit">2) Edit a Demo Vehicle</MenuItem>
              <MenuItem href="/vehicles/demo/delete">3) Delete a Demo Vehicle</MenuItem>
              <MenuItem href="/vehicles/demo/report">4) Report All Demo Vehicle</MenuItem>
            </MenuTile>
          </>
        ) : null}
      </div>

      <section className="vehicle-overview" aria-labelledby="vehicle-overview-title">
        <div className="vehicle-overview-header">
          <h2 id="vehicle-overview-title">All Vehicles Snapshot</h2>
          <div className="vehicle-overview-controls">
            <label className="sr-only" htmlFor="vehicle-status-filter">
              Filter vehicles
            </label>
            <select
              id="vehicle-status-filter"
              className="vehicle-select"
              value={statusFilter}
              onChange={(event) => setStatusFilter(event.target.value as OverviewFilter)}
            >
              <option value="">All vehicles</option>
              <option value="inService">In-service vehicles</option>
              <option value="new">New vehicles</option>
              <option value="recovered">Recovered vehicles</option>
              <option value="renumbered">Renumbered vehicles</option>
              <option value="withContract">Vehicles with contracts</option>
            </select>
            <button
              className="button button-secondary"
              type="button"
              disabled={isRefreshing}
              onClick={() => startRefresh(() => router.refresh())}
            >
              {isRefreshing ? "Refreshing..." : "Refresh"}
            </button>
          </div>
        </div>

        <div className="vehicle-search-row">
          <label className="sr-only" htmlFor="vehicle-search">
            Search vehicles
          </label>
          <input
            id="vehicle-search"
            className="vehicle-search"
            type="search"
            value={searchTerm}
            placeholder="Search GG number, registration, invoice, model, status..."
            onChange={(event) => setSearchTerm(event.target.value)}
          />
          {searchTerm ? (
            <button className="vehicle-search-clear" type="button" onClick={() => setSearchTerm("")} aria-label="Clear search">
              ×
            </button>
          ) : null}
        </div>

        {filteredRows.length === 0 ? (
          <div className="vehicle-empty-state">
            <p className="eyebrow">No vehicles found</p>
            <p>No vehicles match the current filter selection.</p>
          </div>
        ) : (
          <>
            <div className="vehicle-table-wrapper">
              <table className="vehicle-table">
                <thead>
                  <tr>
                    <th scope="col">GG Number</th>
                    <th scope="col">Registration</th>
                    <th scope="col">Invoice Number</th>
                    <th scope="col">Model</th>
                    <th scope="col">Status</th>
                    <th scope="col">Contract State</th>
                    <th scope="col">Target Return</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredRows.map((vehicle) => {
                    const contract = pageData.contractsByVmf[String(vehicle.vmfCode)] ?? {
                      label: "No Contract",
                      badgeClass: "badge" as const,
                      targetReturnDate: null,
                    };

                    return (
                      <tr key={vehicle.vmfCode}>
                        <td>{valueOrDash(vehicle.fleetNumber)}</td>
                        <td>{valueOrDash(vehicle.registrationNumber)}</td>
                        <td>{valueOrDash(vehicle.invoiceNumber)}</td>
                        <td>{valueOrDash(vehicle.modelName)}</td>
                        <td>{valueOrDash(vehicle.statusDescription)}</td>
                        <td>
                          <span className={`vehicle-badge ${contract.badgeClass}`}>{contract.label}</span>
                        </td>
                        <td>{contract.targetReturnDate || "-"}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            <nav className="vehicle-pagination" aria-label="Vehicle snapshot pagination">
              {pageData.page <= 1 ? (
                <span className="vehicle-pagination-button vehicle-pagination-disabled" aria-disabled="true">
                  Previous
                </span>
              ) : (
                <Link className="vehicle-pagination-button" href={pageHref(pageData.page - 1)}>
                  Previous
                </Link>
              )}
              <span aria-live="polite">
                Page {pageData.page} of {pageData.totalPages}
              </span>
              {pageData.page >= pageData.totalPages ? (
                <span className="vehicle-pagination-button vehicle-pagination-disabled" aria-disabled="true">
                  Next
                </span>
              ) : (
                <Link className="vehicle-pagination-button" href={pageHref(pageData.page + 1)}>
                  Next
                </Link>
              )}
            </nav>
            <p className="vehicle-pagination-meta">
              Total records: {filteredRows.length} on this page | Page size: {pageData.pageSize}
            </p>
          </>
        )}
      </section>
    </>
  );
}
