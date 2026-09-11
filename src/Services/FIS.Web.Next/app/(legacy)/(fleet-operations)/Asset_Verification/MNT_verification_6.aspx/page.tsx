import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyAssetVerificationEditSaveContent(): Promise<never> {
  redirect("/vehicle-verification/edit");
}

export default function LegacyAssetVerificationEditSave() {
  return (
    <StreamedRoute>
      <LegacyAssetVerificationEditSaveContent />
    </StreamedRoute>
  );
}
