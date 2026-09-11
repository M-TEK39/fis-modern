import FineMaintenanceRoute, { type FineMaintenancePageProps } from "./_route";

export default function FineMaintenancePage({
  searchParams,
}: Pick<FineMaintenancePageProps, "searchParams">) {
  return <FineMaintenanceRoute searchParams={searchParams} />;
}
