import { FineReportPage } from "@/app/fines/reports/[mode]/page";

export default function LegacyDeptPeriodReportResultPage(
  props: Parameters<typeof FineReportPage>[0],
) {
  return (
    <FineReportPage
      {...props}
      forcedMode="dept-period"
      legacyResult
      routePath="/fines/RPT_dept_period_report_Fines.aspx"
    />
  );
}
