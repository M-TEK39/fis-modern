import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyFiscalTariffParametersPageContent(): Promise<never> {
  redirect("/finance/tariff-parameters");
}

export default function LegacyFiscalTariffParametersPage() {
  return (
    <StreamedRoute>
      <LegacyFiscalTariffParametersPageContent />
    </StreamedRoute>
  );
}
