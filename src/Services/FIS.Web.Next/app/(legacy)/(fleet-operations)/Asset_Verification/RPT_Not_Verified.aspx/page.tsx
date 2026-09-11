import { AssetVerificationReportPage } from "@/app/(fleet-operations)/reports/asset-verification/[mode]/_route";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export default function LegacyAssetVerificationNotVerifiedReport({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  return (
    <StreamedRoute>
      <AssetVerificationReportPage
        mode="not-verified"
        searchParams={searchParams}
        routePath="/Asset_Verification/RPT_Not_Verified.aspx"
      />
    </StreamedRoute>
  );
}
