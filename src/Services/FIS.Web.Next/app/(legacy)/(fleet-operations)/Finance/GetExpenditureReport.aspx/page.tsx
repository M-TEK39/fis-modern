import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyOutstandingPageContent(): Promise<never> {
  redirect("/finance/outstanding/department-site-vehicle");
}

export default function LegacyOutstandingPage() {
  return (
    <StreamedRoute>
      <LegacyOutstandingPageContent />
    </StreamedRoute>
  );
}
