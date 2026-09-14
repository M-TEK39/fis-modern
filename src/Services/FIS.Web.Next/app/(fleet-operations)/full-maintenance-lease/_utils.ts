import type { LeaseTermRecord } from "@/lib/api/finance/api-fml";

export function hasLeaseTariffMaintenanceRole(roles: readonly string[]) {
  return roles.some((role) => role.trim().toLowerCase() === "vehicle master");
}

export function hasLeaseVehiclePendingRole(roles: readonly string[]) {
  return roles.some((role) => role.trim().toLowerCase() === "lease vehicle pending");
}

export function hasLeaseVehicleCapturerRole(roles: readonly string[]) {
  return roles.some((role) => role.trim().toLowerCase() === "lease vehicle capturer");
}

export function hasLeaseVehicleAuthorizerRole(roles: readonly string[]) {
  return roles.some((role) => role.trim().toLowerCase() === "lease vehicle authorizer");
}

export function getStatusLabel(status: number | null) {
  return status === 1
    ? "Pending"
    : status === 2
      ? "Approved"
      : status === 0 || status === 4
        ? "Rejected"
        : "Unknown";
}

export function getStatusClass(status: number | null) {
  return status === 2
    ? "badge badge-success"
    : status === 0 || status === 4
      ? "badge badge-error"
      : "badge badge-warning";
}

export function formatDate(value: string | null) {
  return value ? value.slice(0, 10) : "-";
}

export function formatCurrency(value: number | null) {
  return value === null
    ? "-"
    : `R ${value.toLocaleString("en-ZA", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

export function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

export function vehicleLabel(vehicle: {
  fleetNumber: string | null;
  registrationNumber: string | null;
  vmfCode: number;
}) {
  return `${valueOrDash(vehicle.fleetNumber)} / ${valueOrDash(vehicle.registrationNumber)} (${vehicle.vmfCode})`;
}

export function termNotes(term: LeaseTermRecord) {
  return term.authorityComment ?? term.rejectionReason ?? term.comments;
}
