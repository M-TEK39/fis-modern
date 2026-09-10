import VehicleSourcePage from "@/app/(fleet-operations)/vehicles/source-maintenance/page";

export default function EditVehicleSourceDetailLegacyPage(
  props: Parameters<typeof VehicleSourcePage>[0],
) {
  return <VehicleSourcePage {...props} routePath="/Master-File/Edit_Vehicle_Source_2.aspx" />;
}
