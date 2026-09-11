import TrafficDeptDetailRoute, { type TrafficDeptDetailPageProps } from "./_route";

export default function TrafficDeptDetailPage({
  searchParams,
}: Pick<TrafficDeptDetailPageProps, "searchParams">) {
  return <TrafficDeptDetailRoute searchParams={searchParams} />;
}
