import {
  VEHICLE_STATUS_OPTIONS,
  type VehicleStatusReportRow,
} from "@/app/(fleet-operations)/vehicles/status/status-types";

export const EXPORT_FIELDS = [
  ["fleetNumber", "GG Number"],
  ["registrationNumber", "Registration Number"],
  ["invoiceNumber", "Invoice Number"],
  ["makeModel", "Make & Model"],
  ["status", "Status"],
  ["site", "Site"],
  ["remarkText", "Active Remark"],
  ["remarkCategory", "Remark Category"],
] as const;

export type ExportField = (typeof EXPORT_FIELDS)[number][0];

export function valueOrDash(value: string | null) {
  return value || "-";
}

export function getStatusLabel(row: VehicleStatusReportRow) {
  return (
    row.statusText ||
    VEHICLE_STATUS_OPTIONS.find((option) => option.code === row.statusCode)?.description ||
    "-"
  );
}

export function getMakeModel(row: VehicleStatusReportRow) {
  return `${row.makeDescription || "-"} / ${row.modelDescription || "-"}`;
}

export function getSiteLabel(row: VehicleStatusReportRow) {
  return row.siteName || "-";
}

export function getVehicleLabel(row: VehicleStatusReportRow) {
  return `${valueOrDash(row.fleetNumber)} / ${valueOrDash(row.registrationNumber)} (${row.vmfCode})`;
}

function csvCell(value: string | null) {
  return `"${(value || "").replaceAll('"', '""')}"`;
}

export function downloadCsv(rows: VehicleStatusReportRow[], selectedFields: Set<ExportField>) {
  const fields = EXPORT_FIELDS.filter(([key]) => selectedFields.has(key));
  if (fields.length === 0) return false;

  const lines = [fields.map(([, label]) => csvCell(label)).join(",")];
  for (const row of rows) {
    const values = fields.map(([key]) => {
      switch (key) {
        case "fleetNumber":
          return valueOrDash(row.fleetNumber);
        case "registrationNumber":
          return valueOrDash(row.registrationNumber);
        case "invoiceNumber":
          return valueOrDash(row.invoiceNumber);
        case "makeModel":
          return getMakeModel(row);
        case "status":
          return getStatusLabel(row);
        case "site":
          return getSiteLabel(row);
        case "remarkText":
          return valueOrDash(row.remark?.text ?? null);
        case "remarkCategory":
          return valueOrDash(row.remark?.category ?? null);
      }
    });
    lines.push(values.map(csvCell).join(","));
  }

  const blob = new Blob([`${lines.join("\r\n")}\r\n`], { type: "text/csv;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = `vehicles_status_report_${new Date().toISOString().slice(0, 16).replaceAll(/[-:T]/g, "")}.csv`;
  anchor.click();
  URL.revokeObjectURL(url);
  return true;
}

export type FilterValues = {
  search: string;
  locationCode: string;
  typeCode: string;
  makeCode: string;
  vehicleStatusCode: string;
};

export const EMPTY_FILTERS: FilterValues = {
  search: "",
  locationCode: "",
  typeCode: "",
  makeCode: "",
  vehicleStatusCode: "",
};

export function appendFilters(formData: FormData, filters: FilterValues) {
  formData.set("search", filters.search);
  formData.set("locationCode", filters.locationCode);
  formData.set("typeCode", filters.typeCode);
  formData.set("makeCode", filters.makeCode);
  formData.set("vehicleStatusCode", filters.vehicleStatusCode);
}
