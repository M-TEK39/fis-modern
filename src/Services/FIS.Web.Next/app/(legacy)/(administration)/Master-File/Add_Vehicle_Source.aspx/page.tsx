import VehicleSourcePage from "@/app/(fleet-operations)/vehicles/source-maintenance/page";

export default function AddVehicleSourceLegacyPage(props: Parameters<typeof VehicleSourcePage>[0]) {
  return <VehicleSourcePage {...props} routePath="/Master-File/Add_Vehicle_Source.aspx" />;
}
