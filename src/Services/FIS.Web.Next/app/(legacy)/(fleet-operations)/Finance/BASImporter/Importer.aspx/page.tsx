import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyBasImportPageContent(): Promise<never> {
  redirect("/finance/financial-allocation/import-bas");
}

export default function LegacyBasImportPage() {
  return (
    <StreamedRoute>
      <LegacyBasImportPageContent />
    </StreamedRoute>
  );
}
