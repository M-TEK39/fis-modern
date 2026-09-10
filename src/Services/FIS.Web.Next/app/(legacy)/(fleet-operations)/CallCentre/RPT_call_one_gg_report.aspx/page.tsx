import {
  LegacyReportWrapper,
  type LegacyReportWrapperProps,
} from "@/app/(legacy)/(fleet-operations)/CallCentre/_legacy-report-wrapper";

export default function LegacyCallCentreVehicleReport(props: LegacyReportWrapperProps) {
  return LegacyReportWrapper({
    ...props,
    mode: "one-vehicle",
    path: "/CallCentre/RPT_call_one_gg_report.aspx",
  });
}
