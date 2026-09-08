export type VehicleStatusOption = {
  code: number;
  description: string;
};

export type VehicleStatusSite = {
  code: number;
  description: string;
};

export type VehicleStatusType = {
  code: number;
  description: string;
};

export type VehicleStatusReportFilters = {
  search?: string;
  locationCode?: number;
  typeCode?: number;
  makeCode?: number;
  vehicleStatusCode?: number;
};

export type VehicleStatusReportRemark = {
  remarkId: number;
  category: string | null;
  text: string | null;
};

export type VehicleStatusReportRow = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  invoiceNumber: string | null;
  makeDescription: string | null;
  modelDescription: string | null;
  statusCode: number;
  statusText: string | null;
  typeCode: number | null;
  typeDescription: string | null;
  locationCode: number | null;
  siteName: string | null;
  remark: VehicleStatusReportRemark | null;
};

export type VehicleStatusReport = {
  totalCount: number;
  remarksAvailable: boolean;
  assumptionNote: string | null;
  sites: VehicleStatusSite[];
  types: VehicleStatusType[];
  makes: VehicleStatusOption[];
  rows: VehicleStatusReportRow[];
};

export const VEHICLE_STATUS_OPTIONS: readonly VehicleStatusOption[] = [
  { code: 1, description: "In Service" },
  { code: 2, description: "Withdrawn" },
  { code: 3, description: "Board of Survey" },
  { code: 4, description: "Stolen" },
  { code: 5, description: "Sold" },
  { code: 6, description: "Transferred" },
  { code: 7, description: "Subsidized" },
  { code: 8, description: "From Focus" },
  { code: 9, description: "Privatised" },
  { code: 10, description: "Recovered" },
  { code: 11, description: "Missing" },
  { code: 12, description: "Destroyed" },
];
