import DataTableHeader from "@/components/ui/data-table-header";

import Link from "next/link";

import type { VehicleStatusVehicle } from "@/lib/api/vehicles/api-vehicle-status";

import { formatDate, valueOrDash, vehicleStatusLink } from "./status-maintenance-utils";

export default function StatusMaintenanceSearchResults({
  results,
  returnUrl,
  page,
  total,
  totalPages,
  searchMode,
  searchTerm,
  searchAction,
}: Readonly<{
  results: VehicleStatusVehicle[];
  returnUrl: string;
  page: number;
  total: number;
  totalPages: number;
  searchMode: "GG" | "GP";
  searchTerm: string;
  searchAction: (payload: FormData) => void;
}>) {
  if (results.length === 0) {
    return null;
  }

  return (
    <section className="status-maintenance-panel" aria-labelledby="status-search-results-title">
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Vehicle search</p>
          <h2 id="status-search-results-title">Select Vehicle ({total})</h2>
        </div>
      </div>
      <div className="vehicle-table-wrapper">
        <table className="vehicle-table status-maintenance-table">
          <DataTableHeader
            columns={[
              { key: "column-1", label: <>GG Number</> },
              { key: "column-2", label: <>Registration</> },
              { key: "column-3", label: <>Current Status</> },
              { key: "column-4", label: <>Status Date</> },
              { key: "column-5", label: <>Action</> },
            ]}
          />
          <tbody>
            {results.map((vehicle) => (
              <tr key={vehicle.vmfCode}>
                <td>{valueOrDash(vehicle.fleetNumber)}</td>
                <td>{valueOrDash(vehicle.registrationNumber)}</td>
                <td>{valueOrDash(vehicle.statusDescription || vehicle.statusCode)}</td>
                <td>{formatDate(vehicle.statusDate)}</td>
                <td>
                  <Link
                    className="button button-secondary button-small"
                    href={vehicleStatusLink(vehicle.vmfCode, returnUrl)}
                  >
                    Manage status
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <form
        action={searchAction}
        className="vehicle-pagination"
        aria-label="Vehicle status search results"
      >
        <input name="searchMode" type="hidden" value={searchMode} />
        <input name="searchTerm" type="hidden" value={searchTerm} />
        <button
          className="vehicle-pagination-button"
          name="page"
          type="submit"
          value={Math.max(1, page - 1)}
          disabled={page <= 1}
        >
          Previous
        </button>
        <span className="vehicle-pagination-meta" aria-live="polite">
          Page {page} of {totalPages}
        </span>
        <button
          className="vehicle-pagination-button"
          name="page"
          type="submit"
          value={Math.min(totalPages, page + 1)}
          disabled={page >= totalPages}
        >
          Next
        </button>
      </form>
    </section>
  );
}
