import TaxiReportsPage from "@/app/(fleet-operations)/taxis/reports/_route";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

export default function LegacyTaxiFinancialReportsPage({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <StreamedRoute>
      <TaxiReportsPage searchParams={searchParams} kind="financial" />
    </StreamedRoute>
  );
}
