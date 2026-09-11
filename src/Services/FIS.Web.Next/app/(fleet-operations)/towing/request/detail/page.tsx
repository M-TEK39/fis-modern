import TowingDetailRoute, { type TowingDetailPageProps } from "./_route";

export default function TowingDetailPage({
  searchParams,
}: Pick<TowingDetailPageProps, "searchParams">) {
  return <TowingDetailRoute searchParams={searchParams} />;
}
