import VehicleSourceRoute, { type VehicleSourcePageProps } from "./_route";

export default function VehicleSourcePage({
  searchParams,
}: Pick<VehicleSourcePageProps, "searchParams">) {
  return <VehicleSourceRoute searchParams={searchParams} />;
}
