import {
  LegacyReportWrapper,
  type LegacyReportWrapperProps,
} from "@/app/(legacy)/(fleet-operations)/CallCentre/_legacy-report-wrapper";

export default function LegacyCallCentreDataAccessReport(props: LegacyReportWrapperProps) {
  return LegacyReportWrapper({
    ...props,
    mode: "data-access",
    path: "/CallCentre/RPT_call_oneaccess_report.aspx",
  });
}
