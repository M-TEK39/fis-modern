import AssetVerificationReportsMenu from "@/app/(fleet-operations)/reports/asset-verification/reports-menu";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyAssetVerificationReportsMenuContent() {
  return (
    <AssetVerificationReportsMenu routePath="/Asset_Verification/RPT_Asset_Verification.aspx" />
  );
}

export default function LegacyAssetVerificationReportsMenu() {
  return (
    <StreamedRoute>
      <LegacyAssetVerificationReportsMenuContent />
    </StreamedRoute>
  );
}
