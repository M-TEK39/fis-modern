import { CallCentreReportPage } from "@/app/call-centre/reports/[mode]/page";

export type LegacyReportWrapperProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export function LegacyReportWrapper({
  searchParams,
  mode,
  path,
}: LegacyReportWrapperProps & { mode: string; path: string }) {
  return CallCentreReportPage({ searchParams, forcedMode: mode, routePath: path });
}
