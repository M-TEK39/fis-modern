import PrivateHireReportPage from "@/app/(fleet-operations)/private-hire/reports/_route";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

export default function LegacyPrivateHireOneVehicleReport({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <StreamedRoute>
      <PrivateHireReportPage
        searchParams={searchParams}
        kind="one"
        routePath="/Private_Hire/Get_gp.aspx"
      />
    </StreamedRoute>
  );
}
