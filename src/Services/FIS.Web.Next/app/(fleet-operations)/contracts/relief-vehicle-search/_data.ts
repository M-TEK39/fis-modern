import {
  ContractApiError,
  getContract,
  searchReliefVehicles,
  type ContractRecord,
  type ReliefVehicleSearchResult,
} from "@/lib/api/finance/api-contracts";

export type ReliefVehicleSearchData =
  | { kind: "ok"; contract: ContractRecord; vehicles: ReliefVehicleSearchResult[] }
  | { kind: "unauthorized" }
  | { kind: "not-found" }
  | { kind: "error" };

export async function loadReliefVehicleSearchData({
  contractId,
  searchQuery,
  searchType,
}: Readonly<{
  contractId: number;
  searchQuery: string;
  searchType: "GG" | "GP";
}>): Promise<ReliefVehicleSearchData> {
  try {
    const [contract, reliefVehicles] = await Promise.all([
      getContract(contractId),
      searchQuery
        ? searchReliefVehicles(searchQuery)
        : Promise.resolve([] as ReliefVehicleSearchResult[]),
    ]);
    const normalizedQuery = searchQuery.toLocaleLowerCase();
    const vehicles = reliefVehicles.filter((vehicle) => {
      const value = searchType === "GG" ? vehicle.fleetNumber : vehicle.registrationNumber;
      return value?.toLocaleLowerCase().includes(normalizedQuery) === true;
    });
    return { kind: "ok", contract, vehicles };
  } catch (error) {
    if (error instanceof ContractApiError && error.reason === "unauthorized")
      return { kind: "unauthorized" };
    if (error instanceof ContractApiError && error.reason === "not-found")
      return { kind: "not-found" };
    return { kind: "error" };
  }
}
