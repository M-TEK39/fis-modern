import LossReportRoute, { type LossReportPageProps } from "./_route";

export default function LossReportPage({
  params,
  searchParams,
}: Pick<LossReportPageProps, "params" | "searchParams">) {
  return <LossReportRoute params={params} searchParams={searchParams} />;
}
