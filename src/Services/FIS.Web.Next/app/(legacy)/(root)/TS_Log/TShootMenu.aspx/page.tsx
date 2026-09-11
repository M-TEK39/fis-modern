import TroubleshootPage from "@/app/(fleet-operations)/troubleshoot/_route";
import { StreamedRoute } from "@/components/app-shell/streamed-route";

export default function LegacyTroubleshootMenu({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return (
    <StreamedRoute>
      <TroubleshootPage searchParams={searchParams} routePath="/TS_Log/TShootMenu.aspx" />
    </StreamedRoute>
  );
}
