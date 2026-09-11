import type { LeaseTermRecord } from "@/lib/api/finance/api-fml";

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
  return status === 1
    ? "Pending"
    : status === 2
      ? "Approved"
      : status === 4
        ? "Rejected"
        : "Unknown";
}

export function getStatusClass(status: number | null) {
  return status === 2
    ? "badge badge-success"
    : status === 4
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
