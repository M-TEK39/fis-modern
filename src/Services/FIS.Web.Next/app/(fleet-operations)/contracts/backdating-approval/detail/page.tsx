import {
  ContractDetailRoute,
  type ContractDetailPageProps,
} from "@/app/(fleet-operations)/contracts/detail/_route";

type BackdatingApprovalDetailPageProps = Pick<ContractDetailPageProps, "searchParams">;

export default function BackdatingApprovalDetailPage({
  searchParams,
}: BackdatingApprovalDetailPageProps) {
  return (
    <ContractDetailRoute routePath="/contracts/backdating-approval" searchParams={searchParams} />
  );
}
