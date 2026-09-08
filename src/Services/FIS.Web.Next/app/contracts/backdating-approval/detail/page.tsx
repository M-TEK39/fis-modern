import ContractDetailPage, { type ContractDetailPageProps } from "@/app/contracts/detail/page";

export default function BackdatingApprovalDetailPage(props: ContractDetailPageProps) {
  return <ContractDetailPage {...props} routePath="/contracts/backdating-approval" />;
}
