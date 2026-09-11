import { getQueryValue } from "@/app/(fleet-operations)/vehicle-verification/access";
import {
  AssetVerificationApiError,
  getAssetVerificationsForVehicle,
  type AssetVerificationRecord,
} from "@/lib/api/fleet-operations/api-asset-verification";
import { ContractApiError, getContractPage } from "@/lib/api/finance/api-contracts";
import { DepartmentApiError, getDepartment } from "@/lib/api/reference-data/api-departments";
import { SiteApiError, getSite } from "@/lib/api/reference-data/api-sites";
import {
  getVehicleForEdit,
  VehicleEditApiError,
  type VehicleEditVehicle,
} from "@/lib/api/vehicles/api-vehicle-edit";
import {
  searchVehiclesAgainstApi,
  type VehicleSearchResult,
  VehicleCreateApiError,
} from "@/lib/api/vehicles/api-vehicle-create";

import type { AssetVerificationInitial } from "./details-types";

type Mode = "add" | "edit";
type VehicleVerificationQuery = Record<string, string | string[] | undefined>;

export type AssetVerificationDetailsResult =
  | {
      kind: "ok";
      gg: string;
      message: string | undefined;
      mode: Mode;
      record: AssetVerificationRecord | undefined;
      result: string | undefined;
      vehicle: VehicleEditVehicle;
      initial: AssetVerificationInitial;
    }
  | { kind: "unauthorized" }
  | { kind: "error"; title: string; message: string; backHref: string };

function dateInput(value: string | null | undefined) {
  return value ? value.slice(0, 10) : "";
}

function exactVehicle(matches: VehicleSearchResult[], query: string) {
  const term = query.trim().toLocaleLowerCase();
  return matches.filter((vehicle) =>
    [vehicle.fleetNumber, vehicle.registrationNumber].some(
      (value) => value?.trim().toLocaleLowerCase() === term,
    ),
  );
}

function recordMatches(
  record: AssetVerificationRecord,
  vehicle: VehicleSearchResult,
  query: string,
) {
  const term = query.trim().toLocaleLowerCase();
  return (
    record.vmfCode === vehicle.vmfCode ||
    record.vehicleRegNo?.trim().toLocaleLowerCase() === term ||
    record.vehicleRegNo?.trim().toLocaleLowerCase() ===
      vehicle.registrationNumber?.trim().toLocaleLowerCase()
  );
}

function buildInitial(
  record: AssetVerificationRecord | undefined,
  vehicle: VehicleEditVehicle,
  siteCode: number,
  siteDescription: string,
  departmentDescription: string,
  siteResponsiblePerson: string,
  siteTelephone: string,
  siteFax: string,
): AssetVerificationInitial {
  return {
    province: record?.province || "Select...",
    departmentName: record?.departmentName ?? departmentDescription,
    siteName: record?.siteName ?? siteDescription,
    siteCode: String(siteCode),
    responsibleManager: record?.responsibleManager ?? siteResponsiblePerson,
    telNo: record?.telNo ?? siteTelephone,
    faxNo: record?.faxNo ?? siteFax,
    vehicleMake: record?.vehicleMake ?? "",
    vehicleModel: record?.vehicleModel ?? vehicle.modelName ?? "",
    vehicleColour: record?.vehicleColour ?? vehicle.colour ?? "",
    mobitrackFitted: record?.mobitrackFitted ?? "Select...",
    petrolCard: record?.petrolCard ?? "Select...",
    lamination: record?.lamination ?? "Select...",
    tyreBands: record?.tyreBands ?? "Select...",
    barcode: record?.barcode ?? "Select...",
    logbook: record?.logbook ?? "Select...",
    gearlock: record?.gearlock ?? "Select...",
    radio: record?.radio ?? "Select...",
    carKeys: record?.carKeys ?? "Select...",
    licenceExpiryDate: dateInput(record?.licenceExpiryDate ?? vehicle.licenceDueDate),
    barcodeNumber: record?.barcodeNumber ?? "",
    vehicleEngineNumber: record?.vehicleEngineNumber ?? vehicle.engineNumber,
    vehicleChassisNumber: record?.vehicleChassisNumber ?? vehicle.chassisNumber,
    currentKm: String(record?.currentKm ?? vehicle.currentOdo ?? ""),
    lastVerified: dateInput(record?.dateLastVerified ?? record?.verificationDate),
    comments: record?.comments ?? record?.notes ?? "",
  };
}

export async function loadAssetVerificationDetails({
  gg,
  mode,
  query,
}: Readonly<{
  gg: string;
  mode: Mode;
  query: VehicleVerificationQuery;
}>): Promise<AssetVerificationDetailsResult> {
  try {
    const matches = await searchVehiclesAgainstApi(gg);
    const exactMatches = exactVehicle(matches, gg);
    if (exactMatches.length !== 1)
      return {
        kind: "error",
        title: exactMatches.length === 0 ? "Vehicle not found." : "Vehicle selection is ambiguous.",
        message:
          exactMatches.length === 0
            ? `No exact GG or registration match was found for “${gg}”.`
            : "More than one vehicle has the same identifier. Search again using an exact identifier.",
        backHref: `/vehicle-verification/${mode}?q=${encodeURIComponent(gg)}`,
      };

    const vehicle = await getVehicleForEdit(exactMatches[0].vmfCode);
    const [records, contracts] = await Promise.all([
      getAssetVerificationsForVehicle(vehicle.vmfCode),
      getContractPage({ vmfCode: vehicle.vmfCode, page: 1, pageSize: 100 }),
    ]);
    const existing = records.find((record) => recordMatches(record, exactMatches[0], gg));
    if (mode === "add" && existing)
      return {
        kind: "error",
        title: "Asset verification already exists.",
        message: "This vehicle has already been added. Use the Edit menu option to change it.",
        backHref: "/vehicle-verification",
      };
    if (mode === "edit" && !existing)
      return {
        kind: "error",
        title: "Asset verification record not found.",
        message: "Use the Add menu option to create the vehicle's first verification record.",
        backHref: "/vehicle-verification/edit",
      };

    const contract =
      contracts.items.find((item) => item.stillCurrent?.toUpperCase() === "Y") ??
      contracts.items[0];
    const siteCode = existing?.siteCode ?? contract?.siteCode ?? null;
    if (!siteCode)
      return {
        kind: "error",
        title: "No contract site found.",
        message: `No site could be found for vehicle “${gg}”. Open or correct its contract before capturing asset verification.`,
        backHref: `/vehicle-verification/${mode}`,
      };

    const site = await getSite(siteCode);
    const department = site?.departmentCode ? await getDepartment(site.departmentCode) : null;
    return {
      kind: "ok",
      gg,
      message: getQueryValue(query.message) ?? undefined,
      mode,
      record: existing,
      result: getQueryValue(query.result) ?? undefined,
      vehicle,
      initial: buildInitial(
        existing,
        vehicle,
        siteCode,
        site?.description ?? "",
        department?.description ?? "",
        site?.responsiblePerson ?? "",
        site?.telephone ?? "",
        site?.fax ?? "",
      ),
    };
  } catch (error) {
    if (
      error instanceof AssetVerificationApiError ||
      error instanceof ContractApiError ||
      error instanceof DepartmentApiError ||
      error instanceof SiteApiError ||
      error instanceof VehicleEditApiError ||
      error instanceof VehicleCreateApiError
    ) {
      if (error.reason === "unauthorized") return { kind: "unauthorized" };
    }
    console.error(
      "FIS asset verification details request failed",
      error instanceof Error ? error.message : "unknown error",
    );
    return {
      kind: "error",
      title: "Asset verification details are unavailable.",
      message:
        "The vehicle or compatible Asset_Verification data could not be loaded. Retry when the FIS API is available.",
      backHref: `/vehicle-verification/${mode}`,
    };
  }
}
