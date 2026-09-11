import { PrivateHireMaintenancePage } from "@/app/(fleet-operations)/private-hire/maintenance/_route";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

export default function LegacyPrivateHireVehicleDelete({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <StreamedRoute>
      <PrivateHireMaintenancePage
        searchParams={searchParams}
        forcedMode="delete"
        routePath="/Private_Hire/MNT_del_phv_pass.aspx"
      />
    </StreamedRoute>
  );
}
