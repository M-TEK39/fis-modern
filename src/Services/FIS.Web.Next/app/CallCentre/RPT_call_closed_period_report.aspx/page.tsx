import {
  LegacyReportWrapper,
  type LegacyReportWrapperProps,
} from "@/app/CallCentre/_legacy-report-wrapper";

export default function LegacyCallCentreOpenCallsReport(props: LegacyReportWrapperProps) {
  return LegacyReportWrapper({
    ...props,
    mode: "open-calls",
    path: "/CallCentre/RPT_call_closed_period_report.aspx",
  });
}
