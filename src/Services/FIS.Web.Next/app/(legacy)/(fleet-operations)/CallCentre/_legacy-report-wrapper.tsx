import { CallCentreReportPage } from "@/app/(fleet-operations)/call-centre/reports/[mode]/_route";

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
