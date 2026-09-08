import VehicleSourcePage from "@/app/vehicles/source-maintenance/page";

export default function EditVehicleSourceLegacyPage(
  props: Parameters<typeof VehicleSourcePage>[0],
) {
  return <VehicleSourcePage {...props} routePath="/Master-File/Edit_Vehicle_Source.aspx" />;
}
