import {
  LegacyReportWrapper,
  type LegacyReportWrapperProps,
} from "@/app/(legacy)/(fleet-operations)/CallCentre/_legacy-report-wrapper";

export default function LegacyCallCentreVehicleSplit(props: LegacyReportWrapperProps) {
  return LegacyReportWrapper({
    ...props,
    mode: "one-vehicle",
    path: "/CallCentre/RPT_call_one_gg_split.aspx",
  });
}
