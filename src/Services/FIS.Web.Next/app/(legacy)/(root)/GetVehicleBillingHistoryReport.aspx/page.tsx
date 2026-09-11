import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyVehicleBillingHistoryPageContent(): Promise<never> {
  redirect("/finance/reports/vehicle-billing-history");
}

export default function LegacyVehicleBillingHistoryPage() {
  return (
    <StreamedRoute>
      <LegacyVehicleBillingHistoryPageContent />
    </StreamedRoute>
  );
}
