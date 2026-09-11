import { createLegacyFineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/_route";

export default createLegacyFineReportPage({
  forcedMode: "fine-detail",
  legacyResult: true,
  routePath: "/fines/RPT_letter_report_Fines.aspx",
});
