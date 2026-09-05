import { LegacyReportWrapper, type LegacyReportWrapperProps } from "@/app/CallCentre/_legacy-report-wrapper";

export default function LegacyCallCentreCloReport(props: LegacyReportWrapperProps) {
  return LegacyReportWrapper({ ...props, mode: "clo-report", path: "/CallCentre/RPT_call_cro_report.aspx" });
}
