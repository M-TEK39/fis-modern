import { PrivateHireMaintenancePage } from "@/app/(fleet-operations)/private-hire/maintenance/_route";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

export default function LegacyPrivateHireVehicleAdd({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <StreamedRoute>
      <PrivateHireMaintenancePage
        searchParams={searchParams}
        forcedMode="add"
        routePath="/Private_Hire/MNT_Add_PHV_pass.aspx"
      />
    </StreamedRoute>
  );
}
