import RecoveredVehicleRoute, { type RecoveredVehiclePageProps } from "./_route";

export default function RecoveredVehiclePage({
  searchParams,
}: Pick<RecoveredVehiclePageProps, "searchParams">) {
  return <RecoveredVehicleRoute searchParams={searchParams} />;
}
