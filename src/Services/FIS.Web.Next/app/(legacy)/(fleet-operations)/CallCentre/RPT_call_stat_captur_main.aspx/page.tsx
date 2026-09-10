import { CallCentreReportPage } from "@/app/(fleet-operations)/call-centre/reports/[mode]/page";

export default function LegacyCallCentreStatistics(props: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  return CallCentreReportPage({
    ...props,
    forcedMode: "statistics",
    routePath: "/CallCentre/RPT_call_stat_captur_main.aspx",
  });
}
