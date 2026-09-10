import {
  LegacyReportWrapper,
  type LegacyReportWrapperProps,
} from "@/app/(legacy)/(fleet-operations)/CallCentre/_legacy-report-wrapper";

export default function LegacyCallCentreOneReferenceReport(props: LegacyReportWrapperProps) {
  return LegacyReportWrapper({
    ...props,
    mode: "one-reference",
    path: "/CallCentre/RPT_call_one_num_report.aspx",
  });
}
