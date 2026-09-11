import { createLegacyFineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/_route";

export default createLegacyFineReportPage({
  forcedMode: "traffic-dept",
  legacyResult: true,
  routePath: "/fines/RPT_traffic_all_report.aspx",
});
