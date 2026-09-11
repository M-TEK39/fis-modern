import { createLegacyFineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/legacy-route";

export default createLegacyFineReportPage({
  forcedMode: "appear-date",
  legacyResult: true,
  routePath: "/fines/RPT_app_date_report_Fines.aspx",
});
