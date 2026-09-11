import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyBasViewPageContent(): Promise<never> {
  redirect("/finance/financial-allocation/view-bas");
}

export default function LegacyBasViewPage() {
  return (
    <StreamedRoute>
      <LegacyBasViewPageContent />
    </StreamedRoute>
  );
}
