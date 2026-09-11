import { AssetVerificationReportPage } from "@/app/(fleet-operations)/reports/asset-verification/[mode]/_route";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export default function LegacyAssetVerificationPerSiteResultModernAlias({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  return (
    <StreamedRoute>
      <AssetVerificationReportPage
        mode="per-site-province-date"
        searchParams={searchParams}
        routePath="/Asset_Verification/RPT_asset_verification_2a.aspx"
      />
    </StreamedRoute>
  );
}
