import RenumberedReportPageRoute, { type RenumberedReportPageProps } from "./_route";

export default function RenumberedReportPage({
  searchParams,
}: Pick<RenumberedReportPageProps, "searchParams">) {
  return <RenumberedReportPageRoute searchParams={searchParams} />;
}
