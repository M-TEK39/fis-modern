import RecoveredVehiclePage, {
  type RecoveredVehiclePageProps,
} from "@/app/vehicles/recovered/page";

export default function LegacyRecoveredVehiclePage({ searchParams }: RecoveredVehiclePageProps) {
  return <RecoveredVehiclePage searchParams={searchParams} routePath="/Master-File/Update_Recovered_GG.aspx" />;
}
