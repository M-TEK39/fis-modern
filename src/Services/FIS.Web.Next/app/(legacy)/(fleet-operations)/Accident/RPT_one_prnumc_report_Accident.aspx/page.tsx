import { PrivateVehicleReportPage } from "@/app/(fleet-operations)/accidents/reports/private-vehicle/_route";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

export default function LegacyPrivateDescriptionReportPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  return (
    <StreamedRoute>
      <PrivateVehicleReportPage searchParams={searchParams} defaultMode="description" />
    </StreamedRoute>
  );
}
