import { AssetVerificationReportPage } from "@/app/(fleet-operations)/reports/asset-verification/[mode]/page";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export default function LegacyAssetVerificationPerSiteResult({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  return (
    <AssetVerificationReportPage
      mode="per-site-province-date"
      searchParams={searchParams}
      routePath="/Asset_Verification/RPT_asset_verification_2.aspx"
    />
  );
}
