import type { VehicleDocumentRecord } from "@/lib/api/vehicles/api-vehicle-documents";

const VEHICLE_DATE_FORMATTER = new Intl.DateTimeFormat("en-ZA", {
  day: "2-digit",
  month: "short",
  year: "numeric",
  timeZone: "UTC",
});
const VEHICLE_AMOUNT_FORMATTER = new Intl.NumberFormat("en-ZA", {
  style: "currency",
  currency: "ZAR",
});

export function valueOrDash(value: string | number | null | undefined) {
  if (value === null || value === undefined || value === "") return "-";
  return String(value);
}

export function formatDate(value: string | null) {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value.slice(0, 10);
  return VEHICLE_DATE_FORMATTER.format(date);
}

export function formatAmount(value: number | null) {
  return value === null ? "-" : VEHICLE_AMOUNT_FORMATTER.format(value);
}

export function formatFileSize(value: number | null) {
  if (!value || value <= 0) return "-";
  if (value < 1024 * 1024) return `${Math.round(value / 1024)} KB`;
  return `${(value / (1024 * 1024)).toFixed(2)} MB`;
}

export function formatReference(document: VehicleDocumentRecord) {
  return document.referenceType && document.referenceId
    ? `${document.referenceType} #${document.referenceId}`
    : "-";
}
