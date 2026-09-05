import { LegacyReportWrapper, type LegacyReportWrapperProps } from "@/app/CallCentre/_legacy-report-wrapper";

export default function LegacyCallCentreVehicleNotFound(props: LegacyReportWrapperProps) {
  return LegacyReportWrapper({ ...props, mode: "one-vehicle", path: "/CallCentre/RPT_call_one_gg_valggfout.aspx" });
}
