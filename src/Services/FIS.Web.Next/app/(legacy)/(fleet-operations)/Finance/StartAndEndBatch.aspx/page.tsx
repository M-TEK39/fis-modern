import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyStartBatchPageContent(): Promise<never> {
  redirect("/finance/batch-management/start");
}

export default function LegacyStartBatchPage() {
  return (
    <StreamedRoute>
      <LegacyStartBatchPageContent />
    </StreamedRoute>
  );
}
