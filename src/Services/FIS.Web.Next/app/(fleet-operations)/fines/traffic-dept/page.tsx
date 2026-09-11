import TrafficDeptRoute, { type TrafficDeptPageProps } from "./_route";

export default function TrafficDeptPage({
  searchParams,
}: Pick<TrafficDeptPageProps, "searchParams">) {
  return <TrafficDeptRoute searchParams={searchParams} />;
}
