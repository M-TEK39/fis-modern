import {
  ContractApiError,
  getContract,
  getContractPage,
  searchContractVehicles,
  type ContractRecord,
} from "@/lib/api/finance/api-contracts";

export type ContractPrintMenuData =
  | { kind: "ok"; results: ContractRecord[]; errorMessage?: string }
  | { kind: "unauthorized" }
  | { kind: "error"; message: string };

function positiveInt(value: string) {
  const parsed = Number(value);
  return value && Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

export async function loadContractPrintMenuData(
  mode: string,
  value: string,
): Promise<ContractPrintMenuData> {
  try {
    if (!value)
      return { kind: "ok", results: (await getContractPage({ page: 1, pageSize: 100 })).items };
    if (mode === "Contract") {
      const contractId = positiveInt(value);
      if (!contractId)
        return { kind: "ok", results: [], errorMessage: "Enter a valid contract number." };
      return { kind: "ok", results: [await getContract(contractId)] };
    }

    const vehicles = await searchContractVehicles(value);
    const normalizedValue = value.toLocaleLowerCase();
    const matches = vehicles.filter((vehicle) => {
      const candidate = mode === "GG" ? vehicle.fleetNumber : vehicle.registrationNumber;
      return candidate?.toLocaleLowerCase() === normalizedValue;
    });
    const pages = await Promise.all(
      matches.map((vehicle) =>
        getContractPage({ page: 1, pageSize: 100, vmfCode: vehicle.vmfCode }),
      ),
    );
    return { kind: "ok", results: pages.flatMap((page) => page.items) };
  } catch (error) {
    if (error instanceof ContractApiError && error.reason === "unauthorized")
      return { kind: "unauthorized" };
    return {
      kind: "error",
      message:
        error instanceof ContractApiError && error.reason === "not-found"
          ? "The requested contract was not found."
          : "Contract print records could not be loaded.",
    };
  }
}
