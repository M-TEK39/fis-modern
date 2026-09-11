import VehicleMasterPage from "@/app/(fleet-operations)/vehicles/_route";

type PageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default function VehicleOrdersPage({ searchParams }: Readonly<PageProps>) {
  return (
    <VehicleMasterPage
      searchParams={searchParams}
      routePath="/vehicle-orders"
      pageTitle="Vehicle Orders"
      pageDescription="Vehicle Master Inception Maintenance"
    />
  );
}
