import AssetVerificationReportsMenu from "@/app/(fleet-operations)/reports/asset-verification/reports-menu";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyAccidentAssetVerificationReportsAliasContent() {
  return <AssetVerificationReportsMenu routePath="/Accident/RPT_Asset_Ver_1.aspx" />;
}

export default function LegacyAccidentAssetVerificationReportsAlias() {
  return (
    <StreamedRoute>
      <LegacyAccidentAssetVerificationReportsAliasContent />
    </StreamedRoute>
  );
}
