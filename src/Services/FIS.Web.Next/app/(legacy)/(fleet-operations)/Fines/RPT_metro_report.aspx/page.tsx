import { createLegacyFineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/_route";

export default createLegacyFineReportPage({
  forcedMode: "metro",
  legacyResult: true,
  routePath: "/Fines/RPT_metro_report.aspx",
});
