import { createLegacyFineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/_route";

export default createLegacyFineReportPage({
  forcedMode: "letter",
  routePath: "/fines/RPT_letter_main_Fines.aspx",
});
