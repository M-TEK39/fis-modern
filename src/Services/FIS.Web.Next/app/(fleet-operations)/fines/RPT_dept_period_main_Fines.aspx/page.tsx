import { FineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/page";

export default function LegacyDeptPeriodReportPage(props: Parameters<typeof FineReportPage>[0]) {
  return (
    <FineReportPage
      {...props}
      forcedMode="dept-period"
      routePath="/fines/RPT_dept_period_main_Fines.aspx"
    />
  );
}
