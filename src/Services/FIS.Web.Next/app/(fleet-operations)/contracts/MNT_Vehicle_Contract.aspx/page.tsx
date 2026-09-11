import {
  ContractMaintenanceRoute,
  type ContractMaintenancePageProps,
} from "@/app/(fleet-operations)/contracts/maintenance/_route";

type LegacyContractMaintenancePageProps = Pick<ContractMaintenancePageProps, "searchParams">;

export default function LegacyContractMaintenancePage({
  searchParams,
}: LegacyContractMaintenancePageProps) {
  return (
    <ContractMaintenanceRoute
      routePath="/contracts/MNT_Vehicle_Contract.aspx"
      searchParams={searchParams}
    />
  );
}
