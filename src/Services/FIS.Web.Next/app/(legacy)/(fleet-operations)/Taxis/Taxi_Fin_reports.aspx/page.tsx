import TaxiReportsPage from "@/app/(fleet-operations)/taxis/reports/page";

export default function LegacyTaxiFinancialReportsPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return <TaxiReportsPage searchParams={searchParams} kind="financial" />;
}
