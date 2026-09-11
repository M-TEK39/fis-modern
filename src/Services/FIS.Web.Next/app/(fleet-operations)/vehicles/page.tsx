import VehicleMasterRoute, { type VehicleMasterPageProps } from "./_route";

export default function VehicleMasterPage({
  searchParams,
}: Pick<VehicleMasterPageProps, "searchParams">) {
  return <VehicleMasterRoute searchParams={searchParams} />;
}
