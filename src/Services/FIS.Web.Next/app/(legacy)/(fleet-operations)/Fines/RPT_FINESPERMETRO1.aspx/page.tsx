import { createLegacyFineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/_route";

export default createLegacyFineReportPage({
  forcedMode: "metro",
  routePath: "/Fines/RPT_FINESPERMETRO1.aspx",
});
