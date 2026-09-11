import PrivateHireReportPage from "@/app/(fleet-operations)/private-hire/reports/_route";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

export default function LegacyPrivateHireDepartmentInServiceReport({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <StreamedRoute>
      <PrivateHireReportPage
        searchParams={searchParams}
        kind="department-in-service"
        routePath="/Private_Hire/RPT_phv_vehicle_per_dept_inserv1.aspx"
      />
    </StreamedRoute>
  );
}
