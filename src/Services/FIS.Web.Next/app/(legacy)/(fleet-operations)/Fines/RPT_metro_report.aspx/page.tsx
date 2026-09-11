import { createLegacyFineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/legacy-route";

export default createLegacyFineReportPage({
  forcedMode: "metro",
  legacyResult: true,
  routePath: "/Fines/RPT_metro_report.aspx",
});
