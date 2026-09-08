import {
  LegacyReportWrapper,
  type LegacyReportWrapperProps,
} from "@/app/CallCentre/_legacy-report-wrapper";

export default function LegacyCallCentreLossReferenceOutput(props: LegacyReportWrapperProps) {
  return LegacyReportWrapper({
    ...props,
    mode: "all-reference",
    path: "/CallCentre/RPT_call_all_num_reportlosses.aspx",
  });
}
