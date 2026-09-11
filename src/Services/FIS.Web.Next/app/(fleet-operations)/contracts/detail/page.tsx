import ContractDetailRoute, { type ContractDetailPageProps } from "./_route";

export default function ContractDetailPage(props: Pick<ContractDetailPageProps, "searchParams">) {
  return <ContractDetailRoute {...props} />;
}
