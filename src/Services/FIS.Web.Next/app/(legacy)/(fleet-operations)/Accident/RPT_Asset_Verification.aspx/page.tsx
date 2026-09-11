import AssetVerificationReportsMenu from "@/app/(fleet-operations)/reports/asset-verification/reports-menu";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyAccidentAssetVerificationReportsMenuContent() {
  return <AssetVerificationReportsMenu routePath="/Accident/RPT_Asset_Verification.aspx" />;
}

export default function LegacyAccidentAssetVerificationReportsMenu() {
  return (
    <StreamedRoute>
      <LegacyAccidentAssetVerificationReportsMenuContent />
    </StreamedRoute>
  );
}
