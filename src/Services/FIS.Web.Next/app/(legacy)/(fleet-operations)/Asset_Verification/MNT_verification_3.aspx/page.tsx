import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyAssetVerificationAddSaveContent(): Promise<never> {
  redirect("/vehicle-verification/add");
}

export default function LegacyAssetVerificationAddSave() {
  return (
    <StreamedRoute>
      <LegacyAssetVerificationAddSaveContent />
    </StreamedRoute>
  );
}
