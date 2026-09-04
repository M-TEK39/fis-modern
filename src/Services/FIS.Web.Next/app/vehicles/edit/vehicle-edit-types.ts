export type VehicleEditSearchResult = {
  vmfCode: number;
  fleetNumber: string | null;
  registrationNumber: string | null;
  chassisNumber: string | null;
  engineNumber: string | null;
  invoiceNumber: string | null;
};

export type VehicleEditModelOption = {
  code: number;
  name: string;
  typeCode: number | null;
};

export type VehicleEditOption = {
  code: number;
  label: string;
};

export type VehicleEditReferenceData = {
  models: VehicleEditModelOption[];
  types: VehicleEditOption[];
  statuses: VehicleEditOption[];
  locations: VehicleEditOption[];
};

export type VehicleEditFormData = {
  vmfCode: number;
  fleetNumber: string;
  registrationNumber: string;
  modelCode: number | null;
  typeCode: number | null;
  vehicleStatusCode: number | null;
  locationCode: number | null;
  colour: string;
  chassisNumber: string;
  engineNumber: string;
  takeOnOdo: number | null;
  currentOdo: number | null;
  tare: number | null;
  gvm: number | null;
  yearManufactured: number | null;
  ifmsVehicleRegisterNumber: string;
  natisModelNumber: string;
};

export type VehicleEditSearchActionState = {
  status: "idle" | "success" | "error";
  message?: string;
  results: VehicleEditSearchResult[];
};

export type VehicleEditUpdateActionState = {
  status: "idle" | "success" | "error";
  message?: string;
  fieldErrors?: Record<string, string>;
};

export type VehicleEditSearchAction = (
  previousState: VehicleEditSearchActionState,
  formData: FormData,
) => VehicleEditSearchActionState | Promise<VehicleEditSearchActionState>;

export type VehicleEditUpdateAction = (
  previousState: VehicleEditUpdateActionState,
  formData: FormData,
) => VehicleEditUpdateActionState | Promise<VehicleEditUpdateActionState>;
