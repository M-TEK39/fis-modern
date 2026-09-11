import { createLegacyFineReportPage } from "@/app/(fleet-operations)/fines/reports/[mode]/legacy-route";

export default createLegacyFineReportPage({
  forcedMode: "metro",
  routePath: "/Fines/RPT_FINESPERMETRO1.aspx",
});
