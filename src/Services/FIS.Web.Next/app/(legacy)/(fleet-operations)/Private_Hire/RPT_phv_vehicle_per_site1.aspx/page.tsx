import PrivateHireReportPage from "@/app/(fleet-operations)/private-hire/reports/_route";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

export default function LegacyPrivateHireSiteReport({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <StreamedRoute>
      <PrivateHireReportPage
        searchParams={searchParams}
        kind="site"
        routePath="/Private_Hire/RPT_phv_vehicle_per_site1.aspx"
      />
    </StreamedRoute>
  );
}
