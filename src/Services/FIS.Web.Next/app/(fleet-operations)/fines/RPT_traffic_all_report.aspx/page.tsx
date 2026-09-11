import { createLegacyFineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/legacy-route";

export default createLegacyFineReportPage({
  forcedMode: "traffic-dept",
  legacyResult: true,
  routePath: "/fines/RPT_traffic_all_report.aspx",
});
