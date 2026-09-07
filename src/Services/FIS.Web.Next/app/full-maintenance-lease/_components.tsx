import Link from "next/link";
import type { ReactNode } from "react";

import type { LeaseTermRecord } from "@/lib/api-fml";

export function hasFmlPermission(accessLevel: string | undefined) {
  if (!accessLevel) return false;
  try {
    return (BigInt(accessLevel) & BigInt(2)) === BigInt(2);
  } catch {
    return false;
  }
}

export function hasFinancialPermission(accessLevel: string | undefined) {
  if (!accessLevel) return false;
  try {
    return (BigInt(accessLevel) & BigInt(16)) === BigInt(16);
  } catch {
    return false;
  }
}

export function getStatusLabel(status: number | null) {
  return status === 1 ? "Pending" : status === 2 ? "Approved" : status === 4 ? "Rejected" : "Unknown";
}

export function getStatusClass(status: number | null) {
  return status === 2 ? "badge badge-success" : status === 4 ? "badge badge-error" : "badge badge-warning";
}

export function formatDate(value: string | null) {
  return value ? value.slice(0, 10) : "-";
}

export function formatCurrency(value: number | null) {
  return value === null ? "-" : `R ${value.toLocaleString("en-ZA", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

export function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

export function vehicleLabel(vehicle: { fleetNumber: string | null; registrationNumber: string | null; vmfCode: number }) {
  return `${valueOrDash(vehicle.fleetNumber)} / ${valueOrDash(vehicle.registrationNumber)} (${vehicle.vmfCode})`;
}

export function FmlFrame({ title, description, children, backHref = "/full-maintenance-lease" }: Readonly<{ title: string; description: string; children: ReactNode; backHref?: string }>) {
  return (
    <main className="page-shell vehicle-page-shell">
      <section className="vehicle-card" aria-labelledby="fml-page-title">
        <header className="vehicle-page-header">
          <div>
            <p className="eyebrow">Full Maintenance Lease</p>
            <h1 id="fml-page-title">{title}</h1>
            <p>{description}</p>
          </div>
          <Link className="button button-secondary" href={backHref}>Back</Link>
        </header>
        {children}
      </section>
    </main>
  );
}

export function AccessRestricted({ message = "You do not have permission to access Full Maintenance Lease." }: Readonly<{ message?: string }>) {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">Access restricted</p><h2>{message}</h2><p className="muted-copy">Your account needs contract-management access for this workflow.</p></section>;
}

export function ApiUnavailable({ message = "FML data could not be loaded." }: Readonly<{ message?: string }>) {
  return <section className="vehicle-status-card" role="alert"><p className="eyebrow">API unavailable</p><h2>{message}</h2><p className="muted-copy">The application is still running. Retry when the FIS API is available.</p><Link className="button button-primary" href="/full-maintenance-lease">Try again</Link></section>;
}

export function ActionNotice({ result, message }: Readonly<{ result?: string; message?: string }>) {
  if (result === "error") return <div className="notice notice-error" role="alert">{message ?? "The request could not be completed."}</div>;
  if (result === "created") return <div className="notice notice-success" role="status">Lease record created successfully.</div>;
  if (result === "updated") return <div className="notice notice-success" role="status">Lease record updated successfully.</div>;
  if (result === "approved") return <div className="notice notice-success" role="status">Lease tariff approved successfully.</div>;
  if (result === "rejected") return <div className="notice notice-success" role="status">Lease tariff rejected for correction.</div>;
  return null;
}

export function termNotes(term: LeaseTermRecord) {
  return term.authorityComment ?? term.rejectionReason ?? term.comments;
}
