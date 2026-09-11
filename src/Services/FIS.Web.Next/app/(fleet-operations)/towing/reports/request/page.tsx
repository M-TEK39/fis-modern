import TowingRequestReportRoute, { type TowingRequestReportPageProps } from "./_route";

export default function TowingRequestReportPage({
  searchParams,
}: Pick<TowingRequestReportPageProps, "searchParams">) {
  return <TowingRequestReportRoute searchParams={searchParams} />;
}
