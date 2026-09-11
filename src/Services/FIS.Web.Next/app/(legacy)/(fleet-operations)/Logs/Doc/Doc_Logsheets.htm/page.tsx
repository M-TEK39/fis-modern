import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyLogsheetsManualAliasContent(): Promise<never> {
  redirect("/manuals");
}

export default function LegacyLogsheetsManualAlias() {
  return (
    <StreamedRoute>
      <LegacyLogsheetsManualAliasContent />
    </StreamedRoute>
  );
}
