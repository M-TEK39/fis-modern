import { PrivateHireMaintenancePage } from "@/app/(fleet-operations)/private-hire/maintenance/_route";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

export default function LegacyPrivateHireContractorAdd({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <StreamedRoute>
      <PrivateHireMaintenancePage
        searchParams={searchParams}
        forcedMode="contractor-add"
        routePath="/Private_Hire/Add_PHV_contractor.aspx"
      />
    </StreamedRoute>
  );
}
