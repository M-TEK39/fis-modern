import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyRollbackBatchPageContent(): Promise<never> {
  redirect("/finance/batch-management/rollback");
}

export default function LegacyRollbackBatchPage() {
  return (
    <StreamedRoute>
      <LegacyRollbackBatchPageContent />
    </StreamedRoute>
  );
}
