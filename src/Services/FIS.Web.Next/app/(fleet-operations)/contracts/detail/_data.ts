import {
  ContractApiError,
  getContract,
  searchContractVehicles,
  type ContractRecord,
  type ContractVehicleSearchResult,
} from "@/lib/api/finance/api-contracts";
import { getDepartments, type DepartmentRecord } from "@/lib/api/reference-data/api-departments";
import {
  getDriverManagementSiteDrivers,
  type DriverManagementDriver,
} from "@/lib/api/reference-data/api-driver-management";
import { getSites, type SiteRecord } from "@/lib/api/reference-data/api-sites";
import {
  canCaptureNewContract,
  canCloseActiveContract,
  canEditContract,
  canManageActiveContract,
  canReviewContract,
  canSubmitContract,
  type ContractSession,
} from "@/app/(fleet-operations)/contracts/access";

export type ContractReferenceData = Readonly<{
  sites: SiteRecord[];
  departments: DepartmentRecord[];
  drivers: DriverManagementDriver[];
}>;

export type ContractDetailData =
  | {
      kind: "ok";
      canCancelClose: boolean;
      canCapture: boolean;
      canEdit: boolean;
      canManage: boolean;
      isActive: boolean;
      contract: ContractRecord | null;
      references: ContractReferenceData;
      today: string;
      vehicle: ContractVehicleSearchResult | null;
    }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "no-selection" }
  | { kind: "error" };

export async function loadContractDetailData({
  contractId,
  identifier,
  requestedVmfCode,
  session,
}: Readonly<{
  contractId: number | null;
  identifier: string;
  requestedVmfCode: number | null;
  session: ContractSession;
}>): Promise<ContractDetailData> {
  try {
    let contract: ContractRecord | null = contractId ? await getContract(contractId) : null;
    let vehicle: ContractVehicleSearchResult | null = requestedVmfCode
      ? ((await searchContractVehicles(String(requestedVmfCode))).find(
          (item) => item.vmfCode === requestedVmfCode,
        ) ?? null)
      : null;
    if (!vehicle && identifier) vehicle = (await searchContractVehicles(identifier))[0] ?? null;
    if (!contract && !vehicle) return { kind: "no-selection" };

    const canCapture = !contract && vehicle ? canCaptureNewContract(session.roles) : false;
    const canEdit =
      contract && [0, 4].includes(contract.contractStatusCode ?? -1)
        ? canEditContract(contract, session)
        : false;
    const isActive = Boolean(
      contract?.contractStatusCode === 3 ||
      (contract?.contractStatusCode === null && contract.stillCurrent?.toUpperCase() === "Y"),
    );
    const canManage = isActive ? canManageActiveContract(session.roles) : false;
    const canCancelClose = isActive ? canCloseActiveContract(session.roles) : false;
    const needsReferences = Boolean(canCapture || canEdit || canManage || canCancelClose);
    const references: ContractReferenceData = needsReferences
      ? await loadReferences()
      : { sites: [], departments: [], drivers: [] };
    return {
      kind: "ok",
      canCancelClose,
      canCapture,
      canEdit,
      canManage,
      isActive,
      contract,
      references,
      today: new Date().toISOString().slice(0, 10),
      vehicle,
    };
  } catch (error) {
    if (error instanceof ContractApiError && error.reason === "unauthorized")
      return { kind: "unauthorized" };
    if (error instanceof ContractApiError && error.reason === "not-found")
      return { kind: "not-found" };
    return { kind: "error" };
  }
}

async function loadReferences(): Promise<ContractReferenceData> {
  const [sites, departments, drivers] = await Promise.all([
    getSites(),
    getDepartments(),
    getDriverManagementSiteDrivers(),
  ]);
  return { sites, departments, drivers };
}
