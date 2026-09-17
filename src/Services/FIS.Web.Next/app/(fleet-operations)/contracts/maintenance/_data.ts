import {
  ContractApiError,
  getContractPage,
  searchContractVehicles,
  type ContractPage,
  type ContractVehicleSearchResult,
} from "@/lib/api/finance/api-contracts";
import { SiteApiError, getSites, type SiteRecord } from "@/lib/api/reference-data/api-sites";

export type ContractMaintenanceData =
  | {
      kind: "ok";
      filteredVehicles: ContractVehicleSearchResult[];
      pageData: ContractPage;
      siteLookupUnavailable: boolean;
      sites: SiteRecord[];
    }
  | { kind: "unauthorized" }
  | { kind: "forbidden" }
  | { kind: "error" };

export async function loadContractMaintenanceData({
  page,
  searchQuery,
  searchType,
  siteCode,
  startDateFrom,
  startDateTo,
  statusFilter,
  stillCurrent,
}: Readonly<{
  page: number;
  searchQuery: string;
  searchType: "GG" | "GP";
  siteCode: number | undefined;
  startDateFrom: string | undefined;
  startDateTo: string | undefined;
  statusFilter: number | undefined;
  stillCurrent: string | undefined;
}>): Promise<ContractMaintenanceData> {
  try {
    const [pageData, vehicles] = await Promise.all([
      getContractPage({
        page,
        pageSize: 24,
        statusCode: statusFilter,
        siteCode,
        stillCurrent,
        startDateFrom,
        startDateTo,
      }),
      searchQuery
        ? searchContractVehicles(searchQuery)
        : Promise.resolve([] as ContractVehicleSearchResult[]),
    ]);
    let sites: SiteRecord[] = [];
    let siteLookupUnavailable = false;
    try {
      sites = await getSites();
    } catch (error) {
      if (error instanceof SiteApiError) siteLookupUnavailable = true;
      else throw error;
    }
    const normalizedQuery = searchQuery.toLocaleLowerCase();
    const filteredVehicles = searchQuery
      ? vehicles.filter((vehicle) => {
          const value = searchType === "GG" ? vehicle.fleetNumber : vehicle.registrationNumber;
          return value?.toLocaleLowerCase().includes(normalizedQuery) === true;
        })
      : [];
    return { kind: "ok", filteredVehicles, pageData, siteLookupUnavailable, sites };
  } catch (error) {
    if (
      (error instanceof ContractApiError || error instanceof SiteApiError) &&
      error.reason === "unauthorized"
    )
      return { kind: "unauthorized" };
    if (error instanceof ContractApiError && error.reason === "forbidden")
      return { kind: "forbidden" };
    return { kind: "error" };
  }
}
