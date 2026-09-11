import {
  ReliefVehicleSearchRoute,
  type ReliefVehicleSearchPageProps,
} from "@/app/(fleet-operations)/contracts/relief-vehicle-search/_route";

type LegacyReliefVehicleSearchPageProps = Pick<ReliefVehicleSearchPageProps, "searchParams">;

export default function LegacyReliefVehicleSearchPage({
  searchParams,
}: LegacyReliefVehicleSearchPageProps) {
  return (
    <ReliefVehicleSearchRoute
      routePath="/contracts/MNT_Vehicle_Contract_ReliefVehicleSearch.aspx"
      searchParams={searchParams}
    />
  );
}
