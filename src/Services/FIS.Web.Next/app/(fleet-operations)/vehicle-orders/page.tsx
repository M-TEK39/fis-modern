import VehicleMasterPage, {
  type VehicleMasterPageProps,
} from "@/app/(fleet-operations)/vehicles/page";

export default function VehicleOrdersPage({ searchParams }: VehicleMasterPageProps) {
  return (
    <VehicleMasterPage
      searchParams={searchParams}
      routePath="/vehicle-orders"
      pageTitle="Vehicle Orders"
      pageDescription="Vehicle Master Inception Maintenance"
    />
  );
}
