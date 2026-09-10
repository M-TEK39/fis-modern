import {
  LegacyReportWrapper,
  type LegacyReportWrapperProps,
} from "@/app/(legacy)/(fleet-operations)/CallCentre/_legacy-report-wrapper";

export default function LegacyCallCentreStatisticsReport(props: LegacyReportWrapperProps) {
  return LegacyReportWrapper({
    ...props,
    mode: "statistics",
    path: "/CallCentre/RPT_call_stat_captur_report.aspx",
  });
}
