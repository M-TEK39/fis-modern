import { LegacyReportWrapper, type LegacyReportWrapperProps } from "@/app/CallCentre/_legacy-report-wrapper";

export default function LegacyCallCentreOneReferenceSplit(props: LegacyReportWrapperProps) {
  return LegacyReportWrapper({ ...props, mode: "one-reference", path: "/CallCentre/RPT_call_one_num_split.aspx" });
}
