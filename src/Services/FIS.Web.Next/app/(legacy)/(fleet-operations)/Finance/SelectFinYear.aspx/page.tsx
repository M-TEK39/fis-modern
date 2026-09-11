import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyProfitabilityPageContent(): Promise<never> {
  redirect("/finance/profitability");
}

export default function LegacyProfitabilityPage() {
  return (
    <StreamedRoute>
      <LegacyProfitabilityPageContent />
    </StreamedRoute>
  );
}
