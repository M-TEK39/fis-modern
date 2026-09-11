import type { VehicleStatusVehicle } from "@/lib/api/vehicles/api-vehicle-status";

export function valueOrDash(value: string | number | null | undefined) {
  return value === null || value === undefined || String(value).trim() === "" ? "-" : String(value);
}

export function formatDate(value: string | null | undefined) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value.slice(0, 10);
  }

  return `${date.getUTCFullYear()}/${String(date.getUTCMonth() + 1).padStart(2, "0")}/${String(date.getUTCDate()).padStart(2, "0")}`;
}

export function formatDateTime(value: string | null | undefined) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value.slice(0, 16).replace("T", " ");
  }

  return `${formatDate(value)} ${String(date.getUTCHours()).padStart(2, "0")}:${String(date.getUTCMinutes()).padStart(2, "0")}`;
}

export function todayInputValue() {
  return new Date().toISOString().slice(0, 10);
}

export function vehicleStatusLink(vmfCode: number, returnUrl: string) {
  const query = new URLSearchParams({ vmfCode: String(vmfCode) });
  if (returnUrl) {
    query.set("returnUrl", returnUrl);
  }

  return `/vehicles/status-maintenance?${query.toString()}`;
}

export function vehicleInformationRows(vehicle: VehicleStatusVehicle) {
  return [
    ["GG Number", vehicle.fleetNumber],
    ["Registration Number", vehicle.registrationNumber],
    ["Make & Model", vehicle.modelName],
    ["Year Manufactured", vehicle.yearManufactured],
    ["Colour", vehicle.colour],
    ["VIN/Chassis Number", vehicle.chassisNumber],
    ["Engine Number", vehicle.engineNumber],
    ["Hired From", vehicle.hiredFrom],
    ["Hire Type", vehicle.typeName],
    ["Location", vehicle.locationDescription || vehicle.locationCode],
    ["Current Status", vehicle.statusDescription || vehicle.statusCode],
  ] as const;
}
