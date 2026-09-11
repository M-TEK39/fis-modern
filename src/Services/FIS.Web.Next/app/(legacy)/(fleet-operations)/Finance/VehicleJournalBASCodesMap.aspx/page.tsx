import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyBasFundPageContent(): Promise<never> {
  redirect("/finance/financial-allocation/allocate-fund-codes");
}

export default function LegacyBasFundPage() {
  return (
    <StreamedRoute>
      <LegacyBasFundPageContent />
    </StreamedRoute>
  );
}
