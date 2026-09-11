import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyFinanceProvinceReportsPageContent(): Promise<never> {
  redirect("/finance/reports/province");
}

export default function LegacyFinanceProvinceReportsPage() {
  return (
    <StreamedRoute>
      <LegacyFinanceProvinceReportsPageContent />
    </StreamedRoute>
  );
}
