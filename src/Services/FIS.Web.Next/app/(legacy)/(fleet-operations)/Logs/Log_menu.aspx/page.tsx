import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyLogsheetMenuAliasContent(): Promise<never> {
  redirect("/log-sheets");
}

export default function LegacyLogsheetMenuAlias() {
  return (
    <StreamedRoute>
      <LegacyLogsheetMenuAliasContent />
    </StreamedRoute>
  );
}
