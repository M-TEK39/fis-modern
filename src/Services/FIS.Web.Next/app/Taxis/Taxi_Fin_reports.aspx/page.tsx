import TaxiReportsPage from "@/app/taxis/reports/page";

export default function LegacyTaxiFinancialReportsPage({ searchParams }: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return <TaxiReportsPage searchParams={searchParams} kind="financial" />;
}
