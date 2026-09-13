import TaxiReportsPage from "@/app/(fleet-operations)/taxis/reports/_route";

export default function ReportsTaxiPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return <TaxiReportsPage searchParams={searchParams} routePath="/reports/taxis" />;
}
