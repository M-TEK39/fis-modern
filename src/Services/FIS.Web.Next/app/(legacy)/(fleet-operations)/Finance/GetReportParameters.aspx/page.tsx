import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyFinanceReportParametersPageContent(): Promise<never> {
  redirect("/finance/reports/reversals-tree");
}

export default function LegacyFinanceReportParametersPage() {
  return (
    <StreamedRoute>
      <LegacyFinanceReportParametersPageContent />
    </StreamedRoute>
  );
}
