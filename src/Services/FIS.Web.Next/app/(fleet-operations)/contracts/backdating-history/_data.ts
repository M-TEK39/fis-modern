import {
  ContractApiError,
  getContract,
  getContractPage,
  searchContractVehicles,
  type ContractRecord,
  type ContractVehicleSearchResult,
} from "@/lib/api/finance/api-contracts";

export type BackdatingHistoryData =
  | {
      kind: "ok";
      contracts: ContractRecord[];
      selectedContract: ContractRecord | null;
      vehicles: ContractVehicleSearchResult[];
    }
  | { kind: "unauthorized" }
  | { kind: "error"; message: string };

export async function loadBackdatingHistoryData({
  requestedContractId,
  requestedVmfCode,
  searchQuery,
  searchType,
}: Readonly<{
  requestedContractId: number | null;
  requestedVmfCode: number | null;
  searchQuery: string;
  searchType: "GG" | "GP";
}>): Promise<BackdatingHistoryData> {
  try {
    const [matches, loadedSelectedContract] = await Promise.all([
      searchQuery
        ? searchContractVehicles(searchQuery)
        : Promise.resolve([] as ContractVehicleSearchResult[]),
      requestedContractId ? getContract(requestedContractId) : Promise.resolve(null),
    ]);
    const normalizedQuery = searchQuery.toLocaleLowerCase();
    const vehicles = matches.filter((vehicle) => {
      const value = searchType === "GG" ? vehicle.fleetNumber : vehicle.registrationNumber;
      return value?.toLocaleLowerCase().includes(normalizedQuery) === true;
    });
    let selectedContract = loadedSelectedContract;
    let contracts: ContractRecord[] = [];
    const vmfCode = requestedVmfCode ?? selectedContract?.vmfCode ?? null;
    if (vmfCode) {
      contracts = (await getContractPage({ page: 1, pageSize: 100, vmfCode })).items;
      if (selectedContract)
        selectedContract =
          contracts.find((contract) => contract.contractCode === selectedContract?.contractCode) ??
          selectedContract;
    }
    return { kind: "ok", contracts, selectedContract, vehicles };
  } catch (error) {
    if (error instanceof ContractApiError && error.reason === "unauthorized")
      return { kind: "unauthorized" };
    return {
      kind: "error",
      message:
        error instanceof ContractApiError && error.reason === "not-found"
          ? "The requested contract was not found."
          : "Contract history could not be loaded.",
    };
  }
}
