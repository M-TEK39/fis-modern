import {
  LegacyReportWrapper,
  type LegacyReportWrapperProps,
} from "@/app/CallCentre/_legacy-report-wrapper";

export default function LegacyCallCentreAllReferenceOutput(props: LegacyReportWrapperProps) {
  return LegacyReportWrapper({
    ...props,
    mode: "all-reference",
    path: "/CallCentre/RPT_call_all_num_report.aspx",
  });
}
