import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyCheckScoaPageContent(): Promise<never> {
  redirect("/finance/batch-management/check-scoa");
}

export default function LegacyCheckScoaPage() {
  return (
    <StreamedRoute>
      <LegacyCheckScoaPageContent />
    </StreamedRoute>
  );
}
