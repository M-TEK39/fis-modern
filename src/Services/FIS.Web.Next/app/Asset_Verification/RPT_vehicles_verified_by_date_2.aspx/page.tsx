import { AssetVerificationReportPage } from "@/app/reports/asset-verification/[mode]/page";

type SearchParams = Promise<Record<string, string | string[] | undefined>>;

export default function LegacyAssetVerificationDateRangeResult({ searchParams }: Readonly<{ searchParams: SearchParams }>) {
  return <AssetVerificationReportPage mode="verified-by-date-range" searchParams={searchParams} routePath="/Asset_Verification/RPT_vehicles_verified_by_date_2.aspx" />;
}
