import VehicleMasterPage from "@/app/vehicles/page";

type LegacyVehicleMasterPageProps = {
  searchParams: Promise<{ page?: string | string[] }>;
};

export default function LegacyVehicleMasterPage({ searchParams }: LegacyVehicleMasterPageProps) {
  return (
    <VehicleMasterPage routePath="/Master-File/Vehicle_Master.aspx" searchParams={searchParams} />
  );
}
