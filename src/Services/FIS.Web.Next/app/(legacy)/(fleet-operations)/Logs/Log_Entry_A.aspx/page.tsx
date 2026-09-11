import { redirect } from "next/navigation";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

async function LegacyLogsheetEntryAliasContent(): Promise<never> {
  redirect("/log-sheets/enter");
}

export default function LegacyLogsheetEntryAlias() {
  return (
    <StreamedRoute>
      <LegacyLogsheetEntryAliasContent />
    </StreamedRoute>
  );
}
