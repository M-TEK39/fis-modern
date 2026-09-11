import { createLegacyFineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/_route";

export default createLegacyFineReportPage({
  forcedMode: "dept-period",
  legacyResult: true,
  routePath: "/fines/RPT_dept_period_report_Fines.aspx",
});
