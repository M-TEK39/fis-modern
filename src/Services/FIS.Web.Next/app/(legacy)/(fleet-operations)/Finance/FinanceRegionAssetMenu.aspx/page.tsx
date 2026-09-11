import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyRegionalAssetMenuPageContent(): Promise<never> {
  redirect("/finance/regional/assets");
}

export default function LegacyRegionalAssetMenuPage() {
  return (
    <StreamedRoute>
      <LegacyRegionalAssetMenuPageContent />
    </StreamedRoute>
  );
}
