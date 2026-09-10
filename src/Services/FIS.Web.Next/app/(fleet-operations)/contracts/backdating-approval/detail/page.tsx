import ContractDetailPage, {
  type ContractDetailPageProps,
} from "@/app/(fleet-operations)/contracts/detail/page";

export default function BackdatingApprovalDetailPage(props: ContractDetailPageProps) {
  return <ContractDetailPage {...props} routePath="/contracts/backdating-approval" />;
}
