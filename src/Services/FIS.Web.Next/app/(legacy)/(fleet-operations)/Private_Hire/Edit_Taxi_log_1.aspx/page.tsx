import TaxiLogsPage from "@/app/(fleet-operations)/taxis/logs/page";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

export default function PrivateHireTaxiLogEdit({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <StreamedRoute>
      <TaxiLogsPage searchParams={searchParams} mode="edit" />
    </StreamedRoute>
  );
}
