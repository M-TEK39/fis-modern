import { redirect } from "next/navigation";

import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyVehicleBillingHistoryParametersContent(): Promise<never> {
  // The legacy page only selected the registration and financial year before
  // opening the report; the modern route retains that same scoped form.
  redirect("/finance/reports/vehicle-billing-history");
}

export default function LegacyVehicleBillingHistoryParametersPage() {
  return (
    <StreamedRoute>
      <LegacyVehicleBillingHistoryParametersContent />
    </StreamedRoute>
  );
}
