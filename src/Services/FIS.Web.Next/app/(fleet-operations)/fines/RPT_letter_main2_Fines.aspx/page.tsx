import { createLegacyFineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/legacy-route";

export default createLegacyFineReportPage({
  forcedMode: "letter",
  legacyResult: true,
  routePath: "/fines/RPT_letter_main2_Fines.aspx",
});
