import PrivateHireReportPage from "@/app/(fleet-operations)/private-hire/reports/_route";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

export default function LegacyPrivateHireAllVehiclesReport({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <StreamedRoute>
      <PrivateHireReportPage
        searchParams={searchParams}
        kind="all"
        routePath="/Private_Hire/RPT_all_private_hire_vehicles.aspx"
      />
    </StreamedRoute>
  );
}
