import ContractMaintenanceRoute, { type ContractMaintenancePageProps } from "./_route";

export default function ContractMaintenancePage(
  props: Pick<ContractMaintenancePageProps, "searchParams">,
) {
  return <ContractMaintenanceRoute {...props} />;
}
