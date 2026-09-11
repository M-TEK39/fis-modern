import {
  ContractDetailRoute,
  type ContractDetailPageProps,
} from "@/app/(fleet-operations)/contracts/detail/_route";

type LegacyContractDetailPageProps = Pick<ContractDetailPageProps, "searchParams">;

export default function LegacyContractDetailPage({ searchParams }: LegacyContractDetailPageProps) {
  return (
    <ContractDetailRoute
      routePath="/contracts/MNT_Vehicle_Contract_DetailManagement.aspx"
      searchParams={searchParams}
    />
  );
}
