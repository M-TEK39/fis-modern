import TroubleshootPage from "@/app/troubleshoot/page";

export default function LegacyTroubleshootMenu({
  searchParams,
}: Readonly<{ searchParams: Promise<Record<string, string | string[] | undefined>> }>) {
  return <TroubleshootPage searchParams={searchParams} routePath="/TS_Log/TShootMenu.aspx" />;
}
