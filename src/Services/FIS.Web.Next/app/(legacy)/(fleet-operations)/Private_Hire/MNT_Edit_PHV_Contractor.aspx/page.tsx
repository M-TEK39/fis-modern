import { PrivateHireMaintenancePage } from "@/app/(fleet-operations)/private-hire/maintenance/_route";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

export default function LegacyPrivateHireContractorEdit({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <StreamedRoute>
      <PrivateHireMaintenancePage
        searchParams={searchParams}
        forcedMode="contractor-edit"
        routePath="/Private_Hire/MNT_Edit_PHV_Contractor.aspx"
      />
    </StreamedRoute>
  );
}
