import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyBasFixPageContent(): Promise<never> {
  redirect("/finance/financial-allocation/fix-invalid-journals");
}

export default function LegacyBasFixPage() {
  return (
    <StreamedRoute>
      <LegacyBasFixPageContent />
    </StreamedRoute>
  );
}
