import {
  ContractApiError,
  getContractPage,
  searchContractVehicles,
  type ContractRecord,
  type ContractVehicleSearchResult,
} from "@/lib/api/finance/api-contracts";

export type BackdatingApprovalData =
  | {
      kind: "ok";
      pending: ContractRecord[];
      vehicles: ContractVehicleSearchResult[];
    }
  | { kind: "unauthorized" }
  | { kind: "error"; message: string };

export async function loadBackdatingApprovalData(
  searchType: "GG" | "GP",
  searchQuery: string,
): Promise<BackdatingApprovalData> {
  try {
    const pending = (
      await getContractPage({
        page: 1,
        pageSize: 100,
        list: "backdating-action-required",
        statusCode: 1,
      })
    ).items;
    if (!searchQuery) {
      return { kind: "ok", pending, vehicles: [] };
    }

    const matches = await searchContractVehicles(searchQuery);
    const normalizedQuery = searchQuery.toLocaleLowerCase();
    const vehicles = matches.filter((vehicle) => {
      const value = searchType === "GG" ? vehicle.fleetNumber : vehicle.registrationNumber;
      return value?.toLocaleLowerCase().includes(normalizedQuery) === true;
    });
    return { kind: "ok", pending, vehicles };
  } catch (error) {
    if (error instanceof ContractApiError && error.reason === "unauthorized")
      return { kind: "unauthorized" };
    return { kind: "error", message: "Backdating approval records could not be loaded." };
  }
}
