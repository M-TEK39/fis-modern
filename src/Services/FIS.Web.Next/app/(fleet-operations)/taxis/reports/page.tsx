import TaxiReportsRoute, { type TaxiReportPageProps } from "./_route";

export default function TaxiReportsPage({
  searchParams,
}: Pick<TaxiReportPageProps, "searchParams">) {
  return <TaxiReportsRoute searchParams={searchParams} routePath="/taxis/reports" />;
}
