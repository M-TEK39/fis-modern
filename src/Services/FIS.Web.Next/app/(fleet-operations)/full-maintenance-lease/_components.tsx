import Link from "next/link";
import type { ReactNode } from "react";

import DataTableHeader from "@/components/ui/data-table-header";
import type { VehicleOption } from "@/lib/api/vehicles/api-vehicles";

export function FmlFrame({
  title,
  description,
  children,
  backHref = "/full-maintenance-lease",
}: Readonly<{ title: string; description: string; children: ReactNode; backHref?: string }>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="fml-page-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Full Maintenance Lease</p>
            <h1 id="fml-page-title">{title}</h1>
            <p>{description}</p>
          </div>
          <Link className="button button-secondary" href={backHref}>
            Back
          </Link>
        </header>
        {children}
      </section>
    </main>
  );
}

export function AccessRestricted({
  message = "You do not have permission to access Full Maintenance Lease.",
}: Readonly<{ message?: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">Access restricted</p>
      <h2>{message}</h2>
      <p className="muted-copy">Your account needs contract-management access for this workflow.</p>
    </section>
  );
}

export function ApiUnavailable({
  message = "FML data could not be loaded.",
}: Readonly<{ message?: string }>) {
  return (
    <section className="vehicle-status-card" role="alert">
      <p className="eyebrow">API unavailable</p>
      <h2>{message}</h2>
      <p className="muted-copy">
        The application is still running. Retry when the FIS API is available.
      </p>
      <Link className="button button-primary" href="/full-maintenance-lease">
        Try again
      </Link>
    </section>
  );
}

export function ActionNotice({ result, message }: Readonly<{ result?: string; message?: string }>) {
  if (result === "error")
    return (
      <div className="notice notice-error" role="alert">
        {message ?? "The request could not be completed."}
      </div>
    );
  if (result === "created")
    return (
      <div className="notice notice-success" role="status">
        Lease record created successfully.
      </div>
    );
  if (result === "updated")
    return (
      <div className="notice notice-success" role="status">
        Lease record updated successfully.
      </div>
    );
  if (result === "approved")
    return (
      <div className="notice notice-success" role="status">
        Lease tariff approved successfully.
      </div>
    );
  if (result === "rejected")
    return (
      <div className="notice notice-success" role="status">
        Lease tariff rejected for correction.
      </div>
    );
  return null;
}

function vehicleLabel(vehicle: VehicleOption) {
  return `${vehicle.fleetNumber ?? "-"} / ${vehicle.registrationNumber ?? "-"} (${vehicle.vmfCode})`;
}

export function FmlVehicleSearchResults({
  vehicles,
  search,
  mode,
  routePath,
}: Readonly<{
  vehicles: readonly VehicleOption[];
  search: string;
  mode: string;
  routePath: string;
}>) {
  return (
    <div className="vehicle-table-wrapper">
      <table className="vehicle-table">
        <caption className="sr-only">Vehicle search results</caption>
        <DataTableHeader
          columns={[
            { key: "vehicle", label: "Vehicle" },
            { key: "select", label: "Select" },
          ]}
        />
        <tbody>
          {vehicles.map((vehicle) => (
            <tr key={vehicle.vmfCode}>
              <td>{vehicleLabel(vehicle)}</td>
              <td>
                <Link
                  className="button button-secondary"
                  href={`${routePath}?search=${encodeURIComponent(search)}&mode=${encodeURIComponent(mode)}&vmfCode=${vehicle.vmfCode}`}
                >
                  Select
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
