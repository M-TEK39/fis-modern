import TowTruckDetailRoute, { type TowTruckDetailPageProps } from "./_route";

export default function TowTruckDetailPage({
  searchParams,
}: Pick<TowTruckDetailPageProps, "searchParams">) {
  return <TowTruckDetailRoute searchParams={searchParams} />;
}
