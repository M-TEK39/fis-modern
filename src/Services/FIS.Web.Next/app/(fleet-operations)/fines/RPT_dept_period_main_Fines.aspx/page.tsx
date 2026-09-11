import { createLegacyFineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/legacy-route";

export default createLegacyFineReportPage({
  forcedMode: "dept-period",
  routePath: "/fines/RPT_dept_period_main_Fines.aspx",
});
