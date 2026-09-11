import { AssetVerificationReportPage } from "@/app/(fleet-operations)/reports/asset-verification/[mode]/_route";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export default function LegacyAssetVerificationDateRangeResult({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  return (
    <StreamedRoute>
      <AssetVerificationReportPage
        mode="verified-by-date-range"
        searchParams={searchParams}
        routePath="/Asset_Verification/RPT_vehicles_verified_by_date_2.aspx"
      />
    </StreamedRoute>
  );
}
