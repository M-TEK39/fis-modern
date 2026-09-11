import TowTruckDataRoute, { type TowTruckDataPageProps } from "./_route";

export default function TowTruckDataPage({
  searchParams,
}: Pick<TowTruckDataPageProps, "searchParams">) {
  return <TowTruckDataRoute searchParams={searchParams} />;
}
