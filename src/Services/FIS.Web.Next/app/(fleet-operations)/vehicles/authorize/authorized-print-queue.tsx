"use client";

import Link from "next/link";

import AuthorizedPrintButton from "@/app/(fleet-operations)/vehicles/authorize/authorized-print-button";
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationNext,
  PaginationPrevious,
} from "@/components/ui/pagination";
import type { VehicleAuthorizationQueuePage } from "@/lib/api/vehicles/api-vehicle-authorization";

function valueOrDash(value: string | null | undefined) {
  return value?.trim() || "-";
}

function getYear(value: string | number | null) {
  if (!value) {
    return "-";
  }

  if (typeof value === "number") {
    return String(value);
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "-" : String(date.getUTCFullYear());
}

function pageHref(page: number) {
  return page <= 1 ? "/vehicles/create" : `/vehicles/create?authorizedPage=${page}`;
}

export default function AuthorizedPrintQueue({
  queue,
}: Readonly<{ queue: VehicleAuthorizationQueuePage }>) {
  const previousDisabled = queue.page <= 1;
  const nextDisabled = queue.page >= queue.totalPages;

  return (
    <section
      className="vehicle-authorization-section"
      aria-labelledby="authorized-print-queue-title"
    >
      <div className="vehicle-form-section-header">
        <div>
          <p className="eyebrow">Vehicle inception</p>
          <h2 id="authorized-print-queue-title">
            Authorized Vehicles Ready for Printing and Inception into the Database
          </h2>
        </div>
        <span className="vehicle-queue-count">{queue.totalRecords} record(s)</span>
      </div>

      {queue.data.length === 0 ? (
        <div className="vehicle-empty-state">
          <p>No Authorized Vehicles</p>
        </div>
      ) : (
        <>
          <div className="vehicle-table-wrapper">
            <table className="vehicle-table vehicle-authorization-table">
              <thead>
                <tr>
                  <th>Chassis NO</th>
                  <th>GG Number</th>
                  <th>Model</th>
                  <th>Year Model</th>
                  <th>Engine NO</th>
                  <th>Notes</th>
                  <th>Authority Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {queue.data.map((vehicle) => (
                  <tr key={vehicle.tempVmfCode}>
                    <td>{valueOrDash(vehicle.chassisNumber)}</td>
                    <td>{valueOrDash(vehicle.fleetNumber)}</td>
                    <td>{valueOrDash(vehicle.modelDescription)}</td>
                    <td>{getYear(vehicle.yearManufactured ?? vehicle.purchaseDate)}</td>
                    <td>{valueOrDash(vehicle.engineNumber)}</td>
                    <td>{valueOrDash(vehicle.fleetNotes)}</td>
                    <td>
                      <span className="vehicle-badge badge-success">
                        {valueOrDash(vehicle.authorityStatus)}
                      </span>
                    </td>
                    <td>
                      <AuthorizedPrintButton id={vehicle.tempVmfCode} returnPath="/vehicles/create" />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {queue.totalPages > 1 ? (
            <Pagination>
              <PaginationContent>
                <PaginationItem>
                  <PaginationPrevious
                    href={pageHref(previousDisabled ? queue.page : queue.page - 1)}
                    aria-disabled={previousDisabled}
                    className={previousDisabled ? "pointer-events-none opacity-50" : undefined}
                    tabIndex={previousDisabled ? -1 : undefined}
                  />
                </PaginationItem>
                <PaginationItem>
                  <Link className="button button-secondary button-small" href={pageHref(queue.page)}>
                    Page {queue.page} of {queue.totalPages}
                  </Link>
                </PaginationItem>
                <PaginationItem>
                  <PaginationNext
                    href={pageHref(nextDisabled ? queue.page : queue.page + 1)}
                    aria-disabled={nextDisabled}
                    className={nextDisabled ? "pointer-events-none opacity-50" : undefined}
                    tabIndex={nextDisabled ? -1 : undefined}
                  />
                </PaginationItem>
              </PaginationContent>
            </Pagination>
          ) : null}
        </>
      )}
    </section>
  );
}
