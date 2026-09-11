import { createLegacyFineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/_route";

export default createLegacyFineReportPage({
  forcedMode: "letter",
  legacyResult: true,
  routePath: "/fines/RPT_letter_main2_Fines.aspx",
});
