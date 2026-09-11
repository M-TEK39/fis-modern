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
    if (!searchQuery) {
      const pending = (await getContractPage({ page: 1, pageSize: 100, statusCode: 1 })).items;
      return { kind: "ok", pending, vehicles: [] };
    }

    const matches = await searchContractVehicles(searchQuery);
    const normalizedQuery = searchQuery.toLocaleLowerCase();
    const vehicles = matches.filter((vehicle) => {
      const value = searchType === "GG" ? vehicle.fleetNumber : vehicle.registrationNumber;
      return value?.toLocaleLowerCase().includes(normalizedQuery) === true;
    });
    const pages = await Promise.all(
      vehicles
        .slice(0, 10)
        .map((vehicle) =>
          getContractPage({ page: 1, pageSize: 100, statusCode: 1, vmfCode: vehicle.vmfCode }),
        ),
    );
    return { kind: "ok", pending: pages.flatMap((page) => page.items), vehicles };
  } catch (error) {
    if (error instanceof ContractApiError && error.reason === "unauthorized")
      return { kind: "unauthorized" };
    return { kind: "error", message: "Backdating approval records could not be loaded." };
  }
}
