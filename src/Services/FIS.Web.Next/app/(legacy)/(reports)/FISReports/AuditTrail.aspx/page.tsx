import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyAuditTrailPageContent(): Promise<never> {
  redirect("/finance/audit-trail");
}

export default function LegacyAuditTrailPage() {
  return (
    <StreamedRoute>
      <LegacyAuditTrailPageContent />
    </StreamedRoute>
  );
}
