import TowingFirmDateRoute, { type TowingFirmDatePageProps } from "./_route";

export default function TowingFirmDatePage({
  searchParams,
}: Pick<TowingFirmDatePageProps, "searchParams">) {
  return <TowingFirmDateRoute searchParams={searchParams} />;
}
