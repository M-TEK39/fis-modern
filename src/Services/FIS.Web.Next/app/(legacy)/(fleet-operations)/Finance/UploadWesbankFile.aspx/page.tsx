import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyUploadWesbankFilePageContent(): Promise<never> {
  redirect("/finance/standard-bank-import");
}

export default function LegacyUploadWesbankFilePage() {
  return (
    <StreamedRoute>
      <LegacyUploadWesbankFilePageContent />
    </StreamedRoute>
  );
}
