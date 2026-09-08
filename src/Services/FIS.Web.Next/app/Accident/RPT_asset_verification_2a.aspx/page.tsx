import { AssetVerificationReportPage } from "@/app/reports/asset-verification/[mode]/page";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export default function LegacyAccidentAssetVerificationPerSiteResult({
  searchParams,
}: Readonly<{ searchParams: SearchParams }>) {
  return (
    <AssetVerificationReportPage
      mode="per-site-province-date"
      searchParams={searchParams}
      routePath="/Accident/RPT_asset_verification_2a.aspx"
    />
  );
}
