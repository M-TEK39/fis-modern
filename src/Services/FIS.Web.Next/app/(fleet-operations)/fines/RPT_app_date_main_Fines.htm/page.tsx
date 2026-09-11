import { createLegacyFineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/legacy-route";

export default createLegacyFineReportPage({
  forcedMode: "appear-date",
  routePath: "/fines/RPT_app_date_main_Fines.htm",
});
