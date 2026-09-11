import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyBasActivatePageContent(): Promise<never> {
  redirect("/finance/financial-allocation/activate-bas");
}

export default function LegacyBasActivatePage() {
  return (
    <StreamedRoute>
      <LegacyBasActivatePageContent />
    </StreamedRoute>
  );
}
